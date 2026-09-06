using Microsoft.Playwright;

namespace TransTrack.Automation.Workflows;

/// <summary>
/// The fleet itself. Its own destination rather than a tab beside the people records, because a
/// lorry carries a good deal more than a name and a number — finance terms and the recurring
/// paperwork costs both live on it.
/// </summary>
public sealed class VehiclesWorkflow() : Workflow(
    key: "Vehicles",
    displayName: "The Fleet",
    module: "Vehicles",
    targetAudience: "Fleet owners setting up",
    businessPurpose: "Keep every lorry's registration, papers, loan and running costs in one place, so what a vehicle actually costs to own is answerable.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Vehicles is its own destination, listing the fleet by registration number.",
            async () =>
            {
                await c.NavigateAsync("Vehicles", "Vehicles");
                await c.ExpectVisibleAsync(DemoData.VehicleRegNo);
            });

        await c.StepAsync(
            "Picking a lorry opens its own screen, headed by its number.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByText(DemoData.VehicleRegNo)).ClickAsync();
                await c.ExpectHeadingAsync(DemoData.VehicleRegNo);
            });

        await c.StepAsync(
            "What a lorry is, what its papers say, and what is owed on it are three "
            + "separate tabs — they used to be one dialog you scrolled.",
            async () =>
            {
                foreach (var tab in new[] { "Details", "Papers", "Loan" })
                    await WorkflowContext.Expect(
                        WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = tab })))
                        .ToBeVisibleAsync();
            });

        await c.StepAsync(
            "The papers tab holds the expiry dates and the recurring costs together.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = "Papers" })).ClickAsync();
                await c.ExpectVisibleAsync("Valid until");
                await c.ExpectVisibleAsync("Insurance, road tax & other papers");
            });

        await c.StepAsync(
            "And the loan tab says plainly when there is nothing owed, rather than "
            + "showing five empty boxes.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = "Loan" })).ClickAsync();
                await c.ExpectVisibleAsync("EMI day of month");
            });

        await c.StepAsync(
            "The way back out is at the top of the screen, and stays there while you scroll.",
            () => WorkflowContext.Expect(c.Link("Back")).ToBeVisibleAsync());
    }
}

/// <summary>
/// The people and places everything else refers to. Nothing can be booked until these exist, which
/// is why it is the first thing a new customer is walked through.
/// </summary>
public sealed class MastersWorkflow() : Workflow(
    key: "DriversAndParties",
    displayName: "Drivers, Parties and Places",
    module: "Drivers & Parties",
    targetAudience: "Fleet owners setting up",
    businessPurpose: "Register the people the fleet works with once, so booking a trip is choosing from a list rather than retyping a name.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Drivers and Parties holds the people the rest of the product refers to.",
            async () =>
            {
                await c.NavigateAsync("Drivers & Parties", "Drivers & Parties");
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Drivers come first, each with the phone number the office rings.",
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

/// <summary>
/// Every screen has a visible way out, at the top, without scrolling.
///
/// Worth its own scenario because it is a property of the whole product rather
/// than of any one screen, and the way it breaks is by omission: a new screen
/// added later simply won't have one, and nothing else in this suite would
/// notice. The dashboard is the deliberate exception — it is home, and an arrow
/// there would lead out of the app.
/// </summary>
public sealed class WayOutWorkflow() : Workflow(
    key: "WayOut",
    displayName: "A Way Out of Every Screen",
    module: "Navigation",
    targetAudience: "Anyone on a phone",
    businessPurpose: "Never strand someone on a screen. The control that leaves it is in the same place every time, and is on screen before you scroll.")
{
    /// The nav label of each destination, paired with the heading that proves
    /// the screen actually loaded.
    private static readonly (string Label, string Heading)[] Destinations =
    [
        ("Trips", "Trips"),
        ("Maintenance", "Maintenance"),
        ("Driver Ledger", "Driver Ledger"),
        ("Reports", "Reports"),
        ("Party Bills", "Party Bills"),
        ("Vehicles", "Vehicles"),
        ("Drivers & Parties", "Drivers & Parties"),
        ("Settings", "Settings"),
    ];

    public override async Task RunAsync(WorkflowContext c)
    {
        foreach (var (label, heading) in Destinations)
        {
            await c.StepAsync(
                $"{heading} can be left from the top of the screen.",
                async () =>
                {
                    await c.NavigateAsync(label, heading);
                    await WorkflowContext.Expect(c.Link("Back")).ToBeVisibleAsync();
                });
        }

        await c.StepAsync(
            "Recording an amount offers a cross rather than an arrow — leaving it "
            + "abandons what you were typing, which is a different promise from going back.",
            async () =>
            {
                await c.NavigateAsync("Trips", "Trips");
                await c.OpenFirstTripAsync();
                await c.LinkTo("/amount/new").ClickAsync();
                await WorkflowContext.Expect(c.Link("Close")).ToBeVisibleAsync();
            });
    }
}

/// <summary>
/// A party pays for a month's loads with one transfer.
///
/// The journey this replaces was twenty trips opened one at a time, twenty
/// receipts typed, twenty approvals clicked and twenty trips closed — with no
/// figure anywhere that said what the party had actually paid. Here it is one
/// list, one total, and one decision.
///
/// Worth walking end to end rather than testing the pieces, because the thing
/// that matters is the join: money posted and trips closed, together, only
/// once the Owner has said yes.
/// </summary>
public sealed class BulkSettlementWorkflow() : Workflow(
    key: "BulkSettlement",
    displayName: "Settling a Month in One Payment",
    module: "Bulk Settlement",
    targetAudience: "Office staff and fleet owners",
    businessPurpose: "Clear a party's whole month against one payment, confirmed as a single total and approved once, instead of settling and closing every trip by hand.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Bulk Settlement starts from the party, not from a trip.",
            async () =>
            {
                await c.NavigateAsync("Bulk Settlement", "Bulk Settlement");
                await c.ExpectVisibleAsync("Choose a party to see everything they still owe.");
            });

        await c.StepAsync(
            "Picking them lists every open trip with anything left to pay.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Button, new() { Name = "Choose" })).ClickAsync();
                await WorkflowContext.Visible(c.Page.GetByPlaceholder("Search party…")).FillAsync(DemoData.PartyName);
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Button, new() { Name = DemoData.PartyName })).ClickAsync();
            });

        await c.StepAsync(
            "The period narrows it to the month being paid for — cleared here, so "
            + "everything the party still owes is on the table.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByLabel("Trips from")).FillAsync("");
                await WorkflowContext.Visible(c.Page.GetByLabel("Trips to")).FillAsync("");

                // "Select all" only exists when there is something to select, so
                // its presence is the unambiguous proof the list has rows —
                // unlike the row count, whose wording the empty state shares.
                await WorkflowContext.Expect(c.Button("Select all")).ToBeVisibleAsync();
            });

        await c.StepAsync(
            "Trips are ticked off — one tap each, or the whole month at once.",
            async () =>
            {
                // Deliberately one trip rather than Select all: this scenario
                // runs twice, once per viewport, against one seeded company, and
                // settling the lot would leave the second run nothing to settle.
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Button, new() { Name = "TRP" })).ClickAsync();
                await c.ExpectVisibleAsync("trip selected");
            });

        await c.StepAsync(
            "The total is on screen while the ticking happens — it is what the "
            + "owner will be asked to approve, so it is never a surprise.",
            () => WorkflowContext.Expect(c.Button("Settle & close")).ToBeVisibleAsync());

        await c.StepAsync(
            "Confirming states the figure in full, and says plainly that nothing "
            + "closes until it is approved.",
            async () =>
            {
                await c.Button("Settle & close").ClickAsync();
                await c.ExpectVisibleAsync("Total received from");
                await WorkflowContext.Visible(
                    c.Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" })).ClickAsync();
            });

        await c.StepAsync(
            "It reaches the owner as one approval carrying the whole amount, "
            + "not as a queue of receipts to work through.",
            async () =>
            {
                await c.NavigateAsync("Approvals", "Approvals");
                await c.ExpectVisibleAsync("Bulk settlements");
                await WorkflowContext.Expect(c.Button("Approve & close")).ToBeVisibleAsync();
            });

        await c.StepAsync(
            "Approving posts the money against every trip and closes them all together.",
            async () =>
            {
                await c.Button("Approve & close").ClickAsync();
                await c.NavigateAsync("Trips", "Trips");
                await c.BeatAsync();
            });
    }
}
