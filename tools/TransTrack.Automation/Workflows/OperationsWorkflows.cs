using Microsoft.Playwright;

namespace TransTrack.Automation.Workflows;

/// <summary>
/// The lorries, drivers and parties everything else refers to. Nothing can be booked until these
/// exist, which is why it is the first thing a new customer is walked through.
/// </summary>
public sealed class MastersWorkflow() : Workflow(
    key: "VehiclesAndContacts",
    displayName: "Lorries, Drivers and Parties",
    module: "Vehicles & Contacts",
    targetAudience: "Fleet owners setting up",
    businessPurpose: "Register the fleet and the people it works with once, so booking a trip is choosing from a list rather than retyping a lorry number.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Vehicles and Contacts holds everything the rest of the product refers to.",
            async () =>
            {
                await c.NavigateAsync("Vehicles & Contacts", "Vehicles & Contacts");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "The fleet is listed with its registration numbers and status.",
            () => c.ExpectVisibleAsync(DemoData.VehicleRegNo));

        await c.StepAsync(
            "Drivers are a tab across, each with the phone number the office rings.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = "Drivers" })).ClickAsync();
                await c.ExpectVisibleAsync(DemoData.DriverName);
            });

        await c.StepAsync(
            "And the parties whose goods the lorries carry.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = "Parties" })).ClickAsync();
                await c.ExpectVisibleAsync(DemoData.PartyName);
            });

        await c.StepAsync(
            "The places routes run between are kept here too, so a route reads the same on every trip.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = "Places" })).ClickAsync();
                await c.BeatAsync();
            });
    }
}

/// <summary>
/// Money going out against a trip. Expenses are what turn a freight figure into an actual margin,
/// and they are entered against the trip so they can never drift away from it.
/// </summary>
public sealed class RecordExpenseWorkflow() : Workflow(
    key: "RecordExpense",
    displayName: "Recording What a Trip Cost",
    module: "Trips",
    targetAudience: "Office staff",
    businessPurpose: "Put diesel, tolls and driver advances against the trip that incurred them, so the margin on that load is the real one.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Starting from a trip that is still open.",
            async () =>
            {
                await c.NavigateAsync("Trips", "Trips");
                await c.OpenFirstTripAsync();
            });

        await c.StepAsync(
            "Adding an expense opens its own screen, so nothing on the trip is disturbed.",
            async () =>
            {
                await c.LinkTo("/expenses/new").ClickAsync();
                await WorkflowContext.Expect(c.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/expenses/new"));
            });

        await c.StepAsync(
            "What it was for is picked from the categories the reports total by — no free typing.",
            () => c.Page.GetByRole(AriaRole.Button, new() { Name = "Fuel", Exact = true }).ClickAsync());

        await c.StepAsync(
            "The amount goes in, with the common figures a tap away for a driver at a pump.",
            async () =>
            {
                await c.Page.GetByLabel("Amount").FillAsync("4500");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Saved, and the trip's costs and margin move with it.",
            async () =>
            {
                // The submit button is named for what it does — "Add expense", the same words as the
                // link that opened the screen — rather than a generic Save.
                await c.Button("Add expense", exact: true).ClickAsync();
                await WorkflowContext.Expect(c.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/trips/[0-9a-f-]{36}$"));
                await c.ExpectVisibleAsync("Net after expenses");
            });
    }
}

/// <summary>
/// Money coming in, and the check on it. An amount recorded against a trip does not count until the
/// owner approves it — the control that stops the books being written by whoever happens to be at
/// the desk.
/// </summary>
public sealed class ApprovalsWorkflow() : Workflow(
    key: "ApproveReceipts",
    displayName: "Approving Money Received",
    module: "Approvals",
    targetAudience: "Fleet owners",
    businessPurpose: "Let staff record a payment the moment it arrives, while the figure that reaches the books is one the owner has seen and approved.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Recording a payment starts on the trip the party paid against.",
            async () =>
            {
                await c.NavigateAsync("Trips", "Trips");
                await c.OpenFirstTripAsync();
                await c.LinkTo("/amount/new").ClickAsync();
                await WorkflowContext.Expect(c.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/amount/new"));
            });

        await c.StepAsync(
            "The amount is entered as it was received, and how it was paid.",
            async () =>
            {
                await c.Page.GetByLabel("Amount").FillAsync("6000");
                await c.Button("Cash", exact: true).ClickAsync();
            });

        await c.StepAsync(
            "Saved — but it is not counted yet. It goes to the owner as pending.",
            async () =>
            {
                await c.Button("Add amount", exact: true).ClickAsync();
                await WorkflowContext.Expect(c.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/trips/[0-9a-f-]{36}$"));
            });

        await c.StepAsync(
            "The owner's Approvals screen is where those entries wait.",
            async () =>
            {
                await c.NavigateAsync("Approvals", "Approvals");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Approving it is what lets the money count towards the trip's balance.",
            async () =>
            {
                var approve = c.Button("Approve");
                await WorkflowContext.Expect(approve).ToBeVisibleAsync();
                await approve.ClickAsync();
                await c.BeatAsync();
            });
    }
}

/// <summary>
/// What the business is worth on paper. Reports read the same figures the screens do, filtered to a
/// period, and go out as PDF or Excel for an accountant who has never seen the product.
/// </summary>
public sealed class ReportsWorkflow() : Workflow(
    key: "Reports",
    displayName: "Reports for the Accountant",
    module: "Reports",
    targetAudience: "Fleet owners and their accountants",
    businessPurpose: "Turn a period's trips into the statement an accountant asks for, without anyone re-keying it into a spreadsheet.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Reports gathers the period views in one place.",
            async () =>
            {
                await c.NavigateAsync("Reports", "Reports");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "The trips report lists the period's work with its money alongside.",
            async () =>
            {
                await c.BeatAsync();
                await WorkflowContext.Expect(c.Page.GetByRole(AriaRole.Tab).First).ToBeVisibleAsync();
            });

        await c.StepAsync(
            "Every report exports as PDF or Excel from the same screen.",
            () => c.ExpectVisibleAsync("Excel"));
    }
}

/// <summary>
/// Keeping the lorries legal and on the road. Permits, insurance and fitness all expire, and the
/// product's job is to say so before a vehicle is stopped at a check post.
/// </summary>
public sealed class MaintenanceWorkflow() : Workflow(
    key: "Maintenance",
    displayName: "Keeping Lorries on the Road",
    module: "Maintenance",
    targetAudience: "Fleet owners",
    businessPurpose: "Track servicing and the documents that expire, so a lorry is never stopped for a lapsed permit nobody was watching.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Maintenance records what has been spent keeping each lorry running.",
            async () =>
            {
                await c.NavigateAsync("Maintenance", "Maintenance");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Entries are held per lorry, so a vehicle's true running cost is visible.",
            () => c.BeatAsync());
    }
}

/// <summary>
/// What each driver has taken and what is still owed to them. Advances against a trip and the
/// running balance behind them, so a driver's account is settled from a record rather than memory.
/// </summary>
public sealed class DriverLedgerWorkflow() : Workflow(
    key: "DriverLedger",
    displayName: "The Driver's Account",
    module: "Driver Ledger",
    targetAudience: "Fleet owners and office staff",
    businessPurpose: "Settle with a driver from a running record of advances and dues rather than from a notebook and an argument.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "The driver ledger opens on the people who drive for the company.",
            async () =>
            {
                await c.NavigateAsync("Driver Ledger", "Driver Ledger");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Each driver's advances and dues are held as a running account.",
            () => c.BeatAsync());
    }
}

/// <summary>
/// The record of who changed what. Every figure that matters is stamped with the person and the
/// moment, which is what makes the numbers defensible after the fact.
/// </summary>
public sealed class ActivityWorkflow() : Workflow(
    key: "ActivityTrail",
    displayName: "Who Changed What",
    module: "Activity",
    targetAudience: "Fleet owners",
    businessPurpose: "Answer 'who changed this figure, and when' from the product itself, so a disputed number has an answer rather than an argument.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Activity is the running record of every change made in the product.",
            async () =>
            {
                await c.NavigateAsync("Activity", "Activity");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Each entry names the person, the moment and what moved.",
            () => c.BeatAsync());
    }
}
