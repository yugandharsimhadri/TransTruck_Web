using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>CRUD for the small lookup tables — states, cities, vehicle
/// owners, billing parties, and the expense/maintenance category lists.
/// Deletion is always a soft delete (<see cref="BaseEntity.IsDeleted"/>) so a
/// master already referenced by a trip or a maintenance record is never
/// actually removed from under it.</summary>
public class MasterDataService(IDbContextFactory<AppDbContext> factory, ICurrentUserContext currentUser)
{
    // ── States ────────────────────────────────────────────────────────────

    public async Task<List<State>> GetStatesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.States.AsNoTracking().Where(s => !s.IsDeleted).OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Guid> SaveStateAsync(State state)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = state.Id == Guid.Empty ? null : await db.States.FirstOrDefaultAsync(x => x.Id == state.Id);
        var isNew = entity is null;
        entity ??= new State();
        entity.Name = state.Name.Trim();
        if (isNew) db.States.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteStateAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.States.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Cities ────────────────────────────────────────────────────────────

    public async Task<List<City>> GetCitiesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Cities.AsNoTracking().Include(c => c.State)
            .Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Guid> SaveCityAsync(City city)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = city.Id == Guid.Empty ? null : await db.Cities.FirstOrDefaultAsync(x => x.Id == city.Id);
        var isNew = entity is null;
        entity ??= new City();
        entity.Name = city.Name.Trim();
        entity.StateId = city.StateId;
        if (isNew) db.Cities.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteCityAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Cities.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Owners (other-owner vehicles) ────────────────────────────────────

    public async Task<List<Owner>> GetOwnersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Owners.AsNoTracking().Where(o => !o.IsDeleted).OrderBy(o => o.Name).ToListAsync();
    }

    public async Task<Guid> SaveOwnerAsync(Owner owner)
    {
        if (!PhoneValidator.IsValid(owner.Phone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        await using var db = await factory.CreateDbContextAsync();
        var entity = owner.Id == Guid.Empty ? null : await db.Owners.FirstOrDefaultAsync(x => x.Id == owner.Id);
        var isNew = entity is null;
        entity ??= new Owner();
        entity.Name = owner.Name.Trim();
        entity.Phone = owner.Phone;
        entity.Address = owner.Address;
        entity.BankAccountNo = owner.BankAccountNo;
        entity.Ifsc = owner.Ifsc;
        entity.Remarks = owner.Remarks;
        if (isNew) db.Owners.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    /// <summary>Upserts just name/phone for the owner attached to a vehicle —
    /// the Vehicle form's own inline fields, now that a vehicle and its
    /// other-owner details are entered together rather than as two separate
    /// masters. Bank details are only ever set via those fuller fields, if
    /// an existing owner already has them; this never clears them.</summary>
    public async Task<Guid> SaveOwnerBasicAsync(Guid? existingOwnerId, string name, string? phone)
    {
        if (!PhoneValidator.IsValid(phone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        await using var db = await factory.CreateDbContextAsync();
        var entity = existingOwnerId is { } id ? await db.Owners.FirstOrDefaultAsync(x => x.Id == id) : null;
        var isNew = entity is null;
        entity ??= new Owner();
        entity.Name = name.Trim();
        entity.Phone = phone;
        if (isNew) db.Owners.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteOwnerAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Owners.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Parties (billing) ────────────────────────────────────────────────

    public async Task<List<Party>> GetPartiesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Parties.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Guid> SavePartyAsync(Party party)
    {
        if (!PhoneValidator.IsValid(party.Phone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        await using var db = await factory.CreateDbContextAsync();
        var entity = party.Id == Guid.Empty ? null : await db.Parties.FirstOrDefaultAsync(x => x.Id == party.Id);
        var isNew = entity is null;
        entity ??= new Party();
        entity.Name = party.Name.Trim();
        entity.Phone = party.Phone;
        entity.Address = party.Address;
        entity.Gstin = party.Gstin;
        if (isNew) db.Parties.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeletePartyAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Parties.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Expense categories ───────────────────────────────────────────────

    public async Task<List<ExpenseCategory>> GetExpenseCategoriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ExpenseCategories.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();
    }

    public async Task SaveExpenseCategoryAsync(ExpenseCategory category)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = category.Id == Guid.Empty ? null : await db.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == category.Id);
        var isNew = entity is null;
        entity ??= new ExpenseCategory();
        entity.Name = category.Name.Trim();
        if (isNew) db.ExpenseCategories.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteExpenseCategoryAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Maintenance categories ───────────────────────────────────────────

    public async Task<List<MaintenanceCategory>> GetMaintenanceCategoriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.MaintenanceCategories.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Name).ToListAsync();
    }

    public async Task SaveMaintenanceCategoryAsync(MaintenanceCategory category)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = category.Id == Guid.Empty ? null : await db.MaintenanceCategories.FirstOrDefaultAsync(x => x.Id == category.Id);
        var isNew = entity is null;
        entity ??= new MaintenanceCategory();
        entity.Name = category.Name.Trim();
        if (isNew) db.MaintenanceCategories.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteMaintenanceCategoryAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.MaintenanceCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    // ── Company (own tenant's branding/letterhead — never the license/
    // onboarding fields, which only EnterpriseAdminService touches) ───────

    private Guid OwnCompanyId => currentUser.CompanyId
        ?? throw new InvalidOperationException("No signed-in company.");

    public async Task<Company> GetCompanyAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Companies.AsNoTracking().FirstAsync(c => c.Id == OwnCompanyId);
    }

    /// <summary>Just the theme — flipping the sidebar toggle should not
    /// need the rest of the company form to be valid or even loaded.</summary>
    public async Task UpdateThemeAsync(AppThemeKind theme)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Companies.FirstAsync(c => c.Id == OwnCompanyId);
        entity.Theme = theme;
        await db.SaveChangesAsync();
    }

    /// <summary>Updates only the branding/letterhead fields — license and
    /// onboarding fields (OwnerName, OwnerPhone, license dates, IsActive)
    /// are deliberately untouched here; only EnterpriseAdminService sets
    /// those.</summary>
    public async Task SaveCompanyAsync(Company settings)
    {
        if (!PhoneValidator.IsValid(settings.Phone) || !PhoneValidator.IsValid(settings.Cell))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Companies.FirstAsync(c => c.Id == OwnCompanyId);
        entity.CompanyName = settings.CompanyName.Trim();
        entity.Tagline = settings.Tagline;
        entity.AddressLine = settings.AddressLine;
        entity.Phone = settings.Phone;
        entity.Cell = settings.Cell;
        entity.Pan = settings.Pan;
        entity.Gstin = settings.Gstin;
        entity.JurisdictionNote = settings.JurisdictionNote;
        entity.LogoBase64 = settings.LogoBase64;
        entity.LogoFileName = settings.LogoFileName;
        await db.SaveChangesAsync();
    }
}
