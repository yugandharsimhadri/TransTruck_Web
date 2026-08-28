using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data.Security;

namespace TransTrack.Data;

public enum LoginOutcome
{
    Success,
    EnterpriseRecovery,
    Failed,

    /// <summary>Credentials were correct but the user's company's license
    /// has expired or been deactivated — blocked before a session token is
    /// ever issued. Only EnterpriseAdmin can clear this, via the renew
    /// endpoint.</summary>
    LicenseExpired
}

/// <summary>Result of a login attempt. EnterpriseRecovery is not a normal
/// sign-in — the caller must route it to the password-reset screen, never to
/// the application shell.</summary>
public record LoginResult(LoginOutcome Outcome, User? User, string? Message)
{
    public static LoginResult Success(User user) => new(LoginOutcome.Success, user, null);
    public static LoginResult EnterpriseRecovery() => new(LoginOutcome.EnterpriseRecovery, null, null);
    public static LoginResult Failed(string message) => new(LoginOutcome.Failed, null, message);
    public static LoginResult LicenseExpired(string message) => new(LoginOutcome.LicenseExpired, null, message);
}

/// <summary>Sign-in, password changes, and the user list Owner manages.
///
/// The current-user context is optional so the login paths — which run before
/// anyone is signed in — and the existing tests keep working unchanged; it is
/// only consulted when deciding who may manage whom.</summary>
public class AuthService(IDbContextFactory<AppDbContext> factory, ICurrentUserContext? currentUser = null)
{
    private readonly ICurrentUserContext _currentUser = currentUser ?? new NullCurrentUserContext();

    /// <summary>
    /// The support/recovery identity for the company's own IT team — never a
    /// row in the Users table, a constant checked directly here, so it can
    /// never be listed, edited, renamed or deleted from any screen, and its
    /// password never changes through this application — only by shipping a
    /// new build. Known only to the people who build and support this
    /// software; the Owner account cannot see or use it.
    /// </summary>
    public const string EnterpriseAdminUsername = "EnterpriseAdmin";

    private static readonly Lazy<(string Hash, string Salt)> EnterpriseAdminCredential =
        new(() => PasswordHasher.Hash("SivAyAAn@HMS"));

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        username = username?.Trim() ?? string.Empty;

        if (string.Equals(username, EnterpriseAdminUsername, StringComparison.OrdinalIgnoreCase))
        {
            var (hash, salt) = EnterpriseAdminCredential.Value;
            if (!PasswordHasher.Verify(password, hash, salt))
                return LoginResult.Failed("Incorrect username or password.");

            AppLog.Info("EnterpriseAdmin signed in for password recovery.");
            return LoginResult.EnterpriseRecovery();
        }

        await using var db = await factory.CreateDbContextAsync();

        // IgnoreQueryFilters: at this point nobody is signed in yet, so
        // AppDbContext.CurrentCompanyId is Guid.Empty and the normal
        // per-company filter on Users would match nothing at all — this is
        // the one place that's correct, since login is how we discover
        // which company the caller belongs to in the first place. Username
        // is unique across the whole system (not just within a company), so
        // this still resolves to at most one row.
        // Case-insensitive on purpose — "Owner" and "owner" are the same account.
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(
            u => u.Username.ToLower() == username.ToLower() && !u.IsDeleted);

        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return LoginResult.Failed("Incorrect username or password.");

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == user.CompanyId);
        if (company is null || !company.IsLicenseValid)
        {
            AppLog.Info($"Login blocked for '{user.Username}' — company license invalid or expired.");
            return LoginResult.LicenseExpired(
                "Your company's license has expired or is inactive. Contact your provider to renew it.");
        }

        user.LastLoginOn = DateTime.Now;
        await db.SaveChangesAsync();

        return LoginResult.Success(user);
    }

    /// <summary>Every user within the caller's own company — implicitly
    /// scoped by the global tenant filter, so Owner never sees another
    /// company's users here.</summary>
    public async Task<List<User>> GetUsersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    /// <summary>One user by id — for the "who am I" check on every page load,
    /// which only ever needs the caller's own row, not the whole company
    /// list to filter client-side.</summary>
    public async Task<User?> GetUserAsync(Guid userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
    }

    /// <summary>Creates a user, or updates one when <paramref name="user"/>.Id
    /// matches an existing row. A new user always needs a password; an
    /// existing one keeps its current password unless <paramref name="newPassword"/>
    /// is supplied.</summary>
    public async Task SaveUserAsync(User user, string? newPassword)
    {
        var username = user.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.Equals(username, EnterpriseAdminUsername, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"'{EnterpriseAdminUsername}' is reserved.");

        await using var db = await factory.CreateDbContextAsync();

        // IgnoreQueryFilters: username uniqueness is global, across every
        // company, not just the caller's own — a plain (filtered) query
        // here would miss a collision with another company's user entirely.
        if (await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Username.ToLower() == username.ToLower() && !u.IsDeleted && u.Id != user.Id))
            throw new InvalidOperationException($"'{username}' is already in use.");

        var entity = user.Id == Guid.Empty ? null : await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        var isNew = entity is null;

        await EnsureCallerMayManageAsync(db, existingRole: entity?.Role, newRole: user.Role);

        // Losing the last Owner would leave nobody able to approve amounts or
        // cancel a trip, and no way back in without EnterpriseAdmin — so a
        // demotion or deactivation that empties the role is refused rather
        // than discovered later.
        if (!isNew && entity!.Role == UserRole.Owner && (user.Role != UserRole.Owner || !user.IsActive))
        {
            var otherActiveOwners = await db.Users.CountAsync(
                u => u.Id != entity.Id && u.Role == UserRole.Owner && u.IsActive && !u.IsDeleted);

            if (otherActiveOwners == 0)
                throw new InvalidOperationException(
                    "This is the only active Owner. Make someone else an Owner first.");
        }

        entity ??= new User();

        entity.Username = username;
        entity.DisplayName = (user.DisplayName ?? string.Empty).Trim();
        entity.Role = user.Role;
        entity.IsActive = user.IsActive;

        if (isNew && string.IsNullOrWhiteSpace(newPassword))
            throw new InvalidOperationException("A password is required for a new user.");

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var (hash, salt) = PasswordHasher.Hash(newPassword);
            entity.PasswordHash = hash;
            entity.PasswordSalt = salt;
            entity.MustChangePassword = true;
        }

        if (isNew) db.Users.Add(entity);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Enforces the role hierarchy on every create and edit: nobody may act on
    /// a user who outranks them, and nobody may hand out a role above their
    /// own. Both halves matter — checking only the new role would let a
    /// CoOwner edit an Owner "down" to CoOwner, and checking only the existing
    /// role would let them promote an Accountant straight to Owner.
    ///
    /// The caller's role is read from the database rather than taken from
    /// their token, so a session issued before a demotion carries no authority
    /// it no longer has.
    /// </summary>
    private async Task EnsureCallerMayManageAsync(AppDbContext db, UserRole? existingRole, UserRole newRole)
    {
        if (_currentUser.UserId is not { } callerId) return; // No session: onboarding/registration paths.

        var callerRole = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == callerId && !u.IsDeleted)
            .Select(u => (UserRole?)u.Role)
            .FirstOrDefaultAsync();

        if (callerRole is not { } caller) return; // EnterpriseAdmin is not a Users row.

        if (existingRole is { } current && !caller.CanManage(current))
            throw new InvalidOperationException($"{Describe(caller, capitalised: true)} cannot change {Describe(current)}.");

        if (!caller.CanManage(newRole))
            throw new InvalidOperationException($"{Describe(caller, capitalised: true)} cannot create or assign the {Name(newRole)} role.");
    }

    private static string Name(UserRole role) => role == UserRole.CoOwner ? "Co-owner" : role.ToString();

    /// <summary>"an Accountant" / "An Owner" — the article has to follow the
    /// word, since "a Accountant" reads as a bug in its own right.</summary>
    private static string Describe(UserRole role, bool capitalised = false)
    {
        var name = Name(role);
        var article = "AEIOU".Contains(name[0]) ? "an" : "a";
        return $"{(capitalised ? char.ToUpperInvariant(article[0]) + article[1..] : article)} {name}";
    }

    /// <summary>The signed-in user setting their own new password, typically
    /// right after signing in with a temporary one. IgnoreQueryFilters: the
    /// change-password token that authorizes this call carries no
    /// company_id claim (see JwtTokenService.CreateChangePasswordToken) —
    /// the caller doesn't know their own company yet at this point, that's
    /// exactly what this lookup resolves, via the trusted userId from the
    /// token instead. Returns the user so the caller can mint a full
    /// session token (with company_id) immediately, without a second,
    /// now-impossible-to-scope lookup.</summary>
    public async Task<User> ChangeOwnPasswordAsync(Guid userId, string newPassword)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId)
                    ?? throw new InvalidOperationException("User not found.");

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;

        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Owner resetting another user's forgotten password —
    /// implicitly scoped to the caller's own company by the global tenant
    /// filter. EnterpriseAdmin's equivalent, which can reach across
    /// companies, is <see cref="EnterpriseAdminService.ResetUserPasswordAsync"/>
    /// instead. Always leaves MustChangePassword set, so the temporary
    /// password handed out here is only ever good for one sign-in.</summary>
    public async Task<string> ResetPasswordAsync(Guid userId, string temporaryPassword)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                    ?? throw new InvalidOperationException("User not found.");

        var (hash, salt) = PasswordHasher.Hash(temporaryPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = true;

        await db.SaveChangesAsync();

        AppLog.Info($"Password reset for user '{user.Username}'.");
        return user.Username;
    }
}
