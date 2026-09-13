using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.PgMigrate;

/// <summary>
/// One-shot copy of every row from the SQLite database this app has been
/// running on into a fresh PostgreSQL database, for the postgres-migration
/// branch. Not part of the running application and not meant to be run more
/// than once against a given Postgres database — it refuses to run if the
/// target already has any companies, so a second run can't double every row.
///
/// Usage:
///   dotnet run --project tools/TransTrack.PgMigrate -- [--sqlite PATH] [--pg "CONNECTION STRING"] [--yes]
///
/// Both PATH and the connection string default to the same environment
/// variables the application itself reads (TRANSTRUCKWEB_DB and
/// TRANSTRUCKWEB_PG_CONNECTION), so in the common case neither flag is
/// needed. --yes skips the confirmation prompt, for scripted runs.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var stopwatch = Stopwatch.StartNew();
        var sqlitePath = ArgOrEnv(args, "--sqlite", DbBootstrapper.PathOverrideVariable) ?? DbBootstrapper.DatabasePath;
        var pgConnection = ArgOrEnv(args, "--pg", DbBootstrapper.PgConnectionOverrideVariable);
        var skipConfirm = args.Contains("--yes");

        if (string.IsNullOrWhiteSpace(pgConnection))
        {
            Console.Error.WriteLine(
                $"No target given. Pass --pg \"CONNECTION STRING\" or set {DbBootstrapper.PgConnectionOverrideVariable}.");
            return 1;
        }

        if (!File.Exists(sqlitePath))
        {
            Console.Error.WriteLine($"No SQLite database found at {sqlitePath}.");
            return 1;
        }

        Console.WriteLine($"Source (SQLite): {sqlitePath}");
        Console.WriteLine($"Target (Postgres): {RedactPassword(pgConnection)}");

        // Never open the live file directly: WAL mode keeps recent writes in
        // the -wal sidecar file, so copying only the .db would silently miss
        // them. Copying all three to a scratch folder and reading from there
        // is also what keeps this tool from ever holding a lock on the
        // database the running API is using.
        var snapshotDir = Path.Combine(Path.GetTempPath(), $"transtrack-pgmigrate-{DateTime.UtcNow:yyyyMMddHHmmss}");
        Directory.CreateDirectory(snapshotDir);
        var snapshotPath = Path.Combine(snapshotDir, "source.db");

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var src = sqlitePath + suffix;
            if (File.Exists(src)) File.Copy(src, snapshotPath + suffix, overwrite: true);
        }

        Console.WriteLine($"Snapshotted source database to {snapshotDir}.");

        var schemaFixes = await EnsureSchemaCurrentAsync(snapshotPath);

        await using var source = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={snapshotPath}").Options);

        await using var target = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(pgConnection).Options)
        {
            // Preserve the real historical CreatedAt/UpdatedAt values instead
            // of stamping "now", and don't write an AuditLog row for every
            // single copied record — this is a one-time copy, not user activity.
            BulkCopyMode = true,
        };

        // Checked across every table, not just Companies: CompanyId is a
        // plain column with no enforced foreign key back to Companies (the
        // tenant filter is application-level, not a database constraint), so
        // clearing Companies alone leaves States, Cities and every other
        // table untouched — a prior partial reset that looks empty at a
        // glance but collides on the first insert.
        var existingRows =
            await target.Companies.IgnoreQueryFilters().CountAsync() +
            await target.States.IgnoreQueryFilters().CountAsync() +
            await target.Cities.IgnoreQueryFilters().CountAsync() +
            await target.Owners.IgnoreQueryFilters().CountAsync() +
            await target.Parties.IgnoreQueryFilters().CountAsync() +
            await target.Drivers.IgnoreQueryFilters().CountAsync() +
            await target.Vehicles.IgnoreQueryFilters().CountAsync() +
            await target.Users.IgnoreQueryFilters().CountAsync() +
            await target.Trips.IgnoreQueryFilters().CountAsync();

        if (existingRows > 0)
        {
            Console.Error.WriteLine(
                $"The target database already has {existingRows} row(s) across its tables. " +
                "Refusing to run again against a non-empty database — this tool has no way to tell which rows " +
                "were already copied and would duplicate everything. Truncate every table (not just Companies — " +
                "CompanyId is not an enforced foreign key, so a Companies-only truncate leaves the rest behind) " +
                "or point this at a freshly-migrated, empty database instead.");
            return 1;
        }

        // FK-safe order: every entity here comes after everything it
        // references, so foreign keys always resolve on insert. Derived from
        // every HasOne/HasForeignKey call in AppDbContext.OnModelCreating.
        var steps = new List<(string Table, Func<Task<int>> Run)>
        {
            ("Company", () => CopyAsync(source, target, s => s.Companies, t => t.Companies)),
            ("State", () => CopyAsync(source, target, s => s.States, t => t.States)),
            ("City", () => CopyAsync(source, target, s => s.Cities, t => t.Cities)),
            ("Owner", () => CopyAsync(source, target, s => s.Owners, t => t.Owners)),
            ("Party", () => CopyAsync(source, target, s => s.Parties, t => t.Parties)),
            ("Driver", () => CopyAsync(source, target, s => s.Drivers, t => t.Drivers)),
            ("Vehicle", () => CopyAsync(source, target, s => s.Vehicles, t => t.Vehicles)),
            ("StoredDocument", () => CopyAsync(source, target, s => s.Documents, t => t.Documents)),
            ("ExpenseCategory", () => CopyAsync(source, target, s => s.ExpenseCategories, t => t.ExpenseCategories)),
            ("MaintenanceCategory", () => CopyAsync(source, target, s => s.MaintenanceCategories, t => t.MaintenanceCategories)),
            ("Counter", () => CopyAsync(source, target, s => s.Counters, t => t.Counters)),
            ("User", () => CopyAsync(source, target, s => s.Users, t => t.Users)),
            ("Trip", () => CopyAsync(source, target, s => s.Trips, t => t.Trips)),
            ("TripExpense", () => CopyAsync(source, target, s => s.TripExpenses, t => t.TripExpenses)),
            // Settlements before TripTransactions: a transaction can carry a
            // SettlementId, so the settlement it points at must exist first.
            ("Settlement", () => CopyAsync(source, target, s => s.Settlements, t => t.Settlements)),
            ("TripTransaction", () => CopyAsync(source, target, s => s.TripTransactions, t => t.TripTransactions)),
            ("VehicleMaintenance", () => CopyAsync(source, target, s => s.VehicleMaintenances, t => t.VehicleMaintenances)),
            ("VehicleExpenseSchedule", () => CopyAsync(source, target, s => s.VehicleExpenseSchedules, t => t.VehicleExpenseSchedules)),
            ("VehicleExpense", () => CopyAsync(source, target, s => s.VehicleExpenses, t => t.VehicleExpenses)),
            ("DriverLedgerEntry", () => CopyAsync(source, target, s => s.DriverLedgerEntries, t => t.DriverLedgerEntries)),
            ("AuditLog", () => CopyAsync(source, target, s => s.AuditLogs, t => t.AuditLogs)),
        };

        if (!skipConfirm)
        {
            Console.Write("This will copy every row above into the target database. Continue? [y/N] ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return 1;
            }
        }

        var results = new List<(string Table, int Rows)>();

        await using var transaction = await target.Database.BeginTransactionAsync();
        try
        {
            foreach (var (table, run) in steps)
                results.Add((table, await run()));

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            var failedAt = steps[results.Count].Table;
            PrintSummary(sqlitePath, pgConnection, schemaFixes, results, stopwatch.Elapsed,
                error: $"Failed on \"{failedAt}\" — nothing was written, the whole transaction rolled back.\n" +
                       FormatExceptionChain(ex));
            return 1;
        }

        PrintSummary(sqlitePath, pgConnection, schemaFixes, results, stopwatch.Elapsed, error: null);
        return 0;
    }

    /// <summary>
    /// One block at the end covering everything that matters: what ran,
    /// what it found and fixed along the way, and — on failure — exactly
    /// where it stopped and why, without a raw .NET stack trace standing in
    /// for an answer.
    /// </summary>
    private static void PrintSummary(
        string sqlitePath, string pgConnection, List<string> schemaFixes,
        List<(string Table, int Rows)> results, TimeSpan elapsed, string? error)
    {
        Console.WriteLine();
        Console.WriteLine("=== Migration summary ===");
        Console.WriteLine($"Source:   {sqlitePath}");
        Console.WriteLine($"Target:   {RedactPassword(pgConnection)}");
        Console.WriteLine($"Duration: {elapsed.TotalSeconds:F1}s");

        Console.WriteLine();
        Console.WriteLine(schemaFixes.Count == 0
            ? "Schema: source already current, no fixes needed."
            : $"Schema fixes applied ({schemaFixes.Count}):");
        foreach (var fix in schemaFixes) Console.WriteLine($"  - {fix}");

        Console.WriteLine();
        if (results.Count == 0)
        {
            Console.WriteLine("Tables copied: none.");
        }
        else
        {
            Console.WriteLine($"Tables copied ({results.Count} of {results.Count + (error is null ? 0 : 1)} attempted):");
            foreach (var (table, rows) in results)
                Console.WriteLine($"  {table,-24} {rows,6} row{(rows == 1 ? "" : "s")}");
            Console.WriteLine($"  {"TOTAL",-24} {results.Sum(r => r.Rows),6}");
        }

        Console.WriteLine();
        if (error is null)
        {
            Console.WriteLine("Result: SUCCESS — every table copied, transaction committed.");
        }
        else
        {
            Console.WriteLine("Result: FAILED");
            Console.WriteLine(error);
        }
    }

    /// <summary>Message-only, innermost-first — the .NET stack trace is
    /// still there in the raw exception if someone needs it, but the summary
    /// is for "what happened," not "where in EF Core it happened."</summary>
    private static string FormatExceptionChain(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(Environment.NewLine, messages.Select((m, i) => new string(' ', i * 2) + "- " + m));
    }

    /// <summary>
    /// This branch replaced the SQLite migration history with a single fresh
    /// Postgres InitialCreate, so there is no longer a set of SQLite
    /// migrations here to bring a source database that predates the current
    /// model up to date. In practice the source this tool was developed
    /// against was two migrations behind (BulkSettlement and PlaceActiveFlag,
    /// both 2026-09-06) — reproduced here directly from those migrations'
    /// own Up() methods, and guarded so this is a no-op against a source
    /// database that already has both.
    /// </summary>
    private static async Task<List<string>> EnsureSchemaCurrentAsync(string snapshotPath)
    {
        var fixesApplied = new List<string>();

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={snapshotPath}");
        await connection.OpenAsync();

        async Task<bool> TableExistsAsync(string table)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            cmd.Parameters.AddWithValue("$name", table);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        }

        async Task<bool> ColumnExistsAsync(string table, string column)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $col";
            cmd.Parameters.AddWithValue("$col", column);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        }

        async Task ExecAsync(string sql)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        if (!await TableExistsAsync("Settlements"))
        {
            const string fix = "Source predates BulkSettlement (2026-09-06): added Settlements table and TripTransactions.SettlementId.";
            Console.WriteLine(fix);
            fixesApplied.Add(fix);

            await ExecAsync("""
                CREATE TABLE "Settlements" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Settlements" PRIMARY KEY,
                    "CompanyId" TEXT NOT NULL,
                    "PartyId" TEXT NOT NULL,
                    "Date" TEXT NOT NULL,
                    "PaymentMode" INTEGER NOT NULL,
                    "Remarks" TEXT NULL,
                    "EnteredByUserId" TEXT NULL,
                    "ApprovalStatus" INTEGER NOT NULL,
                    "ApprovedByUserId" TEXT NULL,
                    "ApprovedOn" TEXT NULL,
                    "ApprovalRemarks" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "IsDeleted" INTEGER NOT NULL,
                    "CreatedByUserId" TEXT NULL,
                    "UpdatedByUserId" TEXT NULL
                )
                """);

            if (!await ColumnExistsAsync("TripTransactions", "SettlementId"))
                await ExecAsync("ALTER TABLE \"TripTransactions\" ADD COLUMN \"SettlementId\" TEXT NULL");
        }

        foreach (var (table, column) in new[] { ("States", "IsActive"), ("Cities", "IsActive") })
        {
            if (await ColumnExistsAsync(table, column)) continue;

            var fix = $"Source predates PlaceActiveFlag (2026-09-06): added {table}.{column}.";
            Console.WriteLine(fix);
            fixesApplied.Add(fix);
            await ExecAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" INTEGER NOT NULL DEFAULT 1");
        }

        return fixesApplied;
    }

    /// <summary>
    /// Reads every row of one table from the source with the tenant filter
    /// disabled (there is no signed-in user here, so the filter would
    /// otherwise see zero rows for every company) and no navigation
    /// properties loaded, then inserts those same rows into the target
    /// unchanged — same Id, same CreatedAt, same everything.
    /// </summary>
    private static async Task<int> CopyAsync<T>(
        AppDbContext source, AppDbContext target,
        Func<AppDbContext, DbSet<T>> sourceSet, Func<AppDbContext, DbSet<T>> targetSet)
        where T : class
    {
        var rows = await sourceSet(source).IgnoreQueryFilters().AsNoTracking().ToListAsync();
        if (rows.Count == 0)
        {
            Console.WriteLine($"{typeof(T).Name}: 0 rows.");
            return 0;
        }

        foreach (var row in rows) StampUtcKind(row);

        targetSet(target).AddRange(rows);
        await target.SaveChangesAsync();

        Console.WriteLine($"{typeof(T).Name}: {rows.Count} row{(rows.Count == 1 ? "" : "s")}.");
        return rows.Count;
    }

    /// <summary>
    /// SQLite never stores DateTime.Kind, so every DateTime read back out of
    /// it comes back Unspecified — regardless of whether the app originally
    /// wrote DateTime.Now or DateTime.UtcNow. Npgsql's `timestamp with time
    /// zone` columns refuse anything but Kind=Utc, so every instant column
    /// (everything except the calendar-day ones, which map to a Kind-agnostic
    /// `date` column) gets re-stamped here — a label change only, not a clock
    /// shift, since it would take more than this tool can know about each
    /// historical row to say what timezone the original DateTime.Now call ran
    /// in, and the goal is to reproduce what the app already showed users,
    /// not correct it.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo[]> InstantPropertiesCache = new();

    private static void StampUtcKind<T>(T entity) where T : class
    {
        var properties = InstantPropertiesCache.GetOrAdd(typeof(T), t => t.GetProperties()
            .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
            .Where(p => !AppDbContext.CalendarDayProperties.Contains((t.Name, p.Name)))
            .ToArray());

        foreach (var property in properties)
        {
            var value = property.GetValue(entity);
            switch (value)
            {
                case DateTime dt when dt.Kind != DateTimeKind.Utc:
                    property.SetValue(entity, DateTime.SpecifyKind(dt, DateTimeKind.Utc));
                    break;
            }
        }
    }

    private static string? ArgOrEnv(string[] args, string flag, string envVar)
    {
        var index = Array.IndexOf(args, flag);
        if (index >= 0 && index + 1 < args.Length) return args[index + 1];
        return Environment.GetEnvironmentVariable(envVar);
    }

    private static string RedactPassword(string connectionString) =>
        System.Text.RegularExpressions.Regex.Replace(
            connectionString, @"(Password=)[^;]*", "$1***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
