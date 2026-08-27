namespace TransTrack.Data;

/// <summary>
/// The one place that decides where this installation keeps its data.
///
/// The database, its backups and the uploaded documents all default to
/// subfolders of a single root. That root used to be worked out separately in
/// each of those three places, which meant moving the installation to another
/// drive took several edits and silently half-worked if any were missed —
/// exactly the failure that leaves a restored backup pointing at documents
/// that were never copied across.
///
/// Setting <c>DataRoot</c> in appsettings.json now moves all three together.
/// Each individual path can still be set on its own and wins over this when
/// present, so a deployment can put the database on a fast disk and the
/// documents on a large one.
///
/// Note that logs deliberately do <em>not</em> hang off this root — see
/// <see cref="AppLog"/>. Logging has to keep working when the drive holding
/// the data is the thing that failed.
/// </summary>
public static class AppPaths
{
    /// <summary>Set TRANSTRUCKWEB_ROOT to relocate the whole installation
    /// without editing a file — matching the per-path override variables the
    /// database, backup and document folders already honour. Wins over
    /// appsettings.json, same as those do.</summary>
    public const string RootOverrideVariable = "TRANSTRUCKWEB_ROOT";

    /// <summary>The folder the default data locations hang off.</summary>
    public static string DataRoot
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(RootOverrideVariable);
            if (string.IsNullOrWhiteSpace(configured)) configured = AppConfig.Current.DataRoot;

            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            // The system drive rather than a literal C:, so a machine that
            // boots from another letter still lands somewhere sensible.
            // Kept separate from the TransTruck_WPF product's C:\TransTrack —
            // the two have diverged schemas and must never share a folder.
            return Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "TransTruckWeb");
        }
    }

    /// <summary>A named subfolder of the data root, e.g. "DB" or "VehicleDocs".</summary>
    public static string Under(string leaf) => Path.Combine(DataRoot, leaf);
}
