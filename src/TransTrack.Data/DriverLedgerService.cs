using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class DriverLedgerService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<DriverLedgerEntry>> GetForDriverAsync(Guid driverId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.DriverLedgerEntries.AsNoTracking()
            .Where(e => e.DriverId == driverId && !e.IsDeleted)
            .OrderByDescending(e => e.Date)
            .ToListAsync();
    }

    /// <summary>Advances given, less what has been deducted against them —
    /// how much the driver still owes.</summary>
    public async Task<decimal> GetAdvanceOutstandingAsync(Guid driverId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var given = await db.DriverLedgerEntries.Where(e => e.DriverId == driverId && !e.IsDeleted
                && e.Type == DriverLedgerEntryType.AdvanceGiven).SumAsync(e => e.Amount);
        var deducted = await db.DriverLedgerEntries.Where(e => e.DriverId == driverId && !e.IsDeleted
                && e.Type == DriverLedgerEntryType.Deduction).SumAsync(e => e.Amount);
        return given - deducted;
    }

    public async Task SaveAsync(DriverLedgerEntry entry)
    {
        if (entry.DriverId == Guid.Empty) throw new InvalidOperationException("Choose a driver.");
        if (entry.Amount <= 0) throw new InvalidOperationException("Enter an amount greater than zero.");

        await using var db = await factory.CreateDbContextAsync();

        var entity = entry.Id == Guid.Empty ? null : await db.DriverLedgerEntries.FirstOrDefaultAsync(x => x.Id == entry.Id);
        var isNew = entity is null;
        entity ??= new DriverLedgerEntry();

        entity.DriverId = entry.DriverId;
        entity.Date = entry.Date;
        entity.Type = entry.Type;
        entity.Amount = entry.Amount;
        entity.ForMonth = entry.Type == DriverLedgerEntryType.SalaryPaid ? entry.ForMonth : null;
        entity.Remarks = entry.Remarks;

        if (isNew) db.DriverLedgerEntries.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.DriverLedgerEntries.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }
}
