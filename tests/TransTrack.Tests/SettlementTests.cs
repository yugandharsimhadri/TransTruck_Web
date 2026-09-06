using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// Settling several trips with one payment.
///
/// The risk here is not the arithmetic — each line is an ordinary receipt — but
/// the atomicity. A settlement that posts the money without closing the trips,
/// or closes the trips without posting the money, is worse than no feature at
/// all, because both halves look done from the screen that shows the other one.
/// </summary>
public class SettlementTests
{
    private static async Task<Trip> ReloadAsync(TestWorld world, Guid id)
    {
        await using var db = await world.Factory.CreateDbContextAsync();
        return await db.Trips
            .Include(t => t.Transactions.Where(x => !x.IsDeleted))
            .AsNoTracking()
            .FirstAsync(t => t.Id == id);
    }

    private static async Task<Guid> SettleAllAsync(TestWorld world, params Guid[] tripIds) =>
        await world.Settlements.CreateAsync(
            world.PartyId, DateTime.Today, PaymentMode.Bank, tripIds, "March settlement", world.UserId);

    [Fact]
    public async Task Only_open_trips_with_something_owing_can_be_settled()
    {
        await using var world = await TestWorld.CreateAsync();

        var open = await world.BookTripAsync(amount: 10_000m);
        var closed = await world.BookTripAsync(amount: 5_000m);
        await world.Trips.CloseAsync(closed, world.UserId);

        var settleable = await world.Settlements.GetSettleableTripsAsync(world.PartyId);

        Assert.Contains(settleable, t => t.TripId == open);
        Assert.DoesNotContain(settleable, t => t.TripId == closed);
    }

    [Fact]
    public async Task A_settlement_moves_no_money_until_it_is_approved()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);

        await SettleAllAsync(world, tripId);

        var trip = await ReloadAsync(world, tripId);
        Assert.Equal(0m, trip.TotalApprovedReceived);
        Assert.Equal(10_000m, trip.BalanceReceivable);
        Assert.Equal(TripStatus.Open, trip.Status);
    }

    [Fact]
    public async Task Approving_posts_every_receipt_and_closes_every_trip()
    {
        await using var world = await TestWorld.CreateAsync();
        var first = await world.BookTripAsync(amount: 10_000m);
        var second = await world.BookTripAsync(amount: 6_500m);

        var settlementId = await SettleAllAsync(world, first, second);
        await world.Settlements.ApproveAsync(settlementId, world.UserId, "Received by NEFT");

        foreach (var (id, amount) in new[] { (first, 10_000m), (second, 6_500m) })
        {
            var trip = await ReloadAsync(world, id);
            Assert.Equal(TripStatus.Closed, trip.Status);
            Assert.Equal(amount, trip.TotalApprovedReceived);
            Assert.Equal(0m, trip.BalanceReceivable);
        }
    }

    [Fact]
    public async Task The_settlement_total_is_what_the_party_is_asked_to_confirm()
    {
        await using var world = await TestWorld.CreateAsync();
        var first = await world.BookTripAsync(amount: 10_000m);
        var second = await world.BookTripAsync(amount: 6_500m);

        var settlementId = await SettleAllAsync(world, first, second);

        var settlement = await world.Settlements.GetAsync(settlementId);
        Assert.NotNull(settlement);
        Assert.Equal(16_500m, settlement!.TotalAmount);
        Assert.Equal(2, settlement.TripCount);
    }

    [Fact]
    public async Task Only_the_balance_is_settled_not_the_whole_invoice()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);

        // An approved advance already sits against this trip.
        await world.AddAmountAsync(tripId, 4_000m);
        var pending = await world.Transactions.GetPendingAsync();
        await world.Transactions.ApproveAsync(pending.Single().Id, world.UserId, null);

        var settleable = await world.Settlements.GetSettleableTripsAsync(world.PartyId);
        Assert.Equal(6_000m, settleable.Single(t => t.TripId == tripId).Balance);

        var settlementId = await SettleAllAsync(world, tripId);
        await world.Settlements.ApproveAsync(settlementId, world.UserId, null);

        var trip = await ReloadAsync(world, tripId);
        Assert.Equal(10_000m, trip.TotalApprovedReceived);
        Assert.Equal(0m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task Rejecting_leaves_every_trip_open_and_unpaid()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);

        var settlementId = await SettleAllAsync(world, tripId);
        await world.Settlements.RejectAsync(settlementId, world.UserId, "Cheque bounced");

        var trip = await ReloadAsync(world, tripId);
        Assert.Equal(TripStatus.Open, trip.Status);
        Assert.Equal(0m, trip.TotalApprovedReceived);
        Assert.Equal(10_000m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task A_settlements_lines_never_appear_as_individual_approvals()
    {
        await using var world = await TestWorld.CreateAsync();
        var bulk = await world.BookTripAsync(amount: 10_000m);
        var single = await world.BookTripAsync(amount: 2_000m);

        await SettleAllAsync(world, bulk);
        await world.AddAmountAsync(single, 2_000m);

        // The Approvals queue offers the standalone receipt and nothing else:
        // the settlement is one decision, made on the settlement itself.
        var pending = await world.Transactions.GetPendingAsync();
        Assert.Equal(single, Assert.Single(pending).TripId);

        Assert.Single(await world.Settlements.GetPendingAsync());
    }

    [Fact]
    public async Task A_settlement_line_cannot_be_approved_on_its_own()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);
        var settlementId = await SettleAllAsync(world, tripId);

        var settlement = await world.Settlements.GetAsync(settlementId);
        var lineId = settlement!.Transactions.Single().Id;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Transactions.ApproveAsync(lineId, world.UserId, null));

        Assert.Equal(TripTransactionService.BelongsToSettlementMessage, error.Message);
    }

    [Fact]
    public async Task A_settlement_can_only_be_decided_once()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);
        var settlementId = await SettleAllAsync(world, tripId);

        await world.Settlements.ApproveAsync(settlementId, world.UserId, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Settlements.ApproveAsync(settlementId, world.UserId, null));
        Assert.Equal(SettlementService.AlreadyDecidedMessage, error.Message);

        // And rejecting an approved one must not quietly reverse the money.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Settlements.RejectAsync(settlementId, world.UserId, null));
    }

    [Fact]
    public async Task A_trip_closed_while_the_settlement_waited_still_gets_its_money()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);
        var settlementId = await SettleAllAsync(world, tripId);

        // Somebody closes it by hand before the owner gets to the approval.
        await world.Trips.CloseAsync(tripId, world.UserId);
        await world.Settlements.ApproveAsync(settlementId, world.UserId, null);

        var trip = await ReloadAsync(world, tripId);
        Assert.Equal(TripStatus.Closed, trip.Status);
        Assert.Equal(10_000m, trip.TotalApprovedReceived);
    }

    [Fact]
    public async Task A_trip_belonging_to_another_party_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);

        Guid otherPartyId;
        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            var other = new Party { CompanyId = world.CompanyId, Name = "Someone Else", Phone = "9111111111" };
            db.Parties.Add(other);
            await db.SaveChangesAsync();
            otherPartyId = other.Id;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Settlements.CreateAsync(
            otherPartyId, DateTime.Today, PaymentMode.Bank, [tripId], null, world.UserId));
    }

    [Fact]
    public async Task Settling_nothing_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Settlements.CreateAsync(
            world.PartyId, DateTime.Today, PaymentMode.Bank, [], null, world.UserId));

        Assert.Equal(SettlementService.NothingSelectedMessage, error.Message);
    }

    [Fact]
    public async Task An_already_closed_trip_cannot_be_put_into_a_settlement()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);
        await world.Trips.CloseAsync(tripId, world.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SettleAllAsync(world, tripId));
    }

    [Fact]
    public async Task Extras_and_tax_are_settled_along_with_the_freight()
    {
        await using var world = await TestWorld.CreateAsync();

        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            var party = await db.Parties.FirstAsync(p => p.Id == world.PartyId);
            party.IsGstEnabled = true;
            party.GstPercentage = 5m;
            await db.SaveChangesAsync();
        }

        var tripId = await world.Trips.SaveTripAsync(new Trip
        {
            Date = DateTime.Today,
            VehicleId = world.VehicleId,
            DriverId = world.DriverId,
            PartyId = world.PartyId,
            FromCityId = world.FromCityId,
            ToCityId = world.ToCityId,
            ConsignorName = "Consignor",
            ConsigneeName = "Consignee",
            Amount = 10_000m,
            WaymentCharge = 500m,
            LoadingCharge = 300m,
            UnloadingCharge = 200m,
        });

        // The party owes the whole invoice — 11,000 before tax plus 550 GST —
        // so that is what a settlement clears, not the freight alone.
        var settleable = await world.Settlements.GetSettleableTripsAsync(world.PartyId);
        Assert.Equal(11_550m, settleable.Single(t => t.TripId == tripId).Balance);

        var settlementId = await SettleAllAsync(world, tripId);
        await world.Settlements.ApproveAsync(settlementId, world.UserId, null);

        var trip = await ReloadAsync(world, tripId);
        Assert.Equal(11_550m, trip.TotalApprovedReceived);
        Assert.Equal(0m, trip.BalanceReceivable);
    }
}
