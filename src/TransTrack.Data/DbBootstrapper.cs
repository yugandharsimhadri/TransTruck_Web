using Microsoft.EntityFrameworkCore;

namespace TransTrack.Data;

public static class DbBootstrapper
{
    /// <summary>Set TRANSTRUCKWEB_DB to run against a different database file. Tests use
    /// this to drive a throwaway database instead of the live one. Deliberately a
    /// different variable name and default root than the separate TransTruck_WPF
    /// product (TRANSTRACK_DB / C:\TransTrack) — the two are independent products
    /// now and must never share a database file, since their schemas diverge
    /// (this one is multi-tenant).</summary>
    public const string PathOverrideVariable = "TRANSTRUCKWEB_DB";

    /// <summary>Set TRANSTRUCKWEB_BACKUPDIR to write backups somewhere harmless —
    /// same reasoning as <see cref="PathOverrideVariable"/>, so a test never
    /// writes into the real C:\TransTruckWeb\DBBackup.</summary>
    public const string BackupDirectoryOverrideVariable = "TRANSTRUCKWEB_BACKUPDIR";

    /// <summary>Database file lives under a fixed root so every Windows user of the PC shares it.</summary>
    public static string DatabasePath
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(PathOverrideVariable);
            if (string.IsNullOrWhiteSpace(overridden)) overridden = AppConfig.Current.DatabasePath;

            if (!string.IsNullOrWhiteSpace(overridden))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(overridden)!);
                return overridden;
            }

            // C:\TransTruckWeb keeps the database, its backups and the logs
            // together, and — critically — separate from TransTruck_WPF's own
            // C:\TransTrack, since this is a different product with a different
            // (multi-tenant) schema now.
            var dir = DefaultRoot("DB");
            Directory.CreateDirectory(dir);

            return Path.Combine(dir, "TransTruckWeb.db");
        }
    }

    /// <summary>Where backups are written. Beside the database unless configured.</summary>
    public static string BackupDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(BackupDirectoryOverrideVariable);
            if (string.IsNullOrWhiteSpace(overridden)) overridden = AppConfig.Current.BackupDirectory;

            var dir = string.IsNullOrWhiteSpace(overridden) ? DefaultRoot("DBBackup") : overridden;
            Directory.CreateDirectory(dir);

            return dir;
        }
    }

    private static string DefaultRoot(string leaf)
    {
        var root = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "TransTruckWeb");
        return Path.Combine(root, leaf);
    }

    public static string ConnectionString => $"Data Source={DatabasePath}";

    /// <summary>Applies migrations and takes a dated backup first. Unlike the
    /// single-tenant desktop product, this never seeds a default company or
    /// Owner login — every company's starter data (masters + its Owner
    /// account) is created by EnterpriseAdminService.OnboardCompanyAsync
    /// when EnterpriseAdmin actually onboards that company, not here at
    /// startup, since there is no longer one single "the" company to
    /// seed.</summary>
    public static async Task InitialiseAsync(IDbContextFactory<AppDbContext> factory)
    {
        var existed = File.Exists(DatabasePath);

        AppLog.Info(existed
            ? $"Opening the existing database at {DatabasePath}."
            : $"No database at {DatabasePath} — creating one.");

        BackupOnce();

        await using var db = await factory.CreateDbContextAsync();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count > 0)
        {
            AppLog.Info($"Applying migrations: {string.Join(", ", pending)}");

            // A copy of the file as it was, before the schema is touched — the
            // daily backup below is not enough on the day of an upgrade, since
            // it takes one backup per day and skips if today's already exists.
            if (existed) BackupBeforeUpgrade(pending[^1]);
        }

        await db.Database.MigrateAsync();
    }

    /// <summary>The newest backup on disk, or null if there has never been one.</summary>
    public static FileInfo? LastBackup
    {
        get
        {
            try
            {
                return RoutineBackups().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>Copies the database now, whatever has already been taken today — the
    /// button someone presses before closing up, and before anything they are
    /// nervous about.</summary>
    public static FileInfo BackupNow(string? companyName = null)
    {
        if (!File.Exists(DatabasePath))
            throw new InvalidOperationException("There is no database to back up yet.");

        var target = Path.Combine(BackupDirectory, $"{CompanySlug(companyName)}-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        File.Copy(DatabasePath, target, overwrite: true);
        Prune();

        AppLog.Info($"Backup taken: {target}");
        return new FileInfo(target);
    }

    private static void Prune()
    {
        try
        {
            foreach (var stale in RoutineBackups()
                         .OrderByDescending(f => f.LastWriteTime)
                         .Skip(Math.Max(1, AppConfig.Current.BackupsToKeep)))
            {
                stale.Delete();
            }
        }
        catch (IOException)
        {
            // A backup that could not be pruned is still a backup.
        }
    }

    private static IEnumerable<FileInfo> RoutineBackups()
        => new DirectoryInfo(BackupDirectory).GetFiles("*.db")
            .Where(f => !f.Name.StartsWith("pre-upgrade-", StringComparison.OrdinalIgnoreCase));

    private static void BackupBeforeUpgrade(string upgradingTo)
    {
        var target = Path.Combine(BackupDirectory, $"pre-upgrade-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        try
        {
            File.Copy(DatabasePath, target);
            AppLog.Info($"Backed up before upgrading to {upgradingTo}: {target}");
        }
        catch (IOException ex)
        {
            AppLog.Error($"Could not back up before upgrading to {upgradingTo}. The upgrade will still be attempted.", ex);
        }
    }

    private static void BackupOnce()
    {
        if (!File.Exists(DatabasePath)) return;

        var target = Path.Combine(BackupDirectory, $"{CompanySlug()}-{DateTime.Now:yyyyMMdd}.db");
        if (File.Exists(target)) return; // one automatic backup per day is enough

        try
        {
            File.Copy(DatabasePath, target);
            Prune();
        }
        catch (IOException)
        {
            // A failed backup must never stop the company opening the app.
        }
    }

    /// <summary>
    /// A filename-safe tag for a backup. The single-tenant desktop product
    /// names its backups after the one company in the database; this
    /// product's database holds every onboarded company at once, so there
    /// is no single name to read here — always the product's own name
    /// unless a caller has a more specific reason to override it.
    /// </summary>
    private static string CompanySlug(string? providedName = null)
    {
        var name = providedName;
        if (string.IsNullOrWhiteSpace(name)) return "transtruckweb";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim(), @"\s+", "-");

        return string.IsNullOrWhiteSpace(cleaned) ? "transtruckweb" : cleaned;
    }
}
