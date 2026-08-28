using TransTrack.Automation;
using Xunit.Abstractions;

namespace TransTrack.UatTests;

/// <summary>
/// The first screen of the working day. A fleet owner opens TransTruck to answer one question —
/// how much money is still to come in — and the dashboard answers it before any trip is opened:
/// the outstanding balance across every open trip, what the fleet earned and spent this month, and
/// how many loads it has run.
/// </summary>
public sealed class DashboardUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task The_morning_view_of_the_business(Viewport viewport) => RunWorkflowAsync("Dashboard", viewport);
}

/// <summary>
/// Finding one lorry-load among years of them. The trips list is paged, and — this is the part that
/// matters to a transport office — the status, lorry and ordering are applied by the database across
/// every trip on record, not just the page on screen. Asking for closed trips finds a trip closed
/// two years ago, not merely the closed ones among the most recent twenty-five.
/// </summary>
public sealed class TripListUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Finding_a_trip_among_many(Viewport viewport) => RunWorkflowAsync("BrowseTrips", viewport);
}

/// <summary>
/// One trip's complete record. A load carries the freight agreed with the party, the diesel, tolls
/// and advances spent against it, and whatever the party has actually paid — and the trip screen is
/// where those three meet. Every figure the dashboard and the reports show is derived from here, so
/// this screen is the product's source of truth about money.
/// </summary>
public sealed class TripDetailUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task A_trips_freight_costs_and_balance(Viewport viewport) => RunWorkflowAsync("TripDetail", viewport);
}

/// <summary>
/// Recording what a load actually cost. Diesel, tolls and driver advances are entered against the
/// trip that incurred them rather than into a general expense book, which is what makes the margin
/// on a particular lorry-load a real number instead of an end-of-month guess.
/// </summary>
public sealed class TripExpenseUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Putting_costs_against_the_trip_that_caused_them(Viewport viewport) => RunWorkflowAsync("RecordExpense", viewport);
}
