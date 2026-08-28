using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class TripTransactionService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>Capped, not refused: unlike a report export, an Owner facing
    /// a stalled approvals queue has no date range to narrow — a flat "too
    /// many, try again" would strand them, unable to see or clear any of it.
    /// Set far above what an attended queue ever reaches (pending items leave
    /// this list as they're approved or rejected, so a normal backlog is
    /// self-limiting); this exists purely to stop an unattended queue from
    /// growing without bound rather than to shape everyday use.</summary>
    public const int MaxPending = 1000;

    /// <summary>Every pending transaction, across every trip — what the
    /// Owner-only Approvals screen lists. Oldest first, so the longest-waiting
    /// amount is always the one at the top of an over-the-cap queue.</summary>
    public async Task<List<TripTransaction>> GetPendingAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TripTransactions.AsNoTracking()
            .Include(t => t.Trip).ThenInclude(t => t.Vehicle)
            .Include(t => t.Trip).ThenInclude(t => t.Party)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Pending && !t.IsDeleted)
            .OrderBy(t => t.Date)
            .Take(MaxPending)
            .ToListAsync();
    }

    public async Task AddAsync(Guid tripId, TripTransaction transaction, Guid? enteredByUserId)
    {
        if (transaction.Amount <= 0) throw new InvalidOperationException("Enter an amount greater than zero.");

        await using var db = await factory.CreateDbContextAsync();

        // Same rule as trip expenses: a closed trip is settled, so nothing
        // new gets attached to it until someone deliberately reopens it.
        var status = await db.Trips.Where(t => t.Id == tripId)
            .Select(t => (TripStatus?)t.Status).FirstOrDefaultAsync();
        if (status is null) throw new InvalidOperationException("Trip not found.");
        if (status == TripStatus.Closed) throw new InvalidOperationException(TripService.ClosedTripMessage);

        transaction.Id = Guid.NewGuid();
        transaction.TripId = tripId;
        transaction.EnteredByUserId = enteredByUserId;
        transaction.ApprovalStatus = ApprovalStatus.Pending;
        transaction.ApprovedByUserId = null;
        transaction.ApprovedOn = null;
        transaction.ApprovalRemarks = null;

        db.TripTransactions.Add(transaction);
        await db.SaveChangesAsync();
    }

    /// <summary>Approving is a one-way door: an approved amount is a settled
    /// financial record, so it can never be re-approved, flipped to rejected,
    /// or edited afterwards. The only way back is the Owner deleting it
    /// outright (see <see cref="DeleteAsync"/>), which leaves an audit trail
    /// rather than quietly rewriting history.</summary>
    public const string AlreadyApprovedMessage =
        "This amount has already been approved and can no longer be changed. The owner can delete it if it was wrong.";

    private static void EnsureNotApproved(TripTransaction entity)
    {
        if (entity.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException(AlreadyApprovedMessage);
    }

    public async Task ApproveAsync(Guid transactionId, Guid approvedByUserId, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TripTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && !t.IsDeleted)
                     ?? throw new InvalidOperationException("Transaction not found.");

        EnsureNotApproved(entity);

        entity.ApprovalStatus = ApprovalStatus.Approved;
        entity.ApprovedByUserId = approvedByUserId;
        entity.ApprovedOn = DateTime.Now;
        entity.ApprovalRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid transactionId, Guid approvedByUserId, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TripTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && !t.IsDeleted)
                     ?? throw new InvalidOperationException("Transaction not found.");

        EnsureNotApproved(entity);

        entity.ApprovalStatus = ApprovalStatus.Rejected;
        entity.ApprovedByUserId = approvedByUserId;
        entity.ApprovedOn = DateTime.Now;
        entity.ApprovalRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        await db.SaveChangesAsync();
    }

    /// <summary>The Owner's escape hatch for an amount that was entered or
    /// approved in error — the only way an approved amount can leave a trip's
    /// books. Soft-deletes so the row, and the audit trail explaining who
    /// removed it, both survive. Owner-only is enforced by the endpoint's
    /// authorization policy, not here, matching how approve/reject work.</summary>
    public async Task DeleteAsync(Guid transactionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TripTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && !t.IsDeleted);
        if (entity is null) return;

        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }
}
