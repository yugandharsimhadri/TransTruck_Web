using System.Text;

namespace TransTrack.Data;

/// <summary>
/// Plain file logging, one file per day next to the database. Deliberately tiny
/// and dependency-free: this runs on an office PC with no log server, and the
/// first thing anyone needs after a crash is a file they can attach to an email.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static bool _pruned;
    private static string? _resolved;

    /// <summary>Overrides the configured folder. Used by tests.</summary>
    public const string DirectoryOverrideVariable = "TRANSTRACK_LOG_DIR";

    /// <summary>The folder that was asked for but could not be used, if any.</summary>
    public static string? FallbackReason { get; private set; }

    public static string LogDirectory
    {
        get
        {
            var over = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);

            if (_resolved is not null && over == _resolvedFrom) return _resolved;

            _resolvedFrom = over;
            return _resolved = Resolve();
        }
    }

    private static string? _resolvedFrom;

    private static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
        if (string.IsNullOrWhiteSpace(configured)) configured = AppConfig.Current.LogDirectory;

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TransTrack", "logs"));

        candidates.Add(Path.Combine(Path.GetTempPath(), "TransTrack", "logs"));

        foreach (var candidate in candidates)
        {
            if (!IsUsable(candidate)) continue;

            if (candidate != candidates[0])
            {
                FallbackReason =
                    $"'{candidates[0]}' could not be written to, so logs are going to '{candidate}' instead.";
            }

            return candidate;
        }

        // Nothing was writable. Writing then fails quietly rather than throwing.
        return candidates[0];
    }

    private static bool IsUsable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var probe = Path.Combine(directory, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? _currentFile;
    private static string _currentDay = "";
    private static string _currentDirectory = "";
    private static int _sinceSizeCheck;

    public static string CurrentFile
    {
        get
        {
            var day = DateTime.Now.ToString("yyyyMMdd");
            var directory = LogDirectory;

            if (_currentFile is not null
                && day == _currentDay
                && directory == _currentDirectory
                && ++_sinceSizeCheck < 200)
            {
                return _currentFile;
            }

            _currentDay = day;
            _currentDirectory = directory;
            _sinceSizeCheck = 0;

            var today = Path.Combine(directory, $"transtrack-{day}.log");
            var limit = Math.Max(1, AppConfig.Current.LogFileMaxMb) * 1024L * 1024L;

            try
            {
                if (!File.Exists(today) || new FileInfo(today).Length < limit)
                    return _currentFile = today;

                for (var part = 2; part < 1000; part++)
                {
                    var rolled = Path.Combine(directory, $"transtrack-{day}-{part}.log");

                    if (!File.Exists(rolled) || new FileInfo(rolled).Length < limit)
                        return _currentFile = rolled;
                }

                return _currentFile = today;
            }
            catch (IOException)
            {
                return _currentFile = today;
            }
        }
    }

    public static void Info(string message) => Write("INF", message, null);
    public static void Warn(string message) => Write("WRN", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERR", message, ex);
    public static void Trace(string message) => Write("TRC", message, null);

    /// <summary>
    /// Logs anything thrown by a task nobody is awaiting. Every fire-and-forget
    /// call in the UI goes through this so a failure lands in the log instead of
    /// disappearing or tearing the process down.
    /// </summary>
    public static void Forget(this Task task, string context)
        => task.ContinueWith(
            t => Error($"{context} failed", t.Exception?.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(' ').Append(level)
                .Append(' ').Append(message);

            if (ex is not null)
            {
                line.AppendLine()
                    .Append("    ").Append(ex.GetType().Name).Append(": ").Append(ex.Message)
                    .AppendLine()
                    .Append(Indent(ex.StackTrace));

                if (ex.InnerException is { } inner)
                {
                    line.AppendLine()
                        .Append("    caused by ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
                }
            }

            lock (Gate)
            {
                PruneOnce();
                File.AppendAllText(CurrentFile, line.AppendLine().ToString());
            }
        }
        catch (Exception)
        {
            // Logging must never be the reason the application stops working.
        }
    }

    private static string Indent(string? stackTrace)
        => string.IsNullOrWhiteSpace(stackTrace)
            ? "    (no stack trace)"
            : string.Join(Environment.NewLine,
                stackTrace.Split('\n').Select(l => "    " + l.TrimEnd('\r')));

    /// <summary>Keeps a month of logs; a normal day still only writes a few KB.</summary>
    private static void PruneOnce()
    {
        if (_pruned) return;
        _pruned = true;

        try
        {
            foreach (var stale in new DirectoryInfo(LogDirectory)
                         .GetFiles("transtrack-*.log")
                         .OrderByDescending(f => f.Name)
                         .Skip(Math.Max(1, AppConfig.Current.LogDaysToKeep)))
            {
                stale.Delete();
            }
        }
        catch (IOException) { }
    }
}
