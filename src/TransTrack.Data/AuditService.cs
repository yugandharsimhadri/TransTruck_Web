using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>Read-only access to the audit trail. Nothing here writes: entries
/// are produced automatically by <see cref="AppDbContext"/> on save, and
/// nothing in the app edits or removes them — that permanence is the point.
/// Every query is tenant-scoped by the global filter, same as everything else,
/// so one company can never read another's history.</summary>
public class AuditService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>One page of the company-wide activity feed, newest first.</summary>
    public async Task<List<AuditEntryView>> GetRecentAsync(
        DateTime? from = null, DateTime? to = null, string? entityType = null, int take = 100)
    {
        await using var db = await factory.CreateDbContextAsync();

        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (from is { } f) query = query.Where(a => a.ChangedOn >= f.Date);
        if (to is { } t) query = query.Where(a => a.ChangedOn < t.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);

        return await Project(db, query.OrderByDescending(a => a.ChangedOn).Take(Math.Clamp(take, 1, 500)));
    }

    /// <summary>Everything that has happened to one trip — the trip row itself
    /// plus its expenses and amounts, which carry the trip's id precisely so
    /// this is one indexed query rather than a join per child table.</summary>
    public async Task<List<AuditEntryView>> GetForTripAsync(Guid tripId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.AuditLogs.AsNoTracking().Where(a => a.TripId == tripId);
        return await Project(db, query.OrderByDescending(a => a.ChangedOn));
    }

    /// <summary>One record's own history — used by the per-row history view on
    /// maintenance and driver-ledger entries, which have no owning trip.</summary>
    public async Task<List<AuditEntryView>> GetForRecordAsync(string entityType, Guid entityId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId);
        return await Project(db, query.OrderByDescending(a => a.ChangedOn));
    }

    /// <summary>Resolves the actor's display name in one extra query rather
    /// than a per-row lookup, and keeps the raw user id out of the response.</summary>
    private static async Task<List<AuditEntryView>> Project(AppDbContext db, IQueryable<AuditLog> query)
    {
        var rows = await query.ToListAsync();
        if (rows.Count == 0) return [];

        var userIds = rows.Where(r => r.ChangedByUserId is not null)
            .Select(r => r.ChangedByUserId!.Value).Distinct().ToList();

        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        return rows.Select(r => new AuditEntryView(
            r.Id,
            r.EntityType,
            r.EntityId,
            r.TripId,
            r.Action.ToString(),
            r.Summary,
            r.Changes,
            // A null actor means the row predates login being switched on, or
            // was written by a background path with no signed-in user — say so
            // plainly rather than showing a blank.
            r.ChangedByUserId is { } id && names.TryGetValue(id, out var name) ? name : "System",
            r.ChangedOn)).ToList();
    }
}

/// <summary>What the API returns for an audit entry — the actor's name rather
/// than their id, and no tenant/bookkeeping columns.</summary>
public record AuditEntryView(
    Guid Id,
    string EntityType,
    Guid EntityId,
    Guid? TripId,
    string Action,
    string Summary,
    string? Changes,
    string ChangedBy,
    DateTime ChangedOn);
