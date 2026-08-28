namespace TransTrack.Automation;

/// <summary>
/// How a scenario is being driven, which is the only thing that differs between "run the UAT" and
/// "record the demo": the steps themselves are identical.
/// </summary>
public enum RunMode
{
    /// <summary>Headless, no pacing, no captions — as fast as the browser will go.</summary>
    Test,

    /// <summary>Headed, moderate pacing, one caption per narration beat. Demo videos.</summary>
    Demo,

    /// <summary>Headed, slowest pacing, a numbered caption on every step. User-guide videos.</summary>
    UserGuide,
}

/// <summary>
/// Which shape of screen the journey is driven on. TransTruck is worked on a phone in the yard as
/// much as on a desk, and the two are genuinely different products to use: the desktop sidebar lists
/// every screen, while on mobile only four reach the bottom tab bar and the rest live behind "More".
/// A run covers one viewport, and the UAT covers both.
/// </summary>
public enum Viewport
{
    /// <summary>A 1600x900 desktop browser. No touch, desktop user agent.</summary>
    Desktop,

    /// <summary>
    /// A phone, via Playwright's own device descriptor — so the user agent, device scale factor,
    /// touch support and viewport all move together. Emulating by resizing the window alone would
    /// leave the app believing it is a desktop with a narrow window, which is not the same thing:
    /// pointer-coarse media queries and touch handlers would not engage.
    /// </summary>
    Mobile,
}

/// <summary>
/// All knobs the automation reads, resolved from environment variables so the Content Automation
/// Studio (or any CI runner) can steer a run without a rebuild. Every value has a working default,
/// so a bare <c>dotnet test</c> does the right thing with no configuration at all.
/// </summary>
public sealed record AutomationOptions
{
    /// <summary>
    /// Where the Next.js client is served from. Deliberately not Next's default 3000: several
    /// projects on this machine use the common defaults, and a run that silently attached to
    /// someone else's app would fail in a way that reads as a broken TransTruck. The dev server is
    /// started with --strictPort on this port, so a clash fails loudly instead of hopping.
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:5310";

    /// <summary>
    /// Where the API is served from, and what NEXT_PUBLIC_API_URL is set to for the dev server this
    /// automation starts. 5311 for the same reason as 5310 — the product's own production port is
    /// 6041 and is deliberately left alone, so a UAT run can never reach a real installation.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "http://localhost:5311";

    public RunMode RunMode { get; init; } = RunMode.Test;

    public Viewport Viewport { get; init; } = Viewport.Desktop;

    /// <summary>Absolute path to web/transtrack-web, used to start the dev server on demand.</summary>
    public string WebProjectPath { get; init; } = "";

    /// <summary>
    /// When false the automation assumes something else already serves <see cref="BaseUrl"/> and
    /// <see cref="ApiBaseUrl"/>, and will neither start nor stop them. Useful when pointing the suite
    /// at an already-running pair while writing a workflow.
    /// </summary>
    public bool ManageServers { get; init; } = true;

    /// <summary>
    /// The phone the mobile viewport emulates, by Playwright device-descriptor name. Overridable
    /// because the descriptor list moves between Playwright versions, and a name that vanishes should
    /// be a setting to change rather than a rebuild.
    /// </summary>
    public string MobileDevice { get; init; } = "iPhone 15 Pro";

    /// <summary>Per-action delay Playwright applies, in milliseconds. Raised in the video modes so the cursor is followable.</summary>
    public int SlowMoMs => RunMode switch
    {
        RunMode.Demo => 220,
        RunMode.UserGuide => 380,
        _ => 0,
    };

    /// <summary>How long a narration caption stays on screen before the step it describes runs.</summary>
    public int CaptionHoldMs => RunMode switch
    {
        RunMode.Demo => 1500,
        RunMode.UserGuide => 2600,
        _ => 0,
    };

    /// <summary>Pause held at the end of a workflow so the closing frame is usable in the edit.</summary>
    public int ClosingHoldMs => RunMode switch
    {
        RunMode.Demo => 1200,
        RunMode.UserGuide => 2000,
        _ => 0,
    };

    public bool Headed => RunMode != RunMode.Test;

    public bool ShowCaptions => RunMode != RunMode.Test;

    /// <summary>
    /// Builds the options from environment variables, falling back to the defaults above:
    /// TRANSTRUCK_UAT_BASE_URL, TRANSTRUCK_UAT_API_BASE_URL, TRANSTRUCK_UAT_RUN_MODE (test|demo|userguide),
    /// TRANSTRUCK_UAT_VIEWPORT (desktop|mobile), TRANSTRUCK_UAT_WEB_PATH,
    /// TRANSTRUCK_UAT_MANAGE_SERVERS (true|false), TRANSTRUCK_UAT_MOBILE_DEVICE.
    /// </summary>
    public static AutomationOptions FromEnvironment(
        RunMode defaultRunMode = RunMode.Test,
        Viewport defaultViewport = Viewport.Desktop)
        => new()
        {
            BaseUrl = Env("TRANSTRUCK_UAT_BASE_URL") ?? "http://localhost:5310",
            ApiBaseUrl = Env("TRANSTRUCK_UAT_API_BASE_URL") ?? "http://localhost:5311",
            RunMode = ParseEnum(Env("TRANSTRUCK_UAT_RUN_MODE"), defaultRunMode),
            Viewport = ParseEnum(Env("TRANSTRUCK_UAT_VIEWPORT"), defaultViewport),
            WebProjectPath = Env("TRANSTRUCK_UAT_WEB_PATH") ?? RepoPaths.WebProject,
            ManageServers = !string.Equals(Env("TRANSTRUCK_UAT_MANAGE_SERVERS"), "false", StringComparison.OrdinalIgnoreCase),
            MobileDevice = Env("TRANSTRUCK_UAT_MOBILE_DEVICE") ?? "iPhone 15 Pro",
        };

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
