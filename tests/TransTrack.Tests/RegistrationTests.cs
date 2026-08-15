using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>Self-service sign-up. The rules that matter: the phone becomes
/// the login, the first sign-in is always forced through a password change,
/// and a company can't be registered twice.</summary>
public class RegistrationTests
{
    private static RegistrationService ServiceFor(TestWorld world) => new(world.Factory);

    [Fact]
    public async Task Registering_creates_a_company_with_the_phone_as_its_login()
    {
        await using var world = await TestWorld.CreateAsync();

        var result = await ServiceFor(world).RegisterAsync("New Haulage", "9123456780", null);

        Assert.Equal("9123456780", result.Username);
        Assert.Equal("New Haulage", result.CompanyName);

        await using var db = await world.Factory.CreateDbContextAsync();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Username == "9123456780");

        Assert.Equal(UserRole.Owner, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(result.CompanyId, user.CompanyId);
    }

    /// <summary>The whole point of the default password: it is only ever good
    /// for one sign-in, whether the owner accepted it or typed their own.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("MyOwnPassw0rd")]
    public async Task First_sign_in_is_always_forced_through_a_password_change(string? password)
    {
        await using var world = await TestWorld.CreateAsync();

        await ServiceFor(world).RegisterAsync("Forced Change Transport", "9123456781", password);

        await using var db = await world.Factory.CreateDbContextAsync();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Username == "9123456781");

        Assert.True(user.MustChangePassword);
    }

    /// <summary>A blank password means "use the default", and that default has
    /// to actually work at the login screen — otherwise the owner is locked
    /// out of the company they just created.</summary>
    [Fact]
    public async Task Default_password_signs_in()
    {
        await using var world = await TestWorld.CreateAsync();

        await ServiceFor(world).RegisterAsync("Default Password Transport", "9123456782", null);

        var auth = new AuthService(world.Factory);
        var result = await auth.LoginAsync("9123456782", EnterpriseAdminService.DefaultPassword);

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.True(result.User!.MustChangePassword);
    }

    [Fact]
    public async Task A_self_registered_company_gets_the_same_starter_masters_as_an_onboarded_one()
    {
        await using var world = await TestWorld.CreateAsync();

        var result = await ServiceFor(world).RegisterAsync("Seeded Transport", "9123456783", null);

        await using var db = await world.Factory.CreateDbContextAsync();
        var cities = await db.Cities.IgnoreQueryFilters().CountAsync(c => c.CompanyId == result.CompanyId);
        var expenseCategories = await db.ExpenseCategories.IgnoreQueryFilters().CountAsync(c => c.CompanyId == result.CompanyId);

        Assert.Equal(8, cities);
        Assert.Equal(6, expenseCategories);
    }

    [Fact]
    public async Task Registering_the_same_company_and_phone_twice_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        await service.RegisterAsync("Duplicate Transport", "9123456784", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterAsync("Duplicate Transport", "9123456784", null));

        Assert.Contains("already registered", ex.Message);
    }

    /// <summary>The login *is* the phone number, so a second company on a
    /// number already in use could not be signed into unambiguously — it is
    /// refused with a message that says what to do instead.</summary>
    [Fact]
    public async Task Registering_a_phone_already_in_use_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        await service.RegisterAsync("First Transport", "9123456785", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterAsync("Second Transport", "9123456785", null));

        Assert.Contains("already registered", ex.Message);
    }

    [Theory]
    [InlineData("", "9123456786")]
    [InlineData("   ", "9123456786")]
    [InlineData("No Phone Transport", "")]
    [InlineData("Short Phone Transport", "12345")]
    public async Task Registering_without_the_required_details_is_refused(string companyName, string phone)
    {
        await using var world = await TestWorld.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ServiceFor(world).RegisterAsync(companyName, phone, null));
    }

    /// <summary>A self-registered company must be indistinguishable from an
    /// onboarded one to EnterpriseAdmin — otherwise the owner has no route
    /// back in when they forget the password they just set.</summary>
    [Fact]
    public async Task EnterpriseAdmin_can_reset_a_self_registered_owners_password()
    {
        await using var world = await TestWorld.CreateAsync();

        var registered = await ServiceFor(world).RegisterAsync("Resettable Transport", "9123456787", null);

        var enterprise = new EnterpriseAdminService(world.Factory);
        var users = await enterprise.GetCompanyUsersAsync(registered.CompanyId);
        var owner = Assert.Single(users);

        var username = await enterprise.ResetUserPasswordAsync(registered.CompanyId, owner.Id, "Reset@12345");
        Assert.Equal("9123456787", username);

        var auth = new AuthService(world.Factory);
        var result = await auth.LoginAsync("9123456787", "Reset@12345");
        Assert.Equal(LoginOutcome.Success, result.Outcome);
    }

    /// <summary>Registration must not become a side door into an existing
    /// company's data — a brand-new company starts with nothing but its own
    /// seeded masters.</summary>
    [Fact]
    public async Task A_new_company_sees_none_of_an_existing_companys_trips()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.BookTripAsync(50000);

        var registered = await ServiceFor(world).RegisterAsync("Isolated Transport", "9123456788", null);

        world.CurrentUser.CompanyId = registered.CompanyId;
        world.CurrentUser.UserId = null;

        var trips = await world.Trips.GetTripsAsync();
        Assert.Empty(trips);
    }
}
