using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>The one place a brand-new company is built: its row, its starter
/// masters, and its Owner login. Shared by the two ways a company can come
/// into existence — EnterpriseAdmin onboarding one by hand
/// (<see cref="EnterpriseAdminService.OnboardCompanyAsync"/>) and an owner
/// registering themselves (<see cref="RegistrationService"/>) — so the two
/// can never drift into seeding different starter data or applying different
/// username rules.</summary>
internal static class CompanyProvisioning
{
    /// <summary>Indian mobile numbers only need the ten digits kept — an
    /// optional +91/leading 0 and any spacing are noise once the number is a
    /// username rather than something dialed.</summary>
    public static string NormalizePhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    /// <summary>The starter masters every company begins with, whichever way
    /// it was created — states, cities, and the expense/maintenance category
    /// lists.</summary>
    public static void SeedStarterMasters(AppDbContext db, Guid companyId)
    {
        string[] expenseCategoryNames = ["Fuel", "Toll", "Loading", "Unloading", "Repair", "Other"];
        db.ExpenseCategories.AddRange(expenseCategoryNames.Select(n => new ExpenseCategory { Name = n, CompanyId = companyId }));

        string[] maintenanceCategoryNames = ["Service", "Tyres", "Insurance", "Repair", "Spare Parts", "Other"];
        db.MaintenanceCategories.AddRange(maintenanceCategoryNames.Select(n => new MaintenanceCategory { Name = n, CompanyId = companyId }));

        var telangana = new State { Name = "Telangana", CompanyId = companyId };
        var andhraPradesh = new State { Name = "Andhra Pradesh", CompanyId = companyId };
        var karnataka = new State { Name = "Karnataka", CompanyId = companyId };
        var maharashtra = new State { Name = "Maharashtra", CompanyId = companyId };
        db.States.AddRange(telangana, andhraPradesh, karnataka, maharashtra);

        db.Cities.AddRange(
            new City { Name = "Hyderabad", State = telangana, CompanyId = companyId },
            new City { Name = "Sangareddy", State = telangana, CompanyId = companyId },
            new City { Name = "Warangal", State = telangana, CompanyId = companyId },
            new City { Name = "Vijayawada", State = andhraPradesh, CompanyId = companyId },
            new City { Name = "Visakhapatnam", State = andhraPradesh, CompanyId = companyId },
            new City { Name = "Bengaluru", State = karnataka, CompanyId = companyId },
            new City { Name = "Mumbai", State = maharashtra, CompanyId = companyId },
            new City { Name = "Pune", State = maharashtra, CompanyId = companyId });
    }

    /// <summary>The owner's phone as their username, with a numeric suffix
    /// only if that exact login already exists. Usernames are unique
    /// system-wide (login takes no "which company" step), so a collision has
    /// to resolve to something, and the suffix keeps the number recognisable.
    /// Self-registration refuses the collision outright instead of calling
    /// this — see <see cref="RegistrationService"/>.</summary>
    public static async Task<string> GenerateUniqueUsernameFromPhoneAsync(AppDbContext db, string ownerPhone)
    {
        var normalized = NormalizePhone(ownerPhone);
        if (string.IsNullOrEmpty(normalized)) normalized = "owner";

        var candidate = normalized;
        var suffix = 1;
        while (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == candidate))
        {
            suffix++;
            candidate = $"{normalized}-{suffix}";
        }

        return candidate;
    }
}
