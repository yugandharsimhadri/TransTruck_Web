using System.Diagnostics;

namespace TransTrack.Automation;

/// <summary>
/// Runs the real TransTrack.Api against a throwaway PostgreSQL database for the duration of a run.
///
/// This is the one place this layer deliberately parts company with ABPS_WEB.Automation, which mocks
/// every HTTP response. That choice was right there — its API needs SQL Server, so a mock is what
/// makes the suite runnable at all. Here the live stack costs one process to start, plus a database
/// created and dropped around it (see <see cref="UatDatabase"/>), and gives three things a fixture
/// set cannot:
///
///  * Determinism without maintenance. The dataset is rebuilt from empty on every run through the
///    product's own onboarding endpoint, so it is reproducible for the same reason a fixture is —
///    but it cannot drift from the API the way a hand-written fixture silently does the first time a
///    response shape changes and nobody updates the mock.
///  * Real coverage. A mocked UAT proves the React client renders a shape someone typed into a
///    fixture. This proves the client, the controllers, EF, the tenant filter and PostgreSQL agree —
///    on the same provider production runs, which is worth more since the migration than it was
///    when this and production were both SQLite.
///  * No second implementation. Mocking this product means hand-maintaining roughly forty endpoints,
///    which is a shadow backend that has to be kept in step forever.
///
/// The price is that a run is a few seconds slower, needs the API to build, and now needs a
/// PostgreSQL server to be reachable. That is worth paying.
///
/// Nothing here can touch a real installation: the database is a fresh one created for this run
/// alone, the backup and log folders are overridden to run-local paths, and the production port 6041
/// is never bound.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    private readonly Process? _ownedProcess;
    private readonly string? _dataDirectory;
    private readonly UatDatabase? _database;

    private ApiServer(string baseUrl, Process? ownedProcess, string? dataDirectory, UatDatabase? database)
    {
        BaseUrl = baseUrl;
        _ownedProcess = ownedProcess;
        _dataDirectory = dataDirectory;
        _database = database;
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
            return new ApiServer(baseUrl, ownedProcess: null, dataDirectory: null, database: null);
        }

        if (!options.ManageServers)
            throw new InvalidOperationException(
                $"Nothing is serving {baseUrl} and TRANSTRUCK_UAT_MANAGE_SERVERS=false, so the automation " +
                "will not start one. Start the API yourself or unset that variable.");

        if (string.IsNullOrWhiteSpace(options.PostgresConnectionString))
            throw new InvalidOperationException(
                "This suite needs a PostgreSQL server: the API no longer runs on SQLite, so there is "
                + "no throwaway file to point it at. Set TRANSTRUCK_UAT_PG_CONNECTION to a server and "
                + "a user allowed to create databases, for example:\n"
                + "  $env:TRANSTRUCK_UAT_PG_CONNECTION = "
                + "\"Host=localhost;Database=postgres;Username=postgres;Password=...\"\n"
                + "Only the host and credentials are used — each run creates and drops its own "
                + $"{UatDatabase.Prefix}* database and never opens an existing one.");

        // A per-run folder under artifacts/, not the product's C:\TransTruckWeb: a UAT run must never
        // write logs or uploaded documents anywhere anyone cares about. The database itself is no
        // longer a file, but everything that sat beside it still is.
        var dataDirectory = Path.Combine(RepoPaths.ArtifactsDir, "api-data", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(dataDirectory);

        var database = await UatDatabase.CreateAsync(
            options.PostgresConnectionString!, options.KeepDatabase, log, cancellationToken);

        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",

            // Both, and the unprefixed one is the one that actually binds.
            // ASPNETCORE_URLS is read into *host* configuration, which the
            // appsettings files are then layered on top of — and since the API
            // moved its port into configuration, appsettings.Development.json
            // carries "Urls": "http://localhost:5034" and quietly won. The run
            // then sat waiting for 5311 while a perfectly healthy API answered
            // on 5034. The unprefixed "Urls" variable is applied after the JSON
            // files, so it is the one that settles it.
            ["ASPNETCORE_URLS"] = baseUrl,
            ["Urls"] = baseUrl,

            // The run's own database, created a moment ago and empty. The API applies its migrations
            // into it on start, which is what keeps the schema under test the one the product builds
            // rather than one this harness knows how to build.
            ["TRANSTRUCKWEB_PG_CONNECTION"] = database.ConnectionString,
            ["TRANSTRUCKWEB_BACKUPDIR"] = Path.Combine(dataDirectory, "backup"),
            ["TRANSTRACK_LOG_DIR"] = Path.Combine(dataDirectory, "logs"),

            // Fixed rather than generated so a reused browser cookie stays valid if the API is
            // restarted mid-session. Test-only, and the API only listens on loopback.
            ["Jwt__Key"] = "transtruck-uat-signing-key-not-used-anywhere-real-0123456789",

            // The Development default only allows localhost:3000, and the dev server this automation
            // starts is on 5310. Indexed binding is how ASP.NET overlays a config array element.
            ["Cors__AllowedOrigins__0"] = options.BaseUrl.TrimEnd('/'),

            // Sign-in is rate limited to ten a minute per IP, which is right for a real deployment
            // and wrong for a suite that signs in once per scenario: a full run makes about thirty
            // attempts in under two minutes, every one of them from 127.0.0.1, so the eleventh
            // onwards would be refused and the run would fail as "the password did not work".
            // Raised here rather than switched off in the product, so the limiter itself is still
            // exercised — just with a ceiling no honest test run reaches.
            ["RateLimit__AuthPermitsPerMinute"] = "10000",
        };

        log?.Invoke($"Starting TransTrack.Api on {baseUrl} against {database.Name}, logging to {dataDirectory}");

        var process = ManagedProcess.Start(
            "dotnet",
            RepoPaths.Root,
            new[] { "run", "--project", RepoPaths.ApiProject, "--configuration", "Release", "--no-launch-profile" },
            environment);

        var server = new ApiServer(baseUrl, process, dataDirectory, database);

        try
        {
            // Generous: a cold run compiles the API and applies every migration to a new database.
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
        // Strictly before the drop: DROP DATABASE is refused while any session is attached, and the
        // API holds a connection pool open for as long as it is running.
        await ManagedProcess.StopAsync(_ownedProcess);

        // Dropped, unlike the SQLite file this replaced. Keeping every run's data was cheap when it
        // was a few hundred KB under a gitignored folder; a database left on a server stays there
        // until someone goes looking. TRANSTRUCK_UAT_KEEP_DB=true keeps it when diagnosing.
        if (_database is not null)
            await _database.DropAsync();

        // The logs and any uploaded documents stay either way: they are what a failed scenario is
        // read from, and they are under artifacts/.
    }
}
