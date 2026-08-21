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

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _originalEnvironment);
        typeof(AppConfig).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
