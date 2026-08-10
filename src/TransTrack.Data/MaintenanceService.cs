using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class MaintenanceService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<VehicleMaintenance>> GetForVehicleAsync(Guid vehicleId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.VehicleMaintenances.AsNoTracking()
            .Include(m => m.MaintenanceCategory)
            .Where(m => m.VehicleId == vehicleId && !m.IsDeleted)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task SaveAsync(VehicleMaintenance record)
    {
        if (record.VehicleId == Guid.Empty) throw new InvalidOperationException("Choose a vehicle.");
        if (record.MaintenanceCategoryId == Guid.Empty) throw new InvalidOperationException("Choose a category.");

        await using var db = await factory.CreateDbContextAsync();

        var entity = record.Id == Guid.Empty ? null : await db.VehicleMaintenances.FirstOrDefaultAsync(x => x.Id == record.Id);
        var isNew = entity is null;
        entity ??= new VehicleMaintenance();

        entity.VehicleId = record.VehicleId;
        entity.Date = record.Date;
        entity.MaintenanceCategoryId = record.MaintenanceCategoryId;
        entity.OdometerReading = record.OdometerReading;
        entity.VendorName = record.VendorName;
        entity.Amount = record.Amount;
        entity.NextDueDate = record.NextDueDate;
        entity.NextDueOdometer = record.NextDueOdometer;
        entity.Remarks = record.Remarks;

        if (isNew) db.VehicleMaintenances.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.VehicleMaintenances.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }
}
