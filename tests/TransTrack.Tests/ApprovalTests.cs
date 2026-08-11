using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>Amounts received sit Pending until the Owner rules on them, and
/// an approved amount is final — the money rules that most need a test.</summary>
public class ApprovalTests
{
    [Fact]
    public async Task A_new_amount_starts_pending_and_does_not_count_yet()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        await world.AddAmountAsync(tripId, 4000);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(ApprovalStatus.Pending, trip!.Transactions.Single().ApprovalStatus);

        // The whole point of the approval step: an unapproved amount must not
        // reduce what the party still owes.
        Assert.Equal(0m, trip.TotalApprovedReceived);
        Assert.Equal(10000m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task Approving_an_amount_counts_it_against_the_balance()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        var txnId = await world.AddAmountAsync(tripId, 4000);

        await world.Transactions.ApproveAsync(txnId, world.UserId, "looks right");

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(4000m, trip!.TotalApprovedReceived);
        Assert.Equal(6000m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task Rejecting_an_amount_keeps_it_out_of_the_balance()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        var txnId = await world.AddAmountAsync(tripId, 4000);

        await world.Transactions.RejectAsync(txnId, world.UserId, "wrong party");

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(ApprovalStatus.Rejected, trip!.Transactions.Single().ApprovalStatus);
        Assert.Equal(0m, trip.TotalApprovedReceived);
        Assert.Equal(10000m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task Pending_amounts_across_all_trips_are_listed_for_approval()
    {
        await using var world = await TestWorld.CreateAsync();
        var first = await world.BookTripAsync();
        var second = await world.BookTripAsync();
        await world.AddAmountAsync(first, 1000);
        var approved = await world.AddAmountAsync(second, 2000);
        await world.Transactions.ApproveAsync(approved, world.UserId, null);

        var pending = await world.Transactions.GetPendingAsync();

        // Only the one still awaiting a decision.
        Assert.Single(pending);
        Assert.Equal(1000m, pending[0].Amount);
    }

    // ── Negative: an approved amount is immutable ────────────────────────

    [Fact]
    public async Task Approving_an_already_approved_amount_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        var txnId = await world.AddAmountAsync(tripId, 4000);
        await world.Transactions.ApproveAsync(txnId, world.UserId, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Transactions.ApproveAsync(txnId, world.UserId, "again"));

        Assert.Equal(TripTransactionService.AlreadyApprovedMessage, error.Message);
    }

    [Fact]
    public async Task Rejecting_an_already_approved_amount_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        var txnId = await world.AddAmountAsync(tripId, 4000);
        await world.Transactions.ApproveAsync(txnId, world.UserId, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Transactions.RejectAsync(txnId, world.UserId, "changed my mind"));
    }

    [Fact]
    public async Task A_rejected_amount_can_still_be_approved_on_review()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        var txnId = await world.AddAmountAsync(tripId, 4000);
        await world.Transactions.RejectAsync(txnId, world.UserId, "no receipt");

        // Rejection is a decision, not a dead end — only approval is final.
        await world.Transactions.ApproveAsync(txnId, world.UserId, "receipt produced");

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(4000m, trip!.TotalApprovedReceived);
    }

    [Fact]
    public async Task Deleting_an_approved_amount_removes_it_from_the_balance()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);
        var txnId = await world.AddAmountAsync(tripId, 4000);
        await world.Transactions.ApproveAsync(txnId, world.UserId, null);

        await world.Transactions.DeleteAsync(txnId);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Empty(trip!.Transactions);
        Assert.Equal(10000m, trip.BalanceReceivable);
    }

    [Fact]
    public async Task A_deleted_amount_is_kept_in_the_database_for_the_audit_trail()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        var txnId = await world.AddAmountAsync(tripId, 4000);

        await world.Transactions.DeleteAsync(txnId);

        // Soft delete: gone from the app, still on disk, so the trail can say
        // who removed it.
        await using var db = await world.Factory.CreateDbContextAsync();
        var row = db.TripTransactions.IgnoreQueryFilters().Single(t => t.Id == txnId);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task An_amount_of_zero_or_less_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AddAmountAsync(tripId, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AddAmountAsync(tripId, -100));
    }

    [Fact]
    public async Task Approving_something_that_does_not_exist_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Transactions.ApproveAsync(Guid.NewGuid(), world.UserId, null));
    }
}
