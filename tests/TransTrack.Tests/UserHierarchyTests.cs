using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// Who may create or change whom. Before this rule existed the endpoint was
/// gated only as "Owner or CoOwner", with no check on the role being handed
/// out — so a CoOwner could create an Owner and quietly promote themselves
/// past their own ceiling.
/// </summary>
public class UserHierarchyTests
{
    /// <summary>Signs the given role in as the caller and returns a service
    /// that sees them, mirroring how the API resolves the current user.</summary>
    private static async Task<AuthService> ActingAsAsync(TestWorld world, UserRole role)
    {
        await using var db = await world.Factory.CreateDbContextAsync();

        var caller = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = world.CompanyId,
            Username = $"caller-{role}-{Guid.NewGuid():N}".ToLowerInvariant(),
            DisplayName = $"{role} Caller",
            Role = role,
            IsActive = true,
            PasswordHash = "x",
            PasswordSalt = "y",
        };

        db.Users.Add(caller);
        await db.SaveChangesAsync();

        world.CurrentUser.UserId = caller.Id;
        return new AuthService(world.Factory, world.CurrentUser);
    }

    private static User NewUser(UserRole role) => new()
    {
        Id = Guid.Empty,
        Username = $"9{Random.Shared.NextInt64(100000000, 999999999)}",
        DisplayName = $"New {role}",
        Role = role,
        IsActive = true,
    };

    [Theory]
    [InlineData(UserRole.Owner, UserRole.Owner)]
    [InlineData(UserRole.Owner, UserRole.CoOwner)]
    [InlineData(UserRole.Owner, UserRole.Accountant)]
    [InlineData(UserRole.CoOwner, UserRole.CoOwner)]
    [InlineData(UserRole.CoOwner, UserRole.Accountant)]
    [InlineData(UserRole.Accountant, UserRole.Accountant)]
    public async Task A_role_may_create_its_own_level_and_below(UserRole caller, UserRole created)
    {
        await using var world = await TestWorld.CreateAsync();
        var auth = await ActingAsAsync(world, caller);

        await auth.SaveUserAsync(NewUser(created), "Welcome@123");

        await using var db = await world.Factory.CreateDbContextAsync();
        Assert.True(await db.Users.AnyAsync(u => u.Role == created && u.DisplayName == $"New {created}"));
    }

    [Theory]
    [InlineData(UserRole.CoOwner, UserRole.Owner)]
    [InlineData(UserRole.Accountant, UserRole.Owner)]
    [InlineData(UserRole.Accountant, UserRole.CoOwner)]
    public async Task Nobody_may_create_a_role_above_their_own(UserRole caller, UserRole attempted)
    {
        await using var world = await TestWorld.CreateAsync();
        var auth = await ActingAsAsync(world, caller);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => auth.SaveUserAsync(NewUser(attempted), "Welcome@123"));

        Assert.Contains("cannot create or assign", error.Message);
    }

    [Fact]
    public async Task A_co_owner_cannot_edit_an_owner()
    {
        await using var world = await TestWorld.CreateAsync();

        var ownerAuth = await ActingAsAsync(world, UserRole.Owner);
        var owner = NewUser(UserRole.Owner);
        await ownerAuth.SaveUserAsync(owner, "Welcome@123");

        Guid ownerId;
        await using (var db = await world.Factory.CreateDbContextAsync())
            ownerId = await db.Users.Where(u => u.DisplayName == "New Owner").Select(u => u.Id).FirstAsync();

        var coOwnerAuth = await ActingAsAsync(world, UserRole.CoOwner);
        owner.Id = ownerId;
        owner.DisplayName = "Renamed by a co-owner";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coOwnerAuth.SaveUserAsync(owner, null));

        Assert.Contains("cannot change", error.Message);
    }

    /// <summary>The escalation this rule exists to stop: taking someone you
    /// are allowed to edit and promoting them past your own ceiling.</summary>
    [Fact]
    public async Task A_co_owner_cannot_promote_an_accountant_to_owner()
    {
        await using var world = await TestWorld.CreateAsync();
        var auth = await ActingAsAsync(world, UserRole.CoOwner);

        var accountant = NewUser(UserRole.Accountant);
        await auth.SaveUserAsync(accountant, "Welcome@123");

        await using (var db = await world.Factory.CreateDbContextAsync())
            accountant.Id = await db.Users.Where(u => u.DisplayName == "New Accountant").Select(u => u.Id).FirstAsync();

        accountant.Role = UserRole.Owner;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => auth.SaveUserAsync(accountant, null));

        Assert.Contains("cannot create or assign", error.Message);
    }

    /// <summary>Demoting the only Owner would leave nobody able to approve
    /// amounts or cancel a trip, with no way back in without EnterpriseAdmin.</summary>
    [Fact]
    public async Task The_last_active_owner_cannot_demote_themselves()
    {
        await using var world = await TestWorld.CreateAsync();
        var auth = await ActingAsAsync(world, UserRole.Owner);

        // TestWorld seeds an Owner of its own, so step the company down to a
        // single Owner — the caller — before testing that the last one is stuck.
        User onlyOwner;
        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            var seeded = await db.Users.FirstAsync(u => u.Role == UserRole.Owner && u.Id != world.CurrentUser.UserId);
            seeded.Role = UserRole.Accountant;
            await db.SaveChangesAsync();

            var owners = await db.Users.Where(u => u.Role == UserRole.Owner && u.IsActive && !u.IsDeleted).ToListAsync();
            onlyOwner = Assert.Single(owners);
        }

        onlyOwner.Role = UserRole.Accountant;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => auth.SaveUserAsync(onlyOwner, null));

        Assert.Contains("only active Owner", error.Message);
    }

    [Fact]
    public async Task An_owner_can_be_demoted_once_another_owner_exists()
    {
        await using var world = await TestWorld.CreateAsync();
        var auth = await ActingAsAsync(world, UserRole.Owner);

        await auth.SaveUserAsync(NewUser(UserRole.Owner), "Welcome@123");

        User demoted;
        await using (var db = await world.Factory.CreateDbContextAsync())
            demoted = await db.Users.FirstAsync(u => u.DisplayName == "New Owner");

        demoted.Role = UserRole.Accountant;
        await auth.SaveUserAsync(demoted, null);

        await using (var db = await world.Factory.CreateDbContextAsync())
            Assert.Equal(UserRole.Accountant, (await db.Users.FirstAsync(u => u.Id == demoted.Id)).Role);
    }
}
