using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>Sequential document numbers, one counter per document type. A
/// single-user desktop app with short-lived contexts needs no explicit
/// locking here — the increment is tracked in-memory and flushed by the
/// caller's own <c>SaveChangesAsync</c>.</summary>
public static class NumberService
{
    public const string Trip = "Trip";
    public const string Lr = "LR";
    public const string Bill = "Bill";
    public const string Employee = "Employee";

    private static string DefaultPrefix(string name) => name switch
    {
        Trip => "TRP",
        Lr => "LR",
        Bill => "BILL",
        Employee => "EMP",
        _ => "DOC"
    };

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
}
