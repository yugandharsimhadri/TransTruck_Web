using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class DriverService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>Active drivers by default. A retired driver stays in the
    /// database — their ledger and every trip they ran still refer to them —
    /// but they are not someone you assign new work to, so they are off the
    /// list unless it is asked for them explicitly.</summary>
    public async Task<List<Driver>> GetDriversAsync(bool includeInactive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Drivers.AsNoTracking().Where(d => !d.IsDeleted);
        if (!includeInactive) query = query.Where(d => d.IsActive);
        return await query.OrderBy(d => d.Name).ToListAsync();
    }

    /// <summary>Returns the saved driver id — the caller needs it for a newly
    /// created driver, since documents are stored against that id and there is
    /// nothing to attach one to until the row exists.</summary>
    public async Task<Guid> SaveDriverAsync(Driver driver)
    {
        var name = (driver.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter the driver's name.");

        if (!PhoneValidator.IsValid(driver.Phone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        await using var db = await factory.CreateDbContextAsync();

        // Two drivers with the same name, or the same number, inside one
        // company is nearly always the same person entered twice — and once
        // that happens the trip history splits across both, which no report
        // can put back together. Scoped to the company by the global tenant
        // filter, so another company having a driver by the same name is
        // none of this company's business.
        //
        // Deleted rows are excluded deliberately: a name freed up by removing
        // a driver should be usable again. Deactivated ones still count —
        // they are the same person, just not driving at the moment.
        var lowerName = name.ToLower();
        if (await db.Drivers.AnyAsync(d => d.Id != driver.Id && !d.IsDeleted && d.Name.ToLower() == lowerName))
            throw new InvalidOperationException($"'{name}' is already a driver here. Use a different name, or edit the existing one.");

        if (await db.Drivers.AnyAsync(d => d.Id != driver.Id && !d.IsDeleted && d.Phone == driver.Phone))
            throw new InvalidOperationException($"Another driver already has the number {driver.Phone}.");

        var entity = driver.Id == Guid.Empty ? null : await db.Drivers.FirstOrDefaultAsync(x => x.Id == driver.Id);
        var isNew = entity is null;
        entity ??= new Driver();

        entity.Name = name;
        entity.Phone = driver.Phone;
        entity.Salary = driver.Salary;
        entity.JoiningDate = driver.JoiningDate;
        entity.IsActive = driver.IsActive;

        if (isNew)
        {
            entity.EmployeeCode = await NumberService.NextAsync(db, NumberService.Employee);
            db.Drivers.Add(entity);
        }

        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteDriverAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Drivers.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }
}
