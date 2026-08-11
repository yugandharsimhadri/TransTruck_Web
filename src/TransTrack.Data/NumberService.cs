using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>
/// Sequential document numbers, one counter per document type per company.
///
/// This carried a single-user assumption over from the desktop app: read the
/// counter, add one, let the caller's SaveChanges flush it. That is a race as
/// soon as two people book a trip at the same moment — both read the same
/// LastNumber, both write the same TripNo, and the unique index on
/// (CompanyId, TripNo) rejects the second with a database error rather than
/// simply giving it the next number.
///
/// The counter row itself is the lock: <see cref="NextAsync"/> now takes a
/// concurrency token on it, so a racing writer is detected rather than
/// silently duplicating, and <see cref="AllocateAsync"/> retries the whole
/// operation when that happens. Sequential numbers with no gaps matter here
/// (these are LR and bill numbers a company has to account for), which rules
/// out the usual trick of handing out ranges.
/// </summary>
public static class NumberService
{
    public const string Trip = "Trip";
    public const string Lr = "LR";
    public const string Bill = "Bill";
    public const string Employee = "Employee";

    /// <summary>How many times a contended allocation is retried before
    /// giving up. Contention is resolved in microseconds, so more than a
    /// couple of collisions means something else is wrong.</summary>
    private const int MaxAttempts = 5;

    private static string DefaultPrefix(string name) => name switch
    {
        Trip => "TRP",
        Lr => "LR",
        Bill => "BILL",
        Employee => "EMP",
        _ => "DOC"
    };

    /// <summary>Reserves the next number, leaving the incremented counter for
    /// the caller's own SaveChangesAsync to flush in the same transaction as
    /// whatever it is numbering — so a failed save never burns a number.</summary>
    public static async Task<string> NextAsync(AppDbContext db, string name, CancellationToken ct = default)
    {
        var counter = await db.Counters.FirstOrDefaultAsync(c => c.Name == name, ct);

        if (counter is null)
        {
            counter = new Counter { Name = name, Prefix = DefaultPrefix(name), LastNumber = 0 };
            db.Counters.Add(counter);
        }

        counter.LastNumber++;
        return $"{counter.Prefix}{counter.LastNumber:D5}";
    }

    /// <summary>
    /// Runs <paramref name="work"/> — which is expected to call
    /// <see cref="NextAsync"/> and save — retrying from a clean context if a
    /// concurrent writer took the number first. Use this for anything that
    /// allocates a number outside <c>TripService.SaveTripAsync</c>'s own flow.
    /// </summary>
    public static async Task<T> AllocateAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        Func<AppDbContext, Task<T>> work,
        CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            try
            {
                return await work(db);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Someone else committed the same number between our read and
                // our write. Nothing to reconcile — just start over and take
                // the next one.
                AppLog.Info($"Document number contended (attempt {attempt}); retrying.");
            }
        }
    }
}
