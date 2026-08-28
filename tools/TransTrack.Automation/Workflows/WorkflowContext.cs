using Microsoft.Playwright;

namespace TransTrack.Automation.Workflows;

/// <summary>
/// Everything a workflow needs to drive the app, plus the handful of navigation and verification
/// helpers every workflow repeats. Verification uses Playwright's own web-first assertions, which
/// retry until the timeout — so the same step works at test speed and at demo speed without
/// sprinkling waits through the scenarios.
/// </summary>
public sealed class WorkflowContext(IPage page, Narrator narrator, AutomationOptions options)
{
    public IPage Page { get; } = page;

    public Narrator Narrator { get; } = narrator;

    public AutomationOptions Options { get; } = options;

    /// <summary>Which screen shape this run is on. Workflows branch on this where the journey genuinely differs.</summary>
    public Viewport Viewport => Options.Viewport;

    public bool IsMobile => Options.Viewport == Viewport.Mobile;

    /// <summary>True when the run is producing footage rather than a pass/fail result.</summary>
    public bool IsRecording => Options.RunMode != RunMode.Test;

    /// <summary>Narrates a step, then runs it. The caption is on screen before the action it describes.</summary>
    public async Task StepAsync(string narration, Func<Task> action)
    {
        await Narrator.SayAsync(narration);
        await action();
    }

    /// <summary>Narrates a beat that has no interaction of its own — used to explain what is on screen.</summary>
    public Task SayAsync(string narration) => Narrator.SayAsync(narration);

    /// <summary>Playwright's retrying assertion for a locator.</summary>
    public static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    /// <summary>Playwright's retrying assertion for the page (URL, title).</summary>
    public static IPageAssertions Expect(IPage page) => Assertions.Expect(page);

    /// <summary>
    /// Asserts a piece of text is on screen.
    ///
    /// Filtered to visible before First(), and that is the whole point. This app ships both layouts
    /// in the DOM and hides one with a breakpoint — the desktop table and the mobile card both
    /// exist, as do the sidebar and the tab bar. A plain First() therefore keeps binding to the
    /// layout that is not on screen, and waits out its timeout for a visibility that will never
    /// come. Filtering first asks the question the check means: can the user see this.
    /// </summary>
    public Task ExpectVisibleAsync(string text)
        => Expect(Visible(Page.GetByText(text))).ToBeVisibleAsync();

    /// <summary>The visible one of a set of matches — see <see cref="ExpectVisibleAsync"/> for why.</summary>
    public static ILocator Visible(ILocator locator)
        => locator.Filter(new LocatorFilterOptions { Visible = true }).First;

    /// <summary>The visible link with this name. Same reasoning as <see cref="Visible"/>.</summary>
    public ILocator Link(string name)
        => Visible(Page.GetByRole(AriaRole.Link, new() { Name = name }));

    /// <summary>
    /// The visible control that navigates to a path ending in <paramref name="hrefSuffix"/>.
    ///
    /// By destination rather than by role, because this app's call-to-action controls are anchors
    /// rendered through the Button component, which gives them role="button" — so "Add expense"
    /// looks like a link, behaves like a link, and is not findable as one. The href is the thing
    /// that is actually stable: it is the route, and the route is the product's own contract.
    /// </summary>
    public ILocator LinkTo(string hrefSuffix)
        => Visible(Page.Locator($"a[href$='{hrefSuffix}']"));

    /// <summary>The visible button with this name.</summary>
    public ILocator Button(string name, bool exact = false)
        => Visible(Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = exact }));

    /// <summary>Asserts the page heading, which is how every screen in this product announces itself.</summary>
    public Task ExpectHeadingAsync(string heading)
        => Expect(Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First).ToBeVisibleAsync();

    /// <summary>
    /// Moves to one of the product's screens by clicking, never by typing a URL — a navigation that
    /// only works when driven from the address bar is not a navigation a customer has.
    ///
    /// This is the sharpest of the two viewports' differences, and the reason the branch exists at
    /// all rather than a shared selector: the desktop sidebar lists all nine screens, while the phone
    /// carries four in its bottom tab bar and files the rest behind "More". On mobile the secondary
    /// screens therefore take two taps, through a screen that does not exist on desktop.
    /// </summary>
    public async Task NavigateAsync(string label, string expectedHeading)
    {
        if (IsMobile && !MobileTabBarLabels.Contains(label))
        {
            // Two taps, through a screen the desktop does not have at all. "More" is itself a tab,
            // so it is found in the nav landmark; what it opens is a page of cards, not a menu — so
            // the second tap has to look at the page, not at a nav. Scoping the second lookup to a
            // navigation landmark (as the desktop path does) finds nothing and waits out its
            // timeout, which is precisely what this branch is here to avoid.
            await Visible(Page.GetByRole(AriaRole.Navigation)
                .GetByRole(AriaRole.Link, new() { Name = "More", Exact = true })).ClickAsync();
            await ExpectHeadingAsync("More");

            await Visible(Page.GetByRole(AriaRole.Link, new() { Name = label, Exact = true })).ClickAsync();
            await ExpectHeadingAsync(expectedHeading);
            return;
        }

        // Scoped to a navigation landmark: "Reports" is also the text of buttons and headings on the
        // pages themselves, and an unscoped link lookup goes strict-mode ambiguous the moment a page
        // links onward to a screen the nav also lists.
        await Visible(Page.GetByRole(AriaRole.Navigation)
            .GetByRole(AriaRole.Link, new() { Name = label, Exact = true })).ClickAsync();

        await ExpectHeadingAsync(expectedHeading);
    }

    /// <summary>
    /// The four screens the phone's bottom tab bar carries directly, from
    /// <c>nav-items.ts: primaryNavItems</c>. Everything else is behind "More" on a phone.
    /// </summary>
    private static readonly HashSet<string> MobileTabBarLabels =
        new(StringComparer.Ordinal) { "Dashboard", "Trips", "Approvals", "Maintenance" };

    /// <summary>
    /// Opens the first trip in the list and waits for its detail screen. Both viewports render the
    /// list as the same stack of cards, so this needs no branch — worth saying, because it is the
    /// screen where one would most expect a table/card split.
    /// </summary>
    public async Task OpenFirstTripAsync()
    {
        // Excluding /trips/new is the whole trick: "New trip" is also an anchor under /trips/ and it
        // sits above the rows, so the bare prefix selector opens the booking form instead of a trip
        // and every assertion afterwards is about the wrong screen.
        await Visible(Page.Locator("a[href^='/trips/']:not([href='/trips/new'])")).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/trips/[0-9a-f-]{36}"));
    }

    /// <summary>
    /// A deliberate pause with no caption, for letting a list finish rendering on camera. Skipped
    /// under test, where waiting on a locator is both faster and more reliable.
    /// </summary>
    public Task BeatAsync(int milliseconds = 700) => Narrator.BeatAsync(milliseconds);
}
