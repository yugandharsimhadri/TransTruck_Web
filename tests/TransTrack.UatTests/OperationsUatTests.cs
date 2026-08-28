using TransTrack.Automation;
using Xunit.Abstractions;

namespace TransTrack.UatTests;

/// <summary>
/// The fleet and the people it works with. Lorries, drivers, parties and the cities routes run
/// between are registered once and then chosen from everywhere else, so a registration number is
/// typed once in its life rather than on every trip sheet — and spelled the same way on every
/// document that leaves the office.
/// </summary>
public sealed class MastersUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task The_fleet_drivers_parties_and_routes(Viewport viewport) => RunWorkflowAsync("VehiclesAndContacts", viewport);
}

/// <summary>
/// Money coming in, and the owner's check on it. Staff record a payment the moment a party pays, but
/// the figure does not count towards the trip's balance until the owner has approved it. That gap is
/// the control: the books are written by the owner's decision, not by whoever happened to be at the
/// desk when the cash arrived.
/// </summary>
public sealed class ApprovalUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Recording_a_receipt_and_approving_it(Viewport viewport) => RunWorkflowAsync("ApproveReceipts", viewport);
}

/// <summary>
/// Keeping the lorries earning. Servicing costs are held per vehicle so an owner can see which lorry
/// is quietly eating its own margin, and the documents that expire — permit, insurance, fitness,
/// pollution — are tracked so a vehicle is never stopped at a check post for a lapse nobody was
/// watching.
/// </summary>
public sealed class MaintenanceUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Servicing_and_expiring_documents(Viewport viewport) => RunWorkflowAsync("Maintenance", viewport);
}

/// <summary>
/// What is owed to each driver. Advances taken against a trip and settlements paid back are held as
/// a running account per driver, so the end-of-month conversation is settled from a record both
/// sides can read rather than from a notebook and a memory.
/// </summary>
public sealed class DriverLedgerUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task A_drivers_running_account(Viewport viewport) => RunWorkflowAsync("DriverLedger", viewport);
}

/// <summary>
/// What the owner hands to the accountant. Reports gather a period's trips, expenses and receipts
/// into the statements a transport business is actually asked for, and export them as PDF or Excel —
/// so the year-end conversation starts from the product's own figures rather than from a spreadsheet
/// somebody re-keyed.
/// </summary>
public sealed class ReportUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Period_statements_and_their_exports(Viewport viewport) => RunWorkflowAsync("Reports", viewport);
}

/// <summary>
/// The record of who changed what. Every figure that carries money is stamped with the person who
/// entered it and the moment they did, and the activity screen makes that readable — so a disputed
/// amount three months later has an answer in the product rather than an argument in the office.
/// </summary>
public sealed class ActivityUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task The_trail_behind_every_figure(Viewport viewport) => RunWorkflowAsync("ActivityTrail", viewport);
}
