using TransTrack.Core;
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

    /// <summary>The ledger combines two source tables (expenses, amounts
    /// received) into one in-memory list, so the row-count guard has to look
    /// at both together — a report that individually stayed under the limit
    /// on each table could still assemble twice MaxRows in memory if only one
    /// side were counted.</summary>
    [Fact]
    public async Task Ledger_report_refuses_when_the_combined_row_count_is_too_large()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 1_000_000);

        // Split across both tables rather than piled into one, specifically
        // to prove the guard sums them rather than checking either alone.
        var expenseRows = ReportsService.MaxRows / 2;
        var incomeRows = ReportsService.MaxRows / 2 + 2; // combined total: MaxRows + 1

        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            var baseDate = new DateTime(2020, 1, 1);

            db.TripExpenses.AddRange(Enumerable.Range(0, expenseRows).Select(i => new TripExpense
            {
                CompanyId = world.CompanyId,
                TripId = tripId,
                ExpenseCategoryId = world.ExpenseCategoryId,
                Date = baseDate.AddDays(i),
                Amount = 1,
            }));

            db.TripTransactions.AddRange(Enumerable.Range(0, incomeRows).Select(i => new TripTransaction
            {
                CompanyId = world.CompanyId,
                TripId = tripId,
                Date = baseDate.AddDays(i),
                Amount = 1,
                ApprovalStatus = ApprovalStatus.Pending,
            }));

            await db.SaveChangesAsync();
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReportsFor(world).GetLedgerAsync(null, null, null, null));

        Assert.Contains("ledger lines", error.Message);
    }
}
