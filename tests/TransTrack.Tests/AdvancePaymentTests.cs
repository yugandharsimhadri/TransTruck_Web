using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// An amount received against a trip is now tagged Advance or Payment. The
/// one rule this whole feature rests on is that the two are a true split of
/// what the trip has received — nothing counted twice, nothing dropped —
/// and that every row recorded before this field existed reads as a Payment,
/// not an Advance.
/// </summary>
public class AdvancePaymentTests
{
    [Fact]
    public async Task Advance_and_payment_always_sum_to_the_approved_total()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 20000);

        var advance = await world.AddAmountAsync(tripId, 5000, ReceiptType.Advance);
        var payment = await world.AddAmountAsync(tripId, 3000, ReceiptType.Payment);
        await world.Transactions.ApproveAsync(advance, world.UserId, null);
        await world.Transactions.ApproveAsync(payment, world.UserId, null);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(5000m, trip!.TotalAdvanceReceived);
        Assert.Equal(3000m, trip.TotalPaymentReceived);
        Assert.Equal(trip.TotalApprovedReceived, trip.TotalAdvanceReceived + trip.TotalPaymentReceived);
        Assert.Equal(12000m, trip.BalanceReceivable);
    }

    /// <summary>A row saved without specifying ReceiptType — exactly what
    /// every row recorded before this field existed looks like — must land
    /// as Payment, matching the migration's backfill of existing data.</summary>
    [Fact]
    public async Task An_amount_saved_without_a_receipt_type_defaults_to_payment()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        var id = await world.AddAmountAsync(tripId, 4000); // no ReceiptType specified
        await world.Transactions.ApproveAsync(id, world.UserId, null);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(4000m, trip!.TotalPaymentReceived);
        Assert.Equal(0m, trip.TotalAdvanceReceived);
    }

    /// <summary>Only Approved rows count toward either total — a Pending
    /// advance must not show up as money the trip has actually received,
    /// same rule TotalApprovedReceived already follows.</summary>
    [Fact]
    public async Task A_pending_advance_does_not_count_yet()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        await world.AddAmountAsync(tripId, 5000, ReceiptType.Advance);

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(0m, trip!.TotalAdvanceReceived);
        Assert.Equal(10000m, trip.BalanceReceivable);
    }

    /// <summary>The Ledger report is the other place this has to show up —
    /// each income row carries its ReceiptType, and the human-readable Detail
    /// string names it, so the report and its exports don't need a caller to
    /// cross-reference the raw enum separately.</summary>
    [Fact]
    public async Task Ledger_report_carries_the_receipt_type_on_each_income_row()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 20000);

        await world.AddAmountAsync(tripId, 5000, ReceiptType.Advance);
        await world.AddAmountAsync(tripId, 3000, ReceiptType.Payment);

        var rows = await new ReportsService(world.Factory).GetLedgerAsync(null, null, null, null);

        var advanceRow = Assert.Single(rows, r => r.Amount == 5000m);
        var paymentRow = Assert.Single(rows, r => r.Amount == 3000m);

        Assert.Equal(ReceiptType.Advance, advanceRow.ReceiptType);
        Assert.Contains("Advance", advanceRow.Detail);
        Assert.Equal(ReceiptType.Payment, paymentRow.ReceiptType);
        Assert.Contains("Payment", paymentRow.Detail);
    }

    /// <summary>An Expense row has no receipt type at all — it is not an
    /// amount received, so the field stays null rather than being coerced
    /// into either bucket.</summary>
    [Fact]
    public async Task Ledger_report_leaves_receipt_type_null_on_expense_rows()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        await world.AddExpenseAsync(tripId, 500);

        var rows = await new ReportsService(world.Factory).GetLedgerAsync(null, null, null, null);

        var expenseRow = Assert.Single(rows, r => r.Kind == "Expense");
        Assert.Null(expenseRow.ReceiptType);
    }
}
