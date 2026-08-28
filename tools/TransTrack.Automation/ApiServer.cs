using System.Diagnostics;

namespace TransTrack.Automation;

/// <summary>
/// Runs the real TransTrack.Api against a throwaway SQLite file for the duration of a run.
///
/// This is the one place this layer deliberately parts company with ABPS_WEB.Automation, which mocks
/// every HTTP response. That choice was right there — its API needs SQL Server, so a mock is what
/// makes the suite runnable at all. Here the API is a single process over a *file*, and
/// TRANSTRUCKWEB_DB already exists to point it somewhere harmless, so the live stack costs one
/// process to start and gives three things a fixture set cannot:
///
///  * Determinism without maintenance. The dataset is rebuilt from empty on every run through the
///    product's own onboarding endpoint, so it is reproducible for the same reason a fixture is —
///    but it cannot drift from the API the way a hand-written fixture silently does the first time a
///    response shape changes and nobody updates the mock.
///  * Real coverage. A mocked UAT proves the React client renders a shape someone typed into a
///    fixture. This proves the client, the controllers, EF, the tenant filter and SQLite agree.
///  * No second implementation. Mocking this product means hand-maintaining roughly forty endpoints,
///    which is a shadow backend that has to be kept in step forever.
///
/// The price is that a run is a few seconds slower and needs the API to build. That is worth paying.
/// Nothing here can touch a real installation: the database path, the backup folder and the port are
/// all overridden to run-local values, and the production port 6041 is never bound.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    private readonly Process? _ownedProcess;
    private readonly string? _dataDirectory;

    private ApiServer(string baseUrl, Process? ownedProcess, string? dataDirectory)
    {
        BaseUrl = baseUrl;
        _ownedProcess = ownedProcess;
        _dataDirectory = dataDirectory;
    }

    public string BaseUrl { get; }

    /// <summary>
    /// Starts the API on <see cref="AutomationOptions.ApiBaseUrl"/> against a fresh database, unless
    /// something is already answering there — in which case it is reused and left running, so a
    /// developer with the API up in another terminal keeps it.
    /// </summary>
    public static async Task<ApiServer> StartAsync(
        AutomationOptions options,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = options.ApiBaseUrl.TrimEnd('/');

        if (await ManagedProcess.IsRespondingAsync($"{baseUrl}/api/auth/me", cancellationToken))
        {
            log?.Invoke($"Reusing the API already serving {baseUrl}");
            return new ApiServer(baseUrl, ownedProcess: null, dataDirectory: null);
        }

        if (!options.ManageServers)
            throw new InvalidOperationException(
                $"Nothing is serving {baseUrl} and TRANSTRUCK_UAT_MANAGE_SERVERS=false, so the automation " +
                "will not start one. Start the API yourself or unset that variable.");

        // A per-run folder under artifacts/, not the product's C:\TransTruckWeb: a UAT run must never
        // open, migrate or back up a database anyone cares about.
        var dataDirectory = Path.Combine(RepoPaths.ArtifactsDir, "api-data", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(dataDirectory);

        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = baseUrl,
            ["TRANSTRUCKWEB_DB"] = Path.Combine(dataDirectory, "uat.db"),
            ["TRANSTRUCKWEB_BACKUPDIR"] = Path.Combine(dataDirectory, "backup"),
            ["TRANSTRACK_LOG_DIR"] = Path.Combine(dataDirectory, "logs"),

            // Fixed rather than generated so a reused browser cookie stays valid if the API is
            // restarted mid-session. Test-only, and the API only listens on loopback.
            ["Jwt__Key"] = "transtruck-uat-signing-key-not-used-anywhere-real-0123456789",

            // The Development default only allows localhost:3000, and the dev server this automation
            // starts is on 5310. Indexed binding is how ASP.NET overlays a config array element.
            ["Cors__AllowedOrigins__0"] = options.BaseUrl.TrimEnd('/'),
        };

        log?.Invoke($"Starting TransTrack.Api on {baseUrl} against a throwaway database in {dataDirectory}");

        var process = ManagedProcess.Start(
            "dotnet",
            RepoPaths.Root,
            new[] { "run", "--project", RepoPaths.ApiProject, "--configuration", "Release", "--no-launch-profile" },
            environment);

        var server = new ApiServer(baseUrl, process, dataDirectory);

        try
        {
            // Generous: a cold run compiles the API and applies every migration to a new file.
            await ManagedProcess.WaitUntilRespondingAsync(
                $"{baseUrl}/api/auth/me", TimeSpan.FromSeconds(180), process, "TransTrack.Api", cancellationToken);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }

        log?.Invoke($"API ready at {baseUrl}");
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await ManagedProcess.StopAsync(_ownedProcess);

        // The database is left on disk when the run owned it: a failed scenario is much easier to
        // diagnose against the data it actually ran on. artifacts/ is gitignored and these are a few
        // hundred KB each.
        await Task.CompletedTask;
    }
}
