using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data.Security;

namespace TransTrack.Data;

/// <summary>
/// Self-service sign-up: an owner creating their own company from the login
/// screen, without EnterpriseAdmin onboarding them by hand. The company it
/// produces is identical to an onboarded one — same starter masters, same
/// Owner role, same forced password change on first sign-in — so everything
/// downstream (including EnterpriseAdmin's own password-reset and license
/// tools) works on a self-registered company exactly as it does on an
/// onboarded one.
///
/// Runs anonymously, so it deliberately does no work through
/// ICurrentUserContext: there is no signed-in company yet, and every query
/// here uses IgnoreQueryFilters for the same reason login does.
/// </summary>
public class RegistrationService(IDbContextFactory<AppDbContext> factory)
{
    public record RegisterResult(Guid CompanyId, string CompanyName, string Username, DateTime LicenseExpiresOn);

    /// <summary>How long a self-registered company's license runs before it
    /// needs EnterpriseAdmin to renew it — the same twelve months onboarding
    /// grants, so signing up yourself is not a way to get a longer one.</summary>
    public const int TrialMonths = 12;

    public async Task<RegisterResult> RegisterAsync(string companyName, string phone, string? password)
    {
        companyName = (companyName ?? string.Empty).Trim();
        phone = (phone ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(companyName))
            throw new InvalidOperationException("Company name is required.");

        if (!PhoneValidator.IsValid(phone) || string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        var username = CompanyProvisioning.NormalizePhone(phone);
        if (username.Length != 10)
            throw new InvalidOperationException("Enter a 10-digit mobile number.");

        // Blank means "use the default" rather than "no password" — the
        // registration form pre-fills it, and MustChangePassword below forces
        // it to be replaced on the very first sign-in either way.
        password = string.IsNullOrWhiteSpace(password) ? EnterpriseAdminService.DefaultPassword : password;
        if (password.Length < 8)
            throw new InvalidOperationException("The password must be at least 8 characters.");

        await using var db = await factory.CreateDbContextAsync();

        // Two separate checks so the message actually tells them what to do.
        // The pair (company name + phone) is the rule as specified; the
        // phone-already-used case is called out on its own because the login
        // *is* the phone number, so a second company on the same number
        // could not be signed into unambiguously.
        var normalizedName = companyName.ToLower();
        if (await db.Companies.IgnoreQueryFilters()
                .AnyAsync(c => c.CompanyName.ToLower() == normalizedName && c.OwnerPhone == username && !c.IsDeleted))
            throw new InvalidOperationException("This company is already registered. Sign in instead.");

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username && !u.IsDeleted))
            throw new InvalidOperationException(
                "That phone number is already registered. Sign in with it, or register with a different number.");

        var startsOn = DateTime.UtcNow;
        var company = new Company
        {
            CompanyName = companyName,
            // No separate owner-name field on the registration form — the
            // company name is all we have to go on until they fill in their
            // details in Settings.
            OwnerName = companyName,
            OwnerPhone = username,
            LicenseStartsOn = startsOn,
            LicenseExpiresOn = startsOn.AddMonths(TrialMonths),
            IsActive = true
        };
        db.Companies.Add(company);

        CompanyProvisioning.SeedStarterMasters(db, company.Id);

        var (hash, salt) = PasswordHasher.Hash(password);
        db.Users.Add(new User
        {
            CompanyId = company.Id,
            Username = username,
            DisplayName = companyName,
            Role = UserRole.Owner,
            PasswordHash = hash,
            PasswordSalt = salt,
            MustChangePassword = true,
            IsActive = true
        });

        await db.SaveChangesAsync();

        AppLog.Info($"Company '{companyName}' ({company.Id}) self-registered, owner login '{username}'.");

        return new RegisterResult(company.Id, companyName, username, company.LicenseExpiresOn);
    }
}
