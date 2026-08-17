using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// The Trips report reads its figures off the same computed properties the
/// trip screen does (<c>Trip.TotalExpenses</c>, <c>TotalApprovedReceived</c>,
/// <c>BalanceReceivable</c>), and those sum whichever child rows the query
/// happened to load. Expenses and amounts are soft-deleted, so a report query
/// that loads them unfiltered silently counts money the user already removed.
/// These pin that down.
/// </summary>
public class ReportAccuracyTests
{
    private static ReportsService ReportsFor(TestWorld world) => new(world.Factory);

    [Fact]
    public async Task Trips_report_ignores_a_deleted_expense()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        await world.AddExpenseAsync(tripId, 500);
        await world.AddExpenseAsync(tripId, 300);

        var trip = await world.Trips.GetTripAsync(tripId);
        var doomed = trip!.Expenses.First(e => e.Amount == 300);
        await world.Trips.DeleteExpenseAsync(doomed.Id);

        var rows = await ReportsFor(world).GetTripsAsync(null, null, null, null);

        Assert.Equal(500m, Assert.Single(rows).TotalExpenses);
    }

    [Fact]
    public async Task Trips_report_ignores_a_deleted_amount_received()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        var keep = await world.AddAmountAsync(tripId, 4000);
        var remove = await world.AddAmountAsync(tripId, 1000);
        await world.Transactions.ApproveAsync(keep, world.UserId, null);
        await world.Transactions.ApproveAsync(remove, world.UserId, null);

        await world.Transactions.DeleteAsync(remove);

        var row = Assert.Single(await ReportsFor(world).GetTripsAsync(null, null, null, null));

        Assert.Equal(4000m, row.TotalApprovedReceived);
        Assert.Equal(6000m, row.BalanceReceivable);
    }
}
