using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data.Security;

namespace TransTrack.Data;

/// <summary>
/// Everything reached only via an EnterpriseRecovery login (see
/// <see cref="AuthService.LoginAsync"/>) — onboarding a new company,
/// resetting a forgotten password, and license renewal. Deliberately
/// separate from every other service: those are all scoped to "my own
/// company" by AppDbContext's global query filter, while this one exists
/// specifically to reach across companies, so every query here calls
/// <c>IgnoreQueryFilters()</c> explicitly and takes a companyId as an
/// argument rather than relying on ICurrentUserContext (EnterpriseAdmin is
/// never itself scoped to a company). Per the product's own rule,
/// EnterpriseAdmin never touches a company's fleet data — only these four
/// things.
/// </summary>
public class EnterpriseAdminService(IDbContextFactory<AppDbContext> factory)
{
    public record OnboardResult(Guid CompanyId, string CompanyName, string OwnerUsername, string TemporaryPassword, DateTime LicenseExpiresOn);

    public record CompanySummary(Guid Id, string CompanyName, string OwnerName, string OwnerPhone,
        bool IsActive, DateTime LicenseExpiresOn, bool IsLicenseValid, DateTime CreatedAt);

    public record CompanyUserSummary(Guid Id, string Username, string DisplayName, UserRole Role, bool IsActive);

    /// <summary>The Owner's username on every new company, and the temporary
    /// password on every onboarding and every EnterpriseAdmin reset unless a
    /// different one is entered — memorable on purpose for people who are
    /// not used to managing logins, since MustChangePassword forces it to be
    /// replaced the moment it's actually used.</summary>
    public const string DefaultPassword = "Welcome@123";

    /// <summary>Creates a new company, seeds its default masters (same
    /// starter set every company gets — states/cities/expense and
    /// maintenance categories), and creates its Owner login. The username is
    /// always the owner's own phone number — nothing to invent or forget —
    /// and the password is the fixed <see cref="DefaultPassword"/>, which
    /// MustChangePassword forces to be replaced on first sign-in. Relay both
    /// to the owner directly (phone call, message, however the customer is
    /// onboarded); neither is emailed or stored anywhere in the clear.</summary>
    public async Task<OnboardResult> OnboardCompanyAsync(
        string companyName, string ownerName, string ownerPhone, int licenseMonths = 12)
    {
        companyName = (companyName ?? string.Empty).Trim();
        ownerName = (ownerName ?? string.Empty).Trim();
        ownerPhone = (ownerPhone ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(companyName)) throw new InvalidOperationException("Company name is required.");
        if (string.IsNullOrWhiteSpace(ownerName)) throw new InvalidOperationException("Owner name is required.");
        if (!PhoneValidator.IsValid(ownerPhone)) throw new InvalidOperationException("That doesn't look like a valid phone number.");
        if (licenseMonths <= 0) throw new InvalidOperationException("License length must be at least one month.");

        await using var db = await factory.CreateDbContextAsync();

        var startsOn = DateTime.UtcNow;
        var company = new Company
        {
            CompanyName = companyName,
            OwnerName = ownerName,
            OwnerPhone = ownerPhone,
            LicenseStartsOn = startsOn,
            LicenseExpiresOn = startsOn.AddMonths(licenseMonths),
            IsActive = true
        };
        db.Companies.Add(company);

        // The same starter set DbBootstrapper used to seed once globally —
        // now seeded per company, at onboarding, instead. Shared with
        // self-registration so both routes produce an identical company.
        CompanyProvisioning.SeedStarterMasters(db, company.Id);

        var username = await CompanyProvisioning.GenerateUniqueUsernameFromPhoneAsync(db, ownerPhone);
        var (hash, salt) = PasswordHasher.Hash(DefaultPassword);

        var owner = new User
        {
            CompanyId = company.Id,
            Username = username,
            DisplayName = ownerName,
            Role = UserRole.Owner,
            PasswordHash = hash,
            PasswordSalt = salt,
            MustChangePassword = true,
            IsActive = true
        };
        db.Users.Add(owner);

        await db.SaveChangesAsync();

        AppLog.Info($"EnterpriseAdmin onboarded company '{companyName}' ({company.Id}), owner login '{username}'.");

        return new OnboardResult(company.Id, companyName, username, DefaultPassword, company.LicenseExpiresOn);
    }

    public async Task<List<CompanySummary>> ListCompaniesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Companies.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CompanyName)
            .Select(c => new CompanySummary(c.Id, c.CompanyName, c.OwnerName, c.OwnerPhone,
                c.IsActive, c.LicenseExpiresOn, c.IsActive && DateTime.UtcNow <= c.LicenseExpiresOn, c.CreatedAt))
            .ToListAsync();
    }

    /// <summary>Who to offer in the "reset whose password" dropdown once
    /// EnterpriseAdmin has picked a company.</summary>
    public async Task<List<CompanyUserSummary>> GetCompanyUsersAsync(Guid companyId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.CompanyId == companyId && !u.IsDeleted)
            .OrderBy(u => u.Username)
            .Select(u => new CompanyUserSummary(u.Id, u.Username, u.DisplayName, u.Role, u.IsActive))
            .ToListAsync();
    }

    /// <summary>Resets a forgotten password for a user in a specific
    /// company — the companyId is checked against the user's own, so
    /// EnterpriseAdmin can't be handed a stale/wrong userId and reset the
    /// wrong company's account by mistake. An empty/omitted password falls
    /// back to <see cref="DefaultPassword"/>, same as onboarding.</summary>
    public async Task<string> ResetUserPasswordAsync(Guid companyId, Guid userId, string? temporaryPassword)
    {
        temporaryPassword = string.IsNullOrWhiteSpace(temporaryPassword) ? DefaultPassword : temporaryPassword;
        if (temporaryPassword.Length < 8)
            throw new InvalidOperationException("The temporary password must be at least 8 characters.");

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId && !u.IsDeleted)
            ?? throw new InvalidOperationException("User not found in that company.");

        var (hash, salt) = PasswordHasher.Hash(temporaryPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = true;

        await db.SaveChangesAsync();

        AppLog.Info($"EnterpriseAdmin reset password for '{user.Username}' in company {companyId}.");
        return user.Username;
    }

    /// <summary>Extends a company's license by the given number of months
    /// from today (not stacked onto the old expiry, so a lapsed license
    /// doesn't silently inherit a backdated start).</summary>
    public async Task<DateTime> RenewLicenseAsync(Guid companyId, int months)
    {
        if (months <= 0) throw new InvalidOperationException("License length must be at least one month.");

        await using var db = await factory.CreateDbContextAsync();
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId)
                      ?? throw new InvalidOperationException("Company not found.");

        var from = company.LicenseExpiresOn > DateTime.UtcNow ? company.LicenseExpiresOn : DateTime.UtcNow;
        company.LicenseExpiresOn = from.AddMonths(months);
        company.IsActive = true;

        await db.SaveChangesAsync();

        AppLog.Info($"EnterpriseAdmin renewed license for company {companyId} to {company.LicenseExpiresOn:yyyy-MM-dd}.");
        return company.LicenseExpiresOn;
    }

    /// <summary>Changes a user's username to their (new) phone number —
    /// EnterpriseAdmin's fix when an owner's number changes, since the
    /// username never updates itself. Scoped to the given company the same
    /// way <see cref="ResetUserPasswordAsync"/> is.</summary>
    public async Task<string> ChangeUsernameAsync(Guid companyId, Guid userId, string newPhone)
    {
        if (!PhoneValidator.IsValid(newPhone) || string.IsNullOrWhiteSpace(newPhone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        var normalized = NormalizePhone(newPhone);
        if (normalized.Length != 10)
            throw new InvalidOperationException("Enter a 10-digit mobile number.");

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId && !u.IsDeleted)
            ?? throw new InvalidOperationException("User not found in that company.");

        if (await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Username == normalized && u.Id != userId && !u.IsDeleted))
            throw new InvalidOperationException($"'{normalized}' is already in use by another account.");

        var oldUsername = user.Username;
        user.Username = normalized;
        await db.SaveChangesAsync();

        AppLog.Info($"EnterpriseAdmin changed username '{oldUsername}' to '{normalized}' in company {companyId}.");
        return normalized;
    }

    private static string NormalizePhone(string phone) => CompanyProvisioning.NormalizePhone(phone);
}
