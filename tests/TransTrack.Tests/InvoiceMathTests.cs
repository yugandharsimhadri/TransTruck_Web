using Microsoft.EntityFrameworkCore;
using TransTrack.Data;
using TransTrack.Core;

namespace TransTrack.Tests;

/// <summary>
/// The trip invoice: freight → extras → GST → what the party owes.
///
/// This is the part of the September 2026 batch most worth pinning down,
/// because <c>Amount</c> deliberately stayed the freight alone while
/// <c>BalanceReceivable</c> moved onto the grand total. Those two facts have
/// to hold together, and every trip booked before extras existed has to keep
/// meaning exactly what it did.
/// </summary>
public class InvoiceMathTests
{
    private static Trip FreightOnly(decimal amount = 10000) => new() { Amount = amount };

    [Fact]
    public void A_trip_with_no_extras_and_no_gst_bills_exactly_its_freight()
    {
        var trip = FreightOnly(10000);

        // The backward-compatibility guarantee: every trip in the database
        // before this feature existed looks exactly like this one, and none
        // of its figures may move.
        Assert.Equal(0m, trip.TotalExtras);
        Assert.Equal(10000m, trip.TotalBeforeTax);
        Assert.Equal(0m, trip.GstAmount);
        Assert.Equal(10000m, trip.GrandTotal);
        Assert.Equal(10000m, trip.BalanceReceivable);
        Assert.Equal(10000m, trip.CompanyRevenue);
    }

    [Fact]
    public void Extras_add_on_top_of_the_freight_without_changing_it()
    {
        var trip = FreightOnly(10000);
        trip.WaymentCharge = 500;
        trip.LoadingCharge = 300;
        trip.UnloadingCharge = 200;

        Assert.Equal(10000m, trip.Amount);          // freight itself untouched
        Assert.Equal(1000m, trip.TotalExtras);
        Assert.Equal(11000m, trip.TotalBeforeTax);
        Assert.Equal(11000m, trip.GrandTotal);
    }

    [Fact]
    public void Gst_applies_to_the_freight_and_the_extras_together()
    {
        var trip = FreightOnly(10000);
        trip.WaymentCharge = 500;
        trip.LoadingCharge = 300;
        trip.UnloadingCharge = 200;
        trip.GstPercentage = 5m;

        // 5% of 11,000 — the extras are taxed alongside the freight, not
        // separately and not exempt.
        Assert.Equal(550m, trip.GstAmount);
        Assert.Equal(11550m, trip.GrandTotal);
    }

    /// <summary>The invariant the whole invoice rests on: the parts always
    /// reconstruct the total exactly.</summary>
    [Theory]
    [InlineData(10000, 500, 300, 200, 5)]
    [InlineData(7333, 111, 222, 333, 12)]
    [InlineData(1, 0, 0, 0, 18)]
    [InlineData(999999, 1, 1, 1, 0.5)]
    public void The_parts_always_sum_back_to_the_grand_total(
        decimal freight, decimal wayment, decimal loading, decimal unloading, decimal gstPercent)
    {
        var trip = new Trip
        {
            Amount = freight,
            WaymentCharge = wayment,
            LoadingCharge = loading,
            UnloadingCharge = unloading,
            GstPercentage = gstPercent,
        };

        Assert.Equal(wayment + loading + unloading, trip.TotalExtras);
        Assert.Equal(freight + trip.TotalExtras, trip.TotalBeforeTax);
        Assert.Equal(trip.TotalBeforeTax + trip.GstAmount, trip.GrandTotal);
    }

    [Fact]
    public void Gst_is_rounded_to_whole_rupees()
    {
        // 5% of 10,001 is 500.05 — a bill carrying paise on the tax line and
        // nowhere else reads like a mistake.
        var trip = FreightOnly(10001);
        trip.GstPercentage = 5m;

        Assert.Equal(500m, trip.GstAmount);
        Assert.Equal(10501m, trip.GrandTotal);
    }

    [Fact]
    public void A_zero_or_absent_gst_rate_adds_nothing()
    {
        var withNull = FreightOnly(10000);
        var withZero = FreightOnly(10000);
        withZero.GstPercentage = 0m;

        Assert.Equal(0m, withNull.GstAmount);
        Assert.Equal(0m, withZero.GstAmount);
        Assert.Equal(withNull.GrandTotal, withZero.GrandTotal);
    }

    /// <summary>GST is collected for the government, not earned — so it must
    /// never reach revenue or profit, even though the party does owe it.</summary>
    [Fact]
    public void Gst_is_owed_by_the_party_but_is_never_company_revenue()
    {
        var trip = FreightOnly(10000);
        trip.WaymentCharge = 1000;
        trip.GstPercentage = 5m;

        Assert.Equal(550m, trip.GstAmount);
        Assert.Equal(11550m, trip.GrandTotal);       // the party owes the tax
        Assert.Equal(11000m, trip.CompanyRevenue);   // the company did not earn it
        Assert.Equal(11000m, trip.NetAfterExpenses); // and it is not profit
    }

    [Fact]
    public void What_the_party_still_owes_counts_the_extras_and_the_tax()
    {
        var trip = FreightOnly(10000);
        trip.WaymentCharge = 1000;
        trip.GstPercentage = 5m;
        trip.Transactions =
        [
            new TripTransaction { Amount = 5000, ApprovalStatus = ApprovalStatus.Approved },
            // Pending money must not move the balance, same rule as before.
            new TripTransaction { Amount = 2000, ApprovalStatus = ApprovalStatus.Pending },
        ];

        Assert.Equal(5000m, trip.TotalApprovedReceived);
        Assert.Equal(6550m, trip.BalanceReceivable); // 11,550 − 5,000
    }

    /// <summary>An other-owner vehicle's freight is collected on that owner's
    /// behalf, so only the commission is the company's — the extras and tax
    /// on that trip don't change who the freight belongs to.</summary>
    [Fact]
    public void An_other_owner_trip_still_earns_only_its_commission()
    {
        var trip = new Trip
        {
            Amount = 10000,
            WaymentCharge = 1000,
            GstPercentage = 5m,
            CommissionAmount = 800,
            Vehicle = new Vehicle { Ownership = VehicleOwnership.Other },
        };

        Assert.False(trip.IsOwnAccounting);
        Assert.Equal(800m, trip.CompanyRevenue);
        Assert.Equal(11550m, trip.GrandTotal); // the party is still billed in full
    }
}

/// <summary>
/// The trips list projects its own lighter row rather than loading whole trips,
/// so it computes the money a second time. These pin that row to the trip it
/// opens: a list saying one figure and the trip behind it saying another is the
/// bug this batch's extras and GST made possible.
/// </summary>
public class TripListRowTests
{
    private static async Task<Trip> BookWithExtrasAsync(TestWorld world)
    {
        await using var db = await world.Factory.CreateDbContextAsync();
        var party = await db.Parties.FirstAsync(p => p.Id == world.PartyId);
        party.IsGstEnabled = true;
        party.GstPercentage = 5m;
        await db.SaveChangesAsync();

        var id = await world.Trips.SaveTripAsync(new Trip
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

        return (await world.Trips.GetTripAsync(id))!;
    }

    [Fact]
    public async Task The_list_row_agrees_with_the_trip_it_opens()
    {
        await using var world = await TestWorld.CreateAsync();
        var trip = await BookWithExtrasAsync(world);

        var row = (await world.Trips.GetTripListAsync()).Items.Single(r => r.Id == trip.Id);

        Assert.Equal(trip.TotalExtras, row.TotalExtras);
        Assert.Equal(trip.TotalBeforeTax, row.TotalBeforeTax);
        Assert.Equal(trip.GstAmount, row.GstAmount);
        Assert.Equal(trip.GrandTotal, row.GrandTotal);
        Assert.Equal(trip.BalanceReceivable, row.BalanceReceivable);
    }

    [Fact]
    public async Task The_row_carries_the_whole_invoice_not_the_freight()
    {
        await using var world = await TestWorld.CreateAsync();
        var trip = await BookWithExtrasAsync(world);

        var row = (await world.Trips.GetTripListAsync()).Items.Single(r => r.Id == trip.Id);

        Assert.Equal(10_000m, row.Amount);
        Assert.Equal(11_550m, row.GrandTotal);
        Assert.Equal(11_550m, row.BalanceReceivable);
    }

    [Fact]
    public async Task A_trip_without_extras_is_worth_its_freight()
    {
        await using var world = await TestWorld.CreateAsync();
        var id = await world.BookTripAsync(amount: 7_000m);

        var row = (await world.Trips.GetTripListAsync()).Items.Single(r => r.Id == id);

        Assert.Equal(0m, row.TotalExtras);
        Assert.Equal(0m, row.GstAmount);
        Assert.Equal(7_000m, row.GrandTotal);
    }
}
