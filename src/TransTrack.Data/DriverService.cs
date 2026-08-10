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

    public async Task SaveDriverAsync(Driver driver)
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
