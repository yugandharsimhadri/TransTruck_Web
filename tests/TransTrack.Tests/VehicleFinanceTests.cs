using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// The vehicle's loan terms. <see cref="VehicleService.SaveVehicleAsync"/>
/// copies field by field onto the tracked row rather than replacing it, so a
/// newly added column is silently dropped until it is added to that list —
/// which is exactly what happened to these five. A round-trip test is the
/// only thing that catches it, since the save still reports success.
/// </summary>
public class VehicleFinanceTests
{
    private static async Task<Vehicle> ReloadAsync(TestWorld world, Guid id)
    {
        await using var db = await world.Factory.CreateDbContextAsync();
        return await db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == id);
    }

    [Fact]
    public async Task Loan_terms_survive_a_save()
    {
        await using var world = await TestWorld.CreateAsync();

        var vehicle = await ReloadAsync(world, world.VehicleId);
        vehicle.LoanAmount = 1_200_000m;
        vehicle.LoanStartDate = new DateTime(2026, 1, 10);
        vehicle.LoanEndDate = new DateTime(2030, 1, 10);
        vehicle.EmiAmount = 28_500m;
        vehicle.EmiDayOfMonth = 10;

        await world.Vehicles.SaveVehicleAsync(vehicle);

        var saved = await ReloadAsync(world, world.VehicleId);
        Assert.Equal(1_200_000m, saved.LoanAmount);
        Assert.Equal(new DateTime(2026, 1, 10), saved.LoanStartDate);
        Assert.Equal(new DateTime(2030, 1, 10), saved.LoanEndDate);
        Assert.Equal(28_500m, saved.EmiAmount);
        Assert.Equal(10, saved.EmiDayOfMonth);
        Assert.True(saved.HasLoan);
    }

    [Fact]
    public async Task A_vehicle_without_a_loan_stays_without_one()
    {
        await using var world = await TestWorld.CreateAsync();

        var saved = await ReloadAsync(world, world.VehicleId);
        Assert.Null(saved.LoanAmount);
        Assert.False(saved.HasLoan);
    }

    [Fact]
    public async Task Clearing_the_loan_clears_every_term()
    {
        await using var world = await TestWorld.CreateAsync();

        var vehicle = await ReloadAsync(world, world.VehicleId);
        vehicle.LoanAmount = 500_000m;
        vehicle.EmiAmount = 9_000m;
        vehicle.EmiDayOfMonth = 5;
        await world.Vehicles.SaveVehicleAsync(vehicle);

        // Paying a loan off has to be expressible, or the vehicle shows an EMI
        // for the rest of its life.
        var cleared = await ReloadAsync(world, world.VehicleId);
        cleared.LoanAmount = null;
        cleared.EmiAmount = null;
        cleared.EmiDayOfMonth = null;
        await world.Vehicles.SaveVehicleAsync(cleared);

        var saved = await ReloadAsync(world, world.VehicleId);
        Assert.Null(saved.LoanAmount);
        Assert.Null(saved.EmiAmount);
        Assert.Null(saved.EmiDayOfMonth);
        Assert.False(saved.HasLoan);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(45, 31)]
    [InlineData(-3, 1)]
    public async Task An_out_of_range_emi_day_is_clamped_rather_than_refused(int given, int expected)
    {
        await using var world = await TestWorld.CreateAsync();

        var vehicle = await ReloadAsync(world, world.VehicleId);
        vehicle.LoanAmount = 100_000m;
        vehicle.EmiDayOfMonth = given;
        await world.Vehicles.SaveVehicleAsync(vehicle);

        Assert.Equal(expected, (await ReloadAsync(world, world.VehicleId)).EmiDayOfMonth);
    }
}
