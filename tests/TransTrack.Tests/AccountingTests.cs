using TransTrack.Core;

namespace TransTrack.Tests;

/// <summary>
/// The commission-only rule, which is the subtlest thing in the domain: when
/// the company moves a load on somebody else's lorry, the freight is
/// collected on that owner's behalf and passed on. Only the commission is the
/// company's revenue, and the running costs are that owner's, not the
/// company's — so neither the freight nor the expenses belong in the
/// company's books.
/// </summary>
public class AccountingTests
{
    [Fact]
    public async Task Own_vehicle_the_whole_freight_is_company_revenue()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 20000, vehicleId: world.VehicleId);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.True(trip!.IsOwnAccounting);
        Assert.Equal(20000m, trip.CompanyRevenue);
    }

    [Fact]
    public async Task Own_vehicle_expenses_are_the_companys_own()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 20000, vehicleId: world.VehicleId);
        await world.AddExpenseAsync(tripId, 3000);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(3000m, trip!.TotalExpenses);
        Assert.Equal(3000m, trip.CompanyExpenses);
    }

    [Fact]
    public async Task Other_owners_vehicle_only_the_commission_is_company_revenue()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(
            amount: 20000, vehicleId: world.OtherOwnerVehicleId, commission: 2000);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.False(trip!.IsOwnAccounting);
        Assert.Equal(20000m, trip.Amount);       // still billed in full to the party
        Assert.Equal(2000m, trip.CompanyRevenue); // but only this is ours
    }

    [Fact]
    public async Task Other_owners_vehicle_running_costs_are_not_the_companys()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(
            amount: 20000, vehicleId: world.OtherOwnerVehicleId, commission: 2000);
        await world.AddExpenseAsync(tripId, 3000);

        var trip = await world.Trips.GetTripAsync(tripId);

        // Recorded against the trip for visibility...
        Assert.Equal(3000m, trip!.TotalExpenses);
        // ...but they are the vehicle owner's costs, not ours.
        Assert.Equal(0m, trip.CompanyExpenses);
    }

    [Fact]
    public async Task Commission_is_ignored_on_an_owned_vehicle()
    {
        await using var world = await TestWorld.CreateAsync();

        // Even if a commission is sent for an owned vehicle, it must not stick:
        // there is nobody to pay it to.
        var tripId = await world.BookTripAsync(
            amount: 20000, vehicleId: world.VehicleId, commission: 5000);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Null(trip!.CommissionAmount);
        Assert.Equal(20000m, trip.CompanyRevenue);
    }

    [Fact]
    public async Task Net_after_expenses_subtracts_both_costs_and_commission()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(
            amount: 20000, vehicleId: world.OtherOwnerVehicleId, commission: 2000);
        await world.AddExpenseAsync(tripId, 3000);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(20000m - 3000m - 2000m, trip!.NetAfterExpenses);
    }

    // ── Derived balances ─────────────────────────────────────────────────

    [Fact]
    public async Task Balance_receivable_counts_only_approved_money()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        var approved = await world.AddAmountAsync(tripId, 3000);
        await world.Transactions.ApproveAsync(approved, world.UserId, null);
        await world.AddAmountAsync(tripId, 5000);   // left pending

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(3000m, trip!.TotalApprovedReceived);
        Assert.Equal(7000m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task A_deleted_expense_stops_counting_towards_the_total()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.AddExpenseAsync(tripId, 500);
        await world.AddExpenseAsync(tripId, 300);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(800m, trip!.TotalExpenses);

        // Pick the row by its amount rather than by position — an earlier
        // version of this test took Expenses.First() and failed intermittently
        // because the include had no ORDER BY (since fixed).
        var toDelete = trip.Expenses.Single(e => e.Amount == 500m);
        await world.Trips.DeleteExpenseAsync(toDelete.Id);

        // Soft-deleted rows survive for the audit trail, so the filtered
        // include is what keeps them out of the arithmetic.
        var after = await world.Trips.GetTripAsync(tripId);
        Assert.Single(after!.Expenses);
        Assert.Equal(300m, after.TotalExpenses);
    }

    [Fact]
    public async Task Expenses_come_back_in_a_stable_order()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        await world.Trips.AddExpenseAsync(tripId, new TripExpense
        {
            TripId = tripId, CompanyId = world.CompanyId,
            ExpenseCategoryId = world.ExpenseCategoryId,
            Date = DateTime.Today.AddDays(-1), Amount = 100
        });
        await world.Trips.AddExpenseAsync(tripId, new TripExpense
        {
            TripId = tripId, CompanyId = world.CompanyId,
            ExpenseCategoryId = world.ExpenseCategoryId,
            Date = DateTime.Today.AddDays(-3), Amount = 200
        });
        await world.Trips.AddExpenseAsync(tripId, new TripExpense
        {
            TripId = tripId, CompanyId = world.CompanyId,
            ExpenseCategoryId = world.ExpenseCategoryId,
            Date = DateTime.Today.AddDays(-2), Amount = 300
        });

        var trip = await world.Trips.GetTripAsync(tripId);

        // Oldest first, so the list reads as a running log and never
        // reshuffles between loads.
        Assert.Equal([200m, 300m, 100m], trip!.Expenses.Select(e => e.Amount));
    }

    [Fact]
    public async Task The_trips_list_totals_agree_with_the_detail_view()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        await world.AddExpenseAsync(tripId, 1200);
        var txnId = await world.AddAmountAsync(tripId, 4000);
        await world.Transactions.ApproveAsync(txnId, world.UserId, null);

        var detail = await world.Trips.GetTripAsync(tripId);
        var row = (await world.Trips.GetTripListAsync()).Items.Single(t => t.Id == tripId);

        // The list is a separate SQL projection from the detail's object
        // graph; if the two ever disagree the list is quietly lying.
        Assert.Equal(detail!.TotalExpenses, row.TotalExpenses);
        Assert.Equal(detail.TotalApprovedReceived, row.TotalApprovedReceived);
        Assert.Equal(detail.BalanceReceivable, row.BalanceReceivable);
    }

    [Fact]
    public async Task The_trips_list_ignores_deleted_children_too()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        await world.AddExpenseAsync(tripId, 1000);
        var txnId = await world.AddAmountAsync(tripId, 4000);
        await world.Transactions.ApproveAsync(txnId, world.UserId, null);

        var trip = await world.Trips.GetTripAsync(tripId);
        await world.Trips.DeleteExpenseAsync(trip!.Expenses.First().Id);
        await world.Transactions.DeleteAsync(txnId);

        var row = (await world.Trips.GetTripListAsync()).Items.Single(t => t.Id == tripId);

        Assert.Equal(0m, row.TotalExpenses);
        Assert.Equal(0m, row.TotalApprovedReceived);
        Assert.Equal(10000m, row.BalanceReceivable);
    }
}
