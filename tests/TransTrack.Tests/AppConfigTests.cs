using System.Reflection;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// Storage paths are the one part of configuration a deployment genuinely has
/// to change — putting the database on C: but the backups and uploaded
/// documents on a bigger drive. AppConfig originally read only
/// appsettings.json, so anything set in appsettings.Production.json was
/// silently ignored and the app quietly kept writing to the C: defaults.
/// These pin the base + environment layering down.
/// </summary>
[Collection(ProcessStateCollection.Name)]
public class AppConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lo-config-{Guid.NewGuid():N}");
    private readonly string? _originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    private readonly string? _originalRoot = Environment.GetEnvironmentVariable(AppPaths.RootOverrideVariable);

    public AppConfigTests() => Directory.CreateDirectory(_dir);

    /// <summary>AppConfig reads from AppContext.BaseDirectory and caches, so a
    /// test drives it through the same Load it uses in production, pointed at
    /// a throwaway folder.</summary>
    private AppSettings Load(string? baseJson, string? environmentJson, string environment)
    {
        if (baseJson is not null) File.WriteAllText(Path.Combine(_dir, "appsettings.json"), baseJson);
        if (environmentJson is not null) File.WriteAllText(Path.Combine(_dir, $"appsettings.{environment}.json"), environmentJson);

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        AppDomain.CurrentDomain.SetData("APP_CONTEXT_BASE_DIRECTORY", _dir);

        // Reset the cached instance so each case loads fresh.
        typeof(AppConfig).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);

        return AppConfig.Current;
    }

    [Fact]
    public void Production_file_overrides_the_base_file()
    {
        var settings = Load(
            baseJson: """{ "BackupDirectory": "C:\\TransTruckWeb\\DBBackup" }""",
            environmentJson: """{ "BackupDirectory": "E:\\LorryOwner\\Backup" }""",
            environment: "Production");

        Assert.Equal(@"E:\LorryOwner\Backup", settings.BackupDirectory);
    }

    [Fact]
    public void Production_file_sets_a_path_the_base_file_never_mentions()
    {
        var settings = Load(
            baseJson: """{ "BackupsToKeep": 14 }""",
            environmentJson: """{ "VehicleDocumentDirectory": "F:\\LorryOwner\\VehicleDocs" }""",
            environment: "Production");

        Assert.Equal(@"F:\LorryOwner\VehicleDocs", settings.VehicleDocumentDirectory);
    }

    [Fact]
    public void Keys_the_environment_file_leaves_out_keep_their_base_value()
    {
        var settings = Load(
            baseJson: """{ "BackupsToKeep": 30, "LogDaysToKeep": 45 }""",
            environmentJson: """{ "BackupDirectory": "E:\\LorryOwner\\Backup" }""",
            environment: "Production");

        Assert.Equal(30, settings.BackupsToKeep);
        Assert.Equal(45, settings.LogDaysToKeep);
        Assert.Equal(@"E:\LorryOwner\Backup", settings.BackupDirectory);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated_in_both_files()
    {
        var environmentJson = string.Join('\n',
            "{",
            "  // where the documents live",
            @"  ""VehicleDocumentDirectory"": ""E:\\Docs"",",
            "}");

        var settings = Load(
            baseJson: """{ /* base */ "BackupsToKeep": 14, }""",
            environmentJson: environmentJson,
            environment: "Production");

        Assert.Equal(@"E:\Docs", settings.VehicleDocumentDirectory);
        Assert.Equal(14, settings.BackupsToKeep);
    }

    /// <summary>The point of all this: the two folders a deployment actually
    /// relocates must follow the Production file, not the built-in C: defaults.
    /// Asserted through the real properties the app reads at runtime.</summary>
    [Fact]
    public void Backup_and_document_folders_follow_the_production_file()
    {
        var backup = Path.Combine(_dir, "relocated-backup");
        var documents = Path.Combine(_dir, "relocated-docs");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "BackupDirectory": {{System.Text.Json.JsonSerializer.Serialize(backup)}},
                  "VehicleDocumentDirectory": {{System.Text.Json.JsonSerializer.Serialize(documents)}} }
                """,
            environment: "Production");

        Assert.Equal(backup, DbBootstrapper.BackupDirectory);
        Assert.Equal(documents, FileSystemDocumentStorage.RootDirectory);
    }

    // ── DataRoot ────────────────────────────────────────────────────────────
    // Moving the installation to another drive used to mean setting three
    // paths and silently half-working if one was missed. These pin down that
    // one setting moves all three, and that a specific path still wins.

    /// <summary>The whole point of DataRoot: set it alone and the database,
    /// backups and documents all follow it onto the new drive.</summary>
    [Fact]
    public void DataRoot_moves_the_database_backups_and_documents_together()
    {
        var root = Path.Combine(_dir, "relocated");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "DataRoot": {{System.Text.Json.JsonSerializer.Serialize(root)}} }
                """,
            environment: "Production");

        Assert.Equal(Path.Combine(root, "DB", "TransTruckWeb.db"), DbBootstrapper.DatabasePath);
        Assert.Equal(Path.Combine(root, "DBBackup"), DbBootstrapper.BackupDirectory);
        Assert.Equal(Path.Combine(root, "VehicleDocs"), FileSystemDocumentStorage.RootDirectory);
    }

    /// <summary>The database is the one people most often want somewhere
    /// specific — a fast disk, a particular folder they already back up. An
    /// explicit DatabasePath is a full path including the file name, and it
    /// must beat DataRoot without dragging the other folders along.</summary>
    [Fact]
    public void An_explicit_DatabasePath_wins_over_DataRoot()
    {
        var root = Path.Combine(_dir, "relocated");
        var database = Path.Combine(_dir, "fast-disk", "LorryOwner.db");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "DataRoot": {{System.Text.Json.JsonSerializer.Serialize(root)}},
                  "DatabasePath": {{System.Text.Json.JsonSerializer.Serialize(database)}} }
                """,
            environment: "Production");

        Assert.Equal(database, DbBootstrapper.DatabasePath);
        // Backups and documents are unaffected by that one setting.
        Assert.Equal(Path.Combine(root, "DBBackup"), DbBootstrapper.BackupDirectory);
        Assert.Equal(Path.Combine(root, "VehicleDocs"), FileSystemDocumentStorage.RootDirectory);
    }

    /// <summary>DatabasePath on its own, with no DataRoot anywhere — the
    /// plainest way to move just the database, and the one most likely to be
    /// typed by hand into appsettings.Production.json.</summary>
    [Fact]
    public void DatabasePath_alone_moves_the_database_and_creates_its_folder()
    {
        var database = Path.Combine(_dir, "moved-by-hand", "LorryOwner.db");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "DatabasePath": {{System.Text.Json.JsonSerializer.Serialize(database)}} }
                """,
            environment: "Production");

        Assert.Equal(database, DbBootstrapper.DatabasePath);
        // The getter creates the folder, so a path into a directory that does
        // not exist yet is a valid thing to write in the config file.
        Assert.True(Directory.Exists(Path.GetDirectoryName(database)));
    }

    /// <summary>A deployment that puts the documents on a second, larger disk
    /// must be able to say so without giving up DataRoot for everything else.</summary>
    [Fact]
    public void An_explicit_path_still_wins_over_DataRoot()
    {
        var root = Path.Combine(_dir, "relocated");
        var documents = Path.Combine(_dir, "big-disk-docs");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "DataRoot": {{System.Text.Json.JsonSerializer.Serialize(root)}},
                  "VehicleDocumentDirectory": {{System.Text.Json.JsonSerializer.Serialize(documents)}} }
                """,
            environment: "Production");

        Assert.Equal(documents, FileSystemDocumentStorage.RootDirectory);
        // The others still follow the root — the override is per folder.
        Assert.Equal(Path.Combine(root, "DBBackup"), DbBootstrapper.BackupDirectory);
    }

    /// <summary>Logs stay put on purpose: the log is what you read when the
    /// data drive is the thing that failed.</summary>
    [Fact]
    public void DataRoot_does_not_move_the_logs()
    {
        var root = Path.Combine(_dir, "relocated");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "DataRoot": {{System.Text.Json.JsonSerializer.Serialize(root)}} }
                """,
            environment: "Production");

        Assert.False(AppLog.LogDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The deploy scripts relocate an installation by exporting
    /// TRANSTRUCKWEB_ROOT rather than editing JSON, so that route has to move
    /// all three folders too — and win over the config file, matching how the
    /// per-path override variables already behave.</summary>
    [Fact]
    public void The_root_environment_variable_moves_everything_and_beats_the_config_file()
    {
        var fromConfig = Path.Combine(_dir, "from-config");
        var fromEnvironment = Path.Combine(_dir, "from-environment");

        Load(
            baseJson: "{ }",
            environmentJson: $$"""
                { "DataRoot": {{System.Text.Json.JsonSerializer.Serialize(fromConfig)}} }
                """,
            environment: "Production");

        Environment.SetEnvironmentVariable(AppPaths.RootOverrideVariable, fromEnvironment);

        Assert.Equal(Path.Combine(fromEnvironment, "DB", "TransTruckWeb.db"), DbBootstrapper.DatabasePath);
        Assert.Equal(Path.Combine(fromEnvironment, "DBBackup"), DbBootstrapper.BackupDirectory);
        Assert.Equal(Path.Combine(fromEnvironment, "VehicleDocs"), FileSystemDocumentStorage.RootDirectory);
    }

    /// <summary>Nothing set anywhere must keep landing exactly where every
    /// existing installation already has its data.</summary>
    [Fact]
    public void No_DataRoot_keeps_the_original_default()
    {
        Load(baseJson: "{ }", environmentJson: null, environment: "Production");

        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)!;

        Assert.Equal(Path.Combine(systemDrive, "TransTruckWeb"), AppPaths.DataRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _originalEnvironment);
        // Process-wide, so it has to go back or it leaks into whichever test
        // in this collection runs next.
        Environment.SetEnvironmentVariable(AppPaths.RootOverrideVariable, _originalRoot);
        typeof(AppConfig).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
