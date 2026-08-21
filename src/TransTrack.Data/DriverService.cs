using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class DriverService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Driver>> GetDriversAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Drivers.AsNoTracking().Where(d => !d.IsDeleted).OrderBy(d => d.Name).ToListAsync();
    }

    /// <summary>Returns the saved driver id — the caller needs it for a newly
    /// created driver, since documents are stored against that id and there is
    /// nothing to attach one to until the row exists.</summary>
    public async Task<Guid> SaveDriverAsync(Driver driver)
    {
        if (!PhoneValidator.IsValid(driver.Phone))
            throw new InvalidOperationException("That doesn't look like a valid phone number.");

        await using var db = await factory.CreateDbContextAsync();
        var entity = driver.Id == Guid.Empty ? null : await db.Drivers.FirstOrDefaultAsync(x => x.Id == driver.Id);
        var isNew = entity is null;
        entity ??= new Driver();

        entity.Name = driver.Name.Trim();
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
