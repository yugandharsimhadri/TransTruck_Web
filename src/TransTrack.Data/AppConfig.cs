using System.Text.Json;

namespace TransTrack.Data;

/// <summary>Settings read from appsettings.json beside the executable.</summary>
public class AppSettings
{
    /// <summary>Where the daily log files are written.</summary>
    public string? LogDirectory { get; set; }

    /// <summary>Full path to the database file. Null uses the default under C:\TransTrack.</summary>
    public string? DatabasePath { get; set; }

    /// <summary>Where daily database backups are written.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>Daily backups to keep before the oldest is deleted.</summary>
    public int BackupsToKeep { get; set; } = 14;

    /// <summary>Where uploaded vehicle documents are stored. Null uses the
    /// default (C:\TransTruckWeb\VehicleDocs). Kept out of the database on
    /// purpose — these are 1-5 MB files, so only the reference is stored in
    /// the VehicleDocuments table.</summary>
    public string? VehicleDocumentDirectory { get; set; }

    /// <summary>Largest upload accepted, in MB. Rejected above this with a
    /// plain message rather than a failed request.</summary>
    public int VehicleDocumentMaxMb { get; set; } = 10;

    /// <summary>Days of log files to keep.</summary>
    public int LogDaysToKeep { get; set; } = 30;

    /// <summary>A log file is rolled once it passes this, so a busy day never
    /// grows into a file too large to open.</summary>
    public int LogFileMaxMb { get; set; } = 10;
}

/// <summary>
/// Machine-level configuration, read once from a plain JSON file next to the
/// executable so the company can move the log or database folder without a
/// rebuild. Deliberately not stored in the database: logging has to work
/// when the database is the thing that failed.
/// </summary>
public static class AppConfig
{
    public const string FileName = "appsettings.json";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The environment-specific file layered on top of
    /// <see cref="FileName"/>, e.g. appsettings.Production.json — the same
    /// base + environment pairing ASP.NET Core itself uses, so a setting put
    /// in the Production file behaves the way anyone would expect. Without
    /// this, production storage paths were read from the base file only and
    /// anything set in the Production file was silently ignored.</summary>
    public static string? EnvironmentFilePath
    {
        get
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return string.IsNullOrWhiteSpace(environment)
                ? null
                : Path.Combine(AppContext.BaseDirectory, $"appsettings.{environment}.json");
        }
    }

    private static AppSettings? _settings;

    public static AppSettings Current => _settings ??= Load();

    /// <summary>Set when a file existed but could not be read, so startup can say so.</summary>
    public static string? LoadError { get; private set; }

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static AppSettings Load()
    {
        try
        {
            var merged = ReadObject(FilePath);
            var environmentFile = EnvironmentFilePath;

            // Environment file wins, but only for the keys it actually sets —
            // merged per key rather than per file, so putting one path in
            // appsettings.Production.json doesn't blank out the rest.
            if (environmentFile is not null)
            {
                foreach (var (key, value) in ReadObject(environmentFile))
                    merged[key] = value;
            }

            if (merged.Count == 0) return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(
                JsonSerializer.Serialize(merged), ReadOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            // A broken config file must not stop the app opening — fall back to
            // the built-in defaults and say so once the log is running.
            LoadError = $"{FileName} could not be read ({ex.Message}). Built-in defaults are being used.";
            return new AppSettings();
        }
    }

    /// <summary>The top-level properties of one config file, or nothing when
    /// it isn't there. Only the keys AppSettings knows about are kept, so the
    /// framework's own sections (Logging, Jwt, Cors) never reach the
    /// deserializer.</summary>
    private static Dictionary<string, JsonElement> ReadObject(string path)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;

        using var document = JsonDocument.Parse(File.ReadAllText(path), ParseOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return result;

        var known = typeof(AppSettings).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
            if (known.Contains(property.Name))
                result[property.Name] = property.Value.Clone();

        return result;
    }

    /// <summary>Used by tests to point the application somewhere harmless.</summary>
    public static void Override(AppSettings settings) => _settings = settings;
}
