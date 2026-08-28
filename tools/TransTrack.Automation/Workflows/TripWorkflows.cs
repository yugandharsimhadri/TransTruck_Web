using Microsoft.Playwright;

namespace TransTrack.Automation.Workflows;

/// <summary>
/// The first screen after signing in, and the one an owner actually opens each morning: what is
/// still owed, what has been earned, and how the month is going.
/// </summary>
public sealed class DashboardWorkflow() : Workflow(
    key: "Dashboard",
    displayName: "The Morning Dashboard",
    module: "Overview",
    targetAudience: "Fleet owners",
    businessPurpose: "Answer the only question an owner has before the day starts — what money is still to come in — without opening a single trip.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Every session opens on the dashboard, headed by the company's own name.",
            async () =>
            {
                await c.NavigateAsync("Dashboard", DemoData.CompanyName);
                await c.BeatAsync();
            });

        await c.StepAsync(
            "The headline is the balance still to collect across every open trip.",
            () => c.ExpectVisibleAsync("Still to collect"));

        await c.StepAsync(
            "Underneath it, what the fleet earned and what it spent this month.",
            async () =>
            {
                await c.ExpectVisibleAsync("Earned");
                await c.ExpectVisibleAsync("Spent");
            });

        await c.StepAsync(
            "And the month's trip count, which opens the list in one tap.",
            () => c.ExpectVisibleAsync("Trips this month"));
    }
}

/// <summary>
/// Finding a trip among hundreds. The list is paged and filtered by the database rather than in the
/// browser, so a filter searches every trip on record, not just the ones already on screen.
/// </summary>
public sealed class BrowseTripsWorkflow() : Workflow(
    key: "BrowseTrips",
    displayName: "Finding a Trip",
    module: "Trips",
    targetAudience: "Office staff and fleet owners",
    businessPurpose: "Get to any trip in seconds however many years of history there are, by narrowing on status, lorry or order.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "The trips list opens on the most recent work, newest first.",
            async () =>
            {
                await c.NavigateAsync("Trips", "Trips");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "It loads a page at a time and says how many trips are behind it.",
            () => c.ExpectVisibleAsync("Showing"));

        await c.StepAsync(
            "One tap brings in the next page — the count keeps the total in view.",
            async () =>
            {
                var loadMore = c.Button("Load more trips");
                await WorkflowContext.Expect(loadMore).ToBeVisibleAsync();
                await loadMore.ClickAsync();
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Narrowing to closed trips searches every trip on record, not just the ones on screen.",
            async () =>
            {
                await SelectStatusAsync(c, "Closed");

                // The demo fleet is all still running, so the honest thing for this filter to show
                // is nothing — and that is the assertion. Checking for a count instead would be
                // checking for the wrong screen: the list renders its empty state, not "Showing 0
                // of 0", when a filter matches no trips.
                await c.ExpectVisibleAsync("No trips here yet");
            });

        await c.StepAsync(
            "Back to the open ones, which is where the money still is.",
            () => SelectStatusAsync(c, "Open"));
    }

    /// <summary>
    /// The status control is the same component in both viewports — a listbox, not a native select —
    /// so this needs no branch. It is a helper only because three steps use it.
    /// </summary>
    private static async Task SelectStatusAsync(WorkflowContext c, string status)
    {
        await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Combobox)).ClickAsync();
        await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Option, new() { Name = status, Exact = true })).ClickAsync();
    }
}

/// <summary>
/// A trip's own screen: what it earned, what it cost, and what is still owed on it. This is the
/// record every other number in the product is derived from.
/// </summary>
public sealed class TripDetailWorkflow() : Workflow(
    key: "TripDetail",
    displayName: "Inside a Trip",
    module: "Trips",
    targetAudience: "Office staff and fleet owners",
    businessPurpose: "Hold one lorry-load's whole story in one place — the load, the freight agreed, the diesel and tolls against it, and what the party has actually paid.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Opening a trip from the list.",
            async () =>
            {
                await c.NavigateAsync("Trips", "Trips");
                await c.OpenFirstTripAsync();
                await c.BeatAsync();
            });

        await c.StepAsync(
            "The trip carries its own number, and its status says whether the money is still open.",
            async () =>
            {
                // The trip number rather than the lorry: the registration sits inside the vehicle
                // picker's button, where it is the control's current value rather than text on the
                // page, and asserting it would be asserting about a form control instead of about
                // what the screen says.
                await c.ExpectVisibleAsync("Trip TRP");
                await c.ExpectVisibleAsync("Open");
            });

        await c.StepAsync(
            "Below it the running total of what this trip has cost so far.",
            () => c.ExpectVisibleAsync("Net after expenses"));

        await c.StepAsync(
            "And the balance still receivable, which is what the dashboard adds up.",
            () => c.ExpectVisibleAsync("Balance receivable"));
    }
}
