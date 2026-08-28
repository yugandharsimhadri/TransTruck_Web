using TransTrack.Automation;
using Xunit.Abstractions;

namespace TransTrack.UatTests;

/// <summary>
/// Getting into TransTruck, and being kept out of it. Every screen in the product sits behind a
/// login tied to a phone number, so a transport office can hand a lorry's paperwork to a clerk
/// without handing over the company's money. A wrong password says only that it was wrong, and a
/// correct one lands the user on their own company's dashboard — never anyone else's.
/// </summary>
public sealed class SignInUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Signing_in_and_being_refused(Viewport viewport) => RunWorkflowAsync("SignIn", viewport);
}

/// <summary>
/// Who in the business can reach what. TransTruck's roles are a hierarchy rather than a flat list:
/// an owner can see and approve everything, a co-owner nearly everything, and office staff are kept
/// to the day's data entry. The screens that carry authority — approvals above all — are only
/// reachable by the roles that hold it.
/// </summary>
public sealed class UserAccessUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Staff_accounts_and_the_screens_they_reach(Viewport viewport) => RunWorkflowAsync("UserAccess", viewport);
}
