using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class VehicleService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Vehicle>> GetVehiclesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Vehicles.AsNoTracking().Include(v => v.Owner)
            .Where(v => !v.IsDeleted).OrderBy(v => v.RegNo).ToListAsync();
    }

    public async Task SaveVehicleAsync(Vehicle vehicle)
    {
        if (vehicle.Ownership == VehicleOwnership.Other && vehicle.OwnerId is null)
            throw new InvalidOperationException("An other-owner vehicle needs an owner.");

        await using var db = await factory.CreateDbContextAsync();

        var regNo = vehicle.RegNo.Trim();
        if (await db.Vehicles.AnyAsync(v => v.RegNo == regNo && !v.IsDeleted && v.Id != vehicle.Id))
            throw new InvalidOperationException($"'{regNo}' is already registered.");

        var entity = vehicle.Id == Guid.Empty ? null : await db.Vehicles.FirstOrDefaultAsync(x => x.Id == vehicle.Id);
        var isNew = entity is null;
        entity ??= new Vehicle();

        entity.RegNo = regNo;
        entity.Ownership = vehicle.Ownership;
        entity.OwnerId = vehicle.Ownership == VehicleOwnership.Other ? vehicle.OwnerId : null;
        entity.VehicleType = vehicle.VehicleType;
        entity.Capacity = vehicle.Capacity;
        entity.PermitUpto = vehicle.PermitUpto;
        entity.NationalPermitUpto = vehicle.NationalPermitUpto;
        entity.InsuranceUpto = vehicle.InsuranceUpto;
        entity.FitnessUpto = vehicle.FitnessUpto;
        entity.PollutionUpto = vehicle.PollutionUpto;
        entity.IsActive = vehicle.IsActive;

        if (isNew) db.Vehicles.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteVehicleAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Vehicles.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }
}
