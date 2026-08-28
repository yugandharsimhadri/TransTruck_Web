using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TransTrack.Automation;

/// <summary>
/// Brings up the Next.js client if nothing is already serving it. An already-running server is
/// reused and never shut down, so a developer with `npm run dev` open in another terminal keeps it
/// after a run finishes.
/// </summary>
public sealed class WebDevServer : IAsyncDisposable
{
    private readonly Process? _ownedProcess;

    private WebDevServer(Process? ownedProcess) => _ownedProcess = ownedProcess;

    public static async Task<WebDevServer> StartAsync(
        AutomationOptions options,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/');

        if (await ManagedProcess.IsRespondingAsync(baseUrl, cancellationToken))
        {
            // Something answers, but "a Next dev server" is not the same as "*our* Next dev server" —
            // several projects on this machine serve an identical-looking shell. Attaching to the
            // wrong one produces failures that read as TransTruck bugs, so the app is identified
            // before it is trusted.
            if (!await IsServingTransTruckAsync(baseUrl, cancellationToken))
                throw new InvalidOperationException(
                    $"{baseUrl} is already serving a different application, not the TransTruck client. " +
                    "Stop whatever is on that port, or point the suite elsewhere with TRANSTRUCK_UAT_BASE_URL.");

            log?.Invoke($"Reusing the TransTruck dev server already serving {baseUrl}");
            return new WebDevServer(ownedProcess: null);
        }

        if (!options.ManageServers)
            throw new InvalidOperationException(
                $"Nothing is serving {baseUrl} and TRANSTRUCK_UAT_MANAGE_SERVERS=false, so the automation " +
                "will not start one. Start the client yourself or unset that variable.");

        var webPath = options.WebProjectPath;
        if (!Directory.Exists(webPath))
            throw new DirectoryNotFoundException($"Client project not found at '{webPath}'.");

        await EnsureNodeModulesAsync(webPath, log, cancellationToken);

        var port = new Uri(baseUrl).Port;

        // Next has no --strictPort (that is a Vite flag): given a busy port it prints a notice and
        // quietly starts on the next free one, which would leave the automation waiting on a URL
        // nothing serves and then failing as an unexplained timeout. Binding the port first turns
        // that into the sentence the person running it actually needs.
        EnsurePortAvailable(port);

        // Run Next's own bin through node rather than `npm run dev`. On Windows that route is
        // cmd.exe -> npm.cmd -> node, and killing the tree from the cmd process reliably leaves the
        // node grandchild alive still holding the port — which then fails the *next* run with
        // EADDRINUSE and looks like a port clash with some other project. One process, one kill.
        var nextBin = Path.Combine(webPath, "node_modules", "next", "dist", "bin", "next");
        if (!File.Exists(nextBin))
            throw new FileNotFoundException(
                $"Next.js is not installed at '{nextBin}'. Run 'npm install' in {webPath}.", nextBin);

        log?.Invoke($"Starting the Next.js dev server in {webPath} on port {port}");

        var process = ManagedProcess.Start(
            "node",
            webPath,
            new[] { nextBin, "dev", "--port", port.ToString() },
            new Dictionary<string, string>
            {
                // The whole reason the dev server is started here rather than by hand: the client
                // must call the run's own throwaway API, not a developer's .env.local, and not the
                // real api.lorryowner.com.
                ["NEXT_PUBLIC_API_URL"] = options.ApiBaseUrl.TrimEnd('/'),
            });

        var server = new WebDevServer(process);

        try
        {
            await ManagedProcess.WaitUntilRespondingAsync(
                baseUrl, TimeSpan.FromSeconds(120), process, "The Next.js dev server", cancellationToken);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }

        log?.Invoke($"Dev server ready at {baseUrl}");
        return server;
    }

    /// <summary>
    /// Refuses to start when something already holds the port. The check above ("is our app already
    /// serving it?") answers a different question: this catches a non-HTTP listener, or an HTTP one
    /// that was not answering when probed.
    /// </summary>
    private static void EnsurePortAvailable(int port)
    {
        try
        {
            // IPv6Any in dual-mode, because that is what Next binds ("::"). Probing 127.0.0.1 alone
            // reports the port free while Next then fails with EADDRINUSE on the v6 wildcard.
            using var listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.DualMode = true;
            listener.Start();
        }
        catch (SocketException)
        {
            throw new InvalidOperationException(
                $"Port {port} is already in use, so the TransTruck dev server cannot have it. " +
                "Stop whatever is holding it, or point the suite at another port with TRANSTRUCK_UAT_BASE_URL. " +
                "The port is pinned deliberately - letting Next fall back to the next free one is how a run " +
                "ends up driving somebody else's application.");
        }
    }

    private static async Task EnsureNodeModulesAsync(string webPath, Action<string>? log, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(webPath, "node_modules")))
            return;

        log?.Invoke("node_modules is missing — running 'npm install' (first run only, this takes a while)");

        using var install = ManagedProcess.Start("npm", webPath, new[] { "install" });
        await install.WaitForExitAsync(cancellationToken);

        if (install.ExitCode != 0)
            throw new InvalidOperationException($"'npm install' failed in '{webPath}' with exit code {install.ExitCode}.");
    }

    /// <summary>
    /// Identifies the app by its own sign-in page rather than by the served shell, which is too
    /// generic to tell two Next projects apart.
    /// </summary>
    private static async Task<bool> IsServingTransTruckAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var body = await http.GetStringAsync($"{baseUrl}/login", cancellationToken);
            return body.Contains("LorryOwner", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public ValueTask DisposeAsync() => new(ManagedProcess.StopAsync(_ownedProcess));
}
