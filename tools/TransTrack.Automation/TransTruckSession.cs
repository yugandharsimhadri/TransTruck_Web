using Microsoft.Playwright;
using TransTrack.Automation.Workflows;

namespace TransTrack.Automation;

/// <summary>
/// One browser session against the TransTruck client: owns Playwright, the browser, the page and the
/// narrator, and knows how to sign in. Both the UAT tests and the DemoRunner build their
/// <see cref="WorkflowContext"/> from an instance of this, which is what keeps a recorded demo and a
/// test run the same journey through the app.
/// </summary>
public sealed class TransTruckSession : IAsyncDisposable
{
    /// <summary>
    /// Chrome's tab strip plus omnibox on a narrow window, measured rather than guessed: at phone
    /// widths Chromium stacks them and the band is 88px tall. Added to the mobile window height so
    /// the emulated page area ends up the phone's full height, rather than the phone's height minus
    /// the toolbar — which would crop the bottom tab bar out of every frame, the one control the
    /// mobile videos most need to show.
    ///
    /// Desktop no longer needs an allowance at all: a headed desktop run goes full screen, where
    /// there is no chrome to allow for.
    /// </summary>
    private const int MobileBrowserChromeHeight = 88;

    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _context;

    private TransTruckSession(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        Narrator narrator,
        AutomationOptions options,
        CaptureSize capture)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        Page = page;
        Narrator = narrator;
        Options = options;
        Capture = capture;
    }

    public IPage Page { get; }

    public Narrator Narrator { get; }

    public AutomationOptions Options { get; }

    /// <summary>What the camera sees, reported so the video pipeline can be set up to match.</summary>
    public CaptureSize Capture { get; }

    /// <summary>
    /// Launches the browser in the viewport the options ask for. The servers are expected to be up
    /// already — the UAT fixture and the DemoRunner both start them once for the whole run rather
    /// than once per session.
    /// </summary>
    public static async Task<TransTruckSession> StartAsync(
        AutomationOptions options,
        Action<string>? log = null)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await Playwright.CreateAsync();

            var (contextOptions, capture) = BuildContextOptions(playwright, options);

            browser = await BrowserProvisioning.LaunchChromiumAsync(
                playwright,
                new BrowserTypeLaunchOptions
                {
                    Headless = !options.Headed,
                    SlowMo = options.SlowMoMs,
                    // Sized to the viewport rather than maximised: with a fixed viewport a maximised
                    // window frames the page in dead space, which a window capture would record. On
                    // mobile this is what makes the recording phone-shaped instead of a narrow strip
                    // down the middle of a widescreen canvas.
                    // Only the phone-shaped window is positioned here. Desktop is put full screen
                    // after launch (see FrameTheWindowAsync) because the launch flags for it do not
                    // survive Playwright's own window sizing, and a --window-size that is about to
                    // be replaced is just noise in the command line.
                    Args = options.Headed && options.Viewport == Viewport.Mobile
                        ? new[]
                        {
                            $"--window-size={capture.WindowWidth},{capture.WindowHeight}",
                            "--window-position=0,0",
                        }
                        : null,
                },
                log);

            contextOptions.BaseURL = options.BaseUrl;
            contextOptions.Locale = "en-IN";
            // The seeded trips are dated against a fixed anchor, and the app formats dates for the
            // viewer's zone; pinning it keeps "28 Jul 2026" the same string on any machine.
            contextOptions.TimezoneId = "Asia/Kolkata";

            var context = await browser.NewContextAsync(contextOptions);

            // Next's dev-mode overlay renders into a <nextjs-portal> pinned to the bottom of the
            // viewport — precisely where this app puts its mobile tab bar — and it swallows the taps
            // aimed at the nav beneath it. Every mobile navigation then retries until it times out.
            //
            // It is a dev-server artifact and no part of the product, so it is taken out of the page
            // rather than worked around with offset clicks, which would stop matching what a user
            // actually does. An init script rather than a one-off injection, because the portal is
            // re-created after every client-side route change.
            await context.AddInitScriptAsync(
                "const hideDevOverlay = () => {" +
                "  const style = document.createElement('style');" +
                "  style.textContent = 'nextjs-portal { display: none !important; }';" +
                "  document.head.appendChild(style);" +
                "};" +
                "if (document.readyState === 'loading') {" +
                "  document.addEventListener('DOMContentLoaded', hideDevOverlay);" +
                "} else { hideDevOverlay(); }");

            var page = await context.NewPageAsync();

            if (options.Headed)
                capture = await FrameTheWindowAsync(context, page, options, capture, log);

            var narrator = new Narrator(page, options, log);

            log?.Invoke($"Browser ready — {options.Viewport}, page {capture.PageWidth}x{capture.PageHeight} CSS px @ {capture.DeviceScaleFactor}x");

            return new TransTruckSession(playwright, browser, context, page, narrator, options, capture);
        }
        catch
        {
            if (browser is not null) await browser.CloseAsync();
            playwright?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Desktop is a plain fixed viewport. Mobile comes from Playwright's own device descriptor, so
    /// user agent, viewport, device scale factor, touch support and the mobile flag all move together
    /// and stay consistent with each other. Setting a narrow viewport by hand would leave
    /// <c>navigator.maxTouchPoints</c> at zero and the user agent claiming Windows, and this app's
    /// layout keys off both — a "mobile" run that the app still believes is a desktop would prove
    /// nothing.
    /// </summary>
    private static (BrowserNewContextOptions Options, CaptureSize Capture) BuildContextOptions(
        IPlaywright playwright,
        AutomationOptions options)
    {
        if (options.Viewport == Viewport.Desktop)
        {
            return (
                new BrowserNewContextOptions
                {
                    // Explicit, and it stays explicit. Letting the page size itself off the window
                    // (NoViewport) is the obvious-looking simplification and it would quietly undo
                    // the property this whole layer rests on: a headless UAT run and a headed
                    // recording must lay out identically, or a scenario starts passing or failing
                    // according to the monitor it ran on. The window is made to match the viewport
                    // below — never the other way round.
                    ViewportSize = new ViewportSize { Width = options.DesktopWidth, Height = options.DesktopHeight },
                },
                // Window dimensions are filled in from the real window once it exists; a headed
                // desktop run goes full screen, so guessing them here would be guessing wrong.
                new CaptureSize(options.DesktopWidth, options.DesktopHeight, 1, options.DesktopWidth, options.DesktopHeight));
        }

        var descriptor = playwright.Devices.TryGetValue(options.MobileDevice, out var device)
            ? device
            : throw new InvalidOperationException(
                $"Playwright has no device descriptor named '{options.MobileDevice}'. " +
                $"Set TRANSTRUCK_UAT_MOBILE_DEVICE to one of: {string.Join(", ", playwright.Devices.Keys.Where(k => k.StartsWith("iPhone") || k.StartsWith("Pixel")).Take(12))}.");

        var width = descriptor.ViewportSize?.Width ?? 393;
        var height = descriptor.ViewportSize?.Height ?? 852;

        return (descriptor, new CaptureSize(width, height, descriptor.DeviceScaleFactor ?? 1f, width, height + MobileBrowserChromeHeight));
    }

    /// <summary>
    /// Puts the headed window where the camera wants it, and reports back what the camera will
    /// actually see — measured from the browser rather than assumed, because every guess available
    /// here turns out to be wrong.
    ///
    /// Desktop goes full screen, which is what removes the tab strip, the omnibox and the title bar:
    /// they live inside the client area, so no OBS capture mode can crop them away, and footage that
    /// is meant to be "the product" has a browser around it.
    ///
    /// Full screen is asked for over CDP rather than with --start-fullscreen, and that is not a
    /// preference. Measured on this machine, --start-fullscreen and --kiosk are both silently
    /// ignored once ViewportSize is set: Playwright sizes the OS window to fit the viewport it was
    /// given, in the "normal" window state, and overrides whatever the launch flags asked for. The
    /// flags leave chrome exactly where it was. Browser.setWindowBounds is applied after the window
    /// exists, so nothing overrides it afterwards, and it leaves the emulated viewport untouched —
    /// which is the whole point: the page still lays out at exactly the size the headless UAT run
    /// used.
    ///
    /// Mobile is deliberately left alone. A fullscreen window with a 393px viewport paints the phone
    /// into the corner of a blank 1920x1080 sheet — measured: chrome "height" becomes 421px of dead
    /// space. The phone-shaped window keeps its toolbar, and the recording crops it.
    /// </summary>
    private static async Task<CaptureSize> FrameTheWindowAsync(
        IBrowserContext context,
        IPage page,
        AutomationOptions options,
        CaptureSize capture,
        Action<string>? log)
    {
        try
        {
            var cdp = await context.NewCDPSessionAsync(page);

            var target = await cdp.SendAsync("Browser.getWindowForTarget");
            var windowId = target!.Value.GetProperty("windowId").GetInt32();

            if (options.Viewport == Viewport.Desktop)
            {
                await SetWindowStateAsync(cdp, windowId, "fullscreen");

                // Full screen is also the only way to learn the real display size: screen.width
                // inside the page is emulated along with everything else and just reports the
                // viewport back. So measure here, then decide whether to stay.
                var (screenWidth, screenHeight) = await ReadWindowSizeAsync(cdp);

                EnsureScreenFitsViewport(options, screenWidth, screenHeight);

                if (options.DesktopWidth == screenWidth && options.DesktopHeight == screenHeight)
                    return capture with { WindowWidth = screenWidth, WindowHeight = screenHeight };

                // A viewport deliberately smaller than the display: full screen would paint it into
                // the corner of a black sheet, which is worse to record than an ordinary window.
                // Back to a normal window — Playwright sizes it to the viewport plus chrome itself,
                // which is exactly what a smaller-than-screen recording wants.
                await SetWindowStateAsync(cdp, windowId, "normal");
                log?.Invoke(
                    $"Desktop viewport {options.DesktopWidth}x{options.DesktopHeight} is smaller than this " +
                    $"{screenWidth}x{screenHeight} display, so the window stays windowed and the browser's " +
                    "toolbar is in frame. Set TRANSTRUCK_UAT_DESKTOP_SIZE to the display size for chromeless capture.");
            }

            var (windowWidth, windowHeight) = await ReadWindowSizeAsync(cdp);
            return capture with { WindowWidth = windowWidth, WindowHeight = windowHeight };
        }
        catch (PlaywrightException ex)
        {
            // A window that could not be framed is still a usable recording, just with chrome in it.
            // Worth saying out loud, because the operator is about to point a camera at it.
            log?.Invoke($"Could not put the window full screen ({ex.Message.Split('\n')[0]}). " +
                        "Recording will include the browser's own toolbar.");
            return capture;
        }
    }

    private static async Task SetWindowStateAsync(ICDPSession cdp, int windowId, string state)
    {
        await cdp.SendAsync("Browser.setWindowBounds", new Dictionary<string, object>
        {
            ["windowId"] = windowId,
            ["bounds"] = new Dictionary<string, object> { ["windowState"] = state },
        });

        // The transition is animated; reading the bounds back immediately returns the old ones.
        await Task.Delay(900);
    }

    private static async Task<(int Width, int Height)> ReadWindowSizeAsync(ICDPSession cdp)
    {
        var bounds = (await cdp.SendAsync("Browser.getWindowForTarget"))!.Value.GetProperty("bounds");
        return (bounds.GetProperty("width").GetInt32(), bounds.GetProperty("height").GetInt32());
    }

    /// <summary>
    /// A viewport larger than the screen is clipped, not scaled — the right and bottom edges of the
    /// page simply never render, and the recording looks perfectly fine until someone notices the
    /// Save button is missing. Once full screen, the window bounds are the screen, so this is the
    /// one moment the real display size is knowable: screen.width inside the page is emulated along
    /// with everything else and reports the viewport back at you.
    /// </summary>
    private static void EnsureScreenFitsViewport(AutomationOptions options, int screenWidth, int screenHeight)
    {
        if (screenWidth >= options.DesktopWidth && screenHeight >= options.DesktopHeight)
            return;

        throw new InvalidOperationException(
            $"This display is {screenWidth}x{screenHeight}, smaller than the {options.DesktopWidth}x{options.DesktopHeight} " +
            "desktop viewport, so a full-screen recording would silently cut off the right and bottom of every frame. " +
            $"Record on a larger display, or set TRANSTRUCK_UAT_DESKTOP_SIZE={screenWidth}x{screenHeight} to match this one.");
    }

    /// <summary>
    /// Signs in through the real form — never by injecting a token — so the credential path and the
    /// post-login redirect are covered by every scenario that needs an authenticated screen.
    /// </summary>
    public async Task LoginAsync(string? phone = null, string? password = null)
    {
        await Page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        // Waited for explicitly rather than relying on the fill's own auto-wait: on a cold Next dev
        // server the first request compiles the route, and the React tree can take noticeably longer
        // to mount than a steady-state load. "The sign-in form never appeared" is also a clearer
        // diagnosis than a timeout inside an unrelated fill.
        var phoneField = Page.GetByLabel("Phone number");
        await Assertions.Expect(phoneField).ToBeVisibleAsync(new() { Timeout = 90_000 });

        // The form is server-rendered and then hydrated, and a value typed into the gap between
        // those two is written straight to the DOM node — where hydration promptly overwrites it
        // with the empty string the component rendered with. The field then looks filled, arrives
        // empty at the submit, and the app quite correctly answers "Enter your phone number and
        // password": a sign-in that fails with no visible cause, and only on a cold dev server,
        // which is exactly when a UAT run happens.
        //
        // So: settle first, then fill both, then re-check both immediately before clicking — the
        // check has to be the last thing before the submit, or hydration simply lands after it.
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 60_000 });

        var passwordField = Page.GetByLabel("Password", new() { Exact = true });
        var phone2 = phone ?? DemoData.OwnerPhone;
        var password2 = password ?? DemoData.OwnerPassword;

        for (var attempt = 1; ; attempt++)
        {
            await phoneField.FillAsync(phone2);
            await passwordField.FillAsync(password2);

            try
            {
                await Assertions.Expect(phoneField).ToHaveValueAsync(phone2, new() { Timeout = 2_000 });
                await Assertions.Expect(passwordField).ToHaveValueAsync(password2, new() { Timeout = 2_000 });
                break;
            }
            catch (PlaywrightException) when (attempt < 5)
            {
                await Page.WaitForTimeoutAsync(400);
            }
        }

        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        // Asserted on the destination, not on a navigation event: signing in is a client-side route
        // change, so WaitForURLAsync — which waits for a `load` that a SPA never fires — hangs until
        // its timeout even though the app arrived instantly.
        //
        // The landmark rather than the company name, because the name also appears in the welcome
        // toast: matching text would sometimes bind to a notification that fades while the assertion
        // is still retrying. The nav is the app shell itself — present in both viewports, as the
        // sidebar on desktop and the tab bar on a phone — so it means "signed in and inside the
        // product", which is what this actually needs to establish.
        await Assertions.Expect(Page.GetByRole(AriaRole.Navigation).First)
            .ToBeVisibleAsync(new() { Timeout = 60_000 });
    }

    /// <summary>Builds the context handed to a workflow.</summary>
    public WorkflowContext CreateWorkflowContext() => new(Page, Narrator, Options);

    /// <summary>
    /// Saves a screenshot under artifacts/uat, used for the closing frame of a workflow and for
    /// recording what the page looked like when a scenario failed. Named with the viewport, because
    /// the same workflow failing on a phone and passing on a desktop is the interesting case.
    /// </summary>
    public async Task<string> CaptureScreenshotAsync(string name)
    {
        Directory.CreateDirectory(RepoPaths.ArtifactsDir);

        var safe = string.Concat($"{name}-{Options.Viewport}".Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(RepoPaths.ArtifactsDir, $"{safe}.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _context.CloseAsync(); } catch { /* already closing */ }
        try { await _browser.CloseAsync(); } catch { /* already closing */ }
        _playwright.Dispose();
    }
}

/// <summary>
/// The frame the camera gets. <see cref="PageWidth"/>/<see cref="PageHeight"/> are CSS pixels the
/// page lays out in; <see cref="WindowWidth"/>/<see cref="WindowHeight"/> are what the OS window
/// occupies, which is what OBS's window capture is sized against.
/// </summary>
public readonly record struct CaptureSize(
    int PageWidth,
    int PageHeight,
    float DeviceScaleFactor,
    int WindowWidth,
    int WindowHeight)
{
    public override string ToString() => $"{PageWidth}x{PageHeight} @ {DeviceScaleFactor}x (window {WindowWidth}x{WindowHeight})";
}
