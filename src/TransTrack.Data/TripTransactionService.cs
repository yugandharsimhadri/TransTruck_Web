using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class TripTransactionService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>Every pending transaction, across every trip — what the
    /// Owner-only Approvals screen lists.</summary>
    public async Task<List<TripTransaction>> GetPendingAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TripTransactions.AsNoTracking()
            .Include(t => t.Trip).ThenInclude(t => t.Vehicle)
            .Include(t => t.Trip).ThenInclude(t => t.Party)
            .Where(t => t.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task AddAsync(Guid tripId, TripTransaction transaction, Guid? enteredByUserId)
    {
        if (transaction.Amount <= 0) throw new InvalidOperationException("Enter an amount greater than zero.");

        await using var db = await factory.CreateDbContextAsync();

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

    public async Task ApproveAsync(Guid transactionId, Guid approvedByUserId, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TripTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                     ?? throw new InvalidOperationException("Transaction not found.");

        entity.ApprovalStatus = ApprovalStatus.Approved;
        entity.ApprovedByUserId = approvedByUserId;
        entity.ApprovedOn = DateTime.Now;
        entity.ApprovalRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid transactionId, Guid approvedByUserId, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.TripTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                     ?? throw new InvalidOperationException("Transaction not found.");

        entity.ApprovalStatus = ApprovalStatus.Rejected;
        entity.ApprovedByUserId = approvedByUserId;
        entity.ApprovedOn = DateTime.Now;
        entity.ApprovalRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        await db.SaveChangesAsync();
    }
}
