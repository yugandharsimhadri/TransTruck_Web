using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace TransTrack.Automation.Workflows;

/// <summary>
/// The way in, and the way the system keeps everyone else out. Runs from an already-signed-in state
/// (as every workflow does) by signing out first, so it can show the whole door: a rejected
/// credential and then a successful sign-in.
/// </summary>
public sealed class SignInWorkflow() : Workflow(
    key: "SignIn",
    displayName: "Signing In",
    module: "Security",
    targetAudience: "Everyone who uses TransTruck",
    businessPurpose: "Put every screen behind a named login, so only your own staff can see your trips, your parties and your money — and every entry is tied to the person who made it.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Signing out returns to the sign-in screen — the only way into the product.",
            async () =>
            {
                await SignOutAsync(c);
                await WorkflowContext.Expect(c.Page).ToHaveURLAsync(new Regex(@"/login"));
                await c.ExpectVisibleAsync("Fleet & trip management");
            });

        await c.StepAsync(
            "A wrong password is refused, and says only that the credentials are wrong.",
            async () =>
            {
                await c.Page.GetByLabel("Phone number").FillAsync(DemoData.OwnerPhone);
                await c.Page.GetByLabel("Password", new() { Exact = true }).FillAsync("not-the-password");
                await c.Button("Sign in").ClickAsync();
                await c.ExpectVisibleAsync("Incorrect username or password.");
            });

        await c.StepAsync(
            "The right phone number and password let the owner through to their dashboard.",
            async () =>
            {
                await c.Page.GetByLabel("Password", new() { Exact = true }).FillAsync(DemoData.OwnerPassword);
                await c.Button("Sign in").ClickAsync();
                // Same reason as TransTruckSession.LoginAsync: a client-side route change fires no
                // load event, and the company name also lands in a toast that fades — so arrival is
                // asserted on the app shell's own navigation.
                await WorkflowContext.Expect(c.Page.GetByRole(AriaRole.Navigation).First)
                    .ToBeVisibleAsync(new() { Timeout = 60_000 });
            });

        await c.StepAsync(
            c.IsMobile
                ? "The company's own name heads the dashboard they land on."
                : "The company's name sits under the product name in the sidebar, on every screen from here on.",
            () =>
            {
                // A real difference, not a selector quirk. The desktop sidebar carries the company
                // name permanently beneath the product name; the phone's top bar has room for one of
                // the two and keeps the product's, so on mobile the company name is the dashboard's
                // own heading. Asserting the desktop element on a phone finds it in the DOM — the
                // sidebar is rendered and hidden by a breakpoint — and waits for a visibility that
                // never comes, which is the failure this branch exists to prevent.
                return c.IsMobile
                    ? c.ExpectHeadingAsync(DemoData.CompanyName)
                    : c.ExpectVisibleAsync(DemoData.CompanyName);
            });
    }

    /// <summary>
    /// Signing out is the clearest place the two viewports diverge, so it is worth showing rather
    /// than hiding in a helper: the desktop keeps a Sign out button in the sidebar, while the phone
    /// files it behind the avatar menu in the top bar — the same menu that shows who is signed in.
    /// </summary>
    private static async Task SignOutAsync(WorkflowContext c)
    {
        if (c.IsMobile)
        {
            await c.Page.Locator("header [data-slot='dropdown-menu-trigger']").ClickAsync();
            await c.Page.GetByRole(AriaRole.Menuitem, new() { Name = "Sign out" }).ClickAsync();
            return;
        }

        await c.Button("Sign out").ClickAsync();
    }
}

/// <summary>
/// Who can do what. TransTruck's roles are a hierarchy, not a flat list, and the point of this
/// segment is that the product enforces it rather than trusting people to stay in their lane.
/// </summary>
public sealed class UserAccessWorkflow() : Workflow(
    key: "UserAccess",
    displayName: "Staff and What They Can Reach",
    module: "Settings",
    targetAudience: "Fleet owners",
    businessPurpose: "Give the office staff their own logins without giving them the owner's authority — approvals and money decisions stay with the owner.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Settings is where the owner manages who else can sign in.",
            () => c.NavigateAsync("Settings", "Settings"));

        await c.StepAsync(
            "Settings opens on the company's own details — the name and address printed on every document.",
            () => c.ExpectVisibleAsync("Company name"));

        await c.StepAsync(
            "The Users tab is where the accounts live, each with the role that decides what it can reach.",
            async () =>
            {
                await WorkflowContext.Visible(c.Page.GetByRole(AriaRole.Tab, new() { Name = "Users" })).ClickAsync();
                await c.ExpectVisibleAsync(DemoData.OwnerPhone);
                await c.BeatAsync();
            });

        await c.StepAsync(
            "Approvals is an owner-only screen, and it is reachable — which is what the role grants.",
            () => c.NavigateAsync("Approvals", "Approvals"));
    }
}
