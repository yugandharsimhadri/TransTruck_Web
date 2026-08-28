using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Tests;

/// <summary>
/// The multi-tenant guarantee: one onboarded company can never see or touch
/// another's data. This is enforced by a global query filter rather than by
/// remembering a WHERE clause in each method, and these tests exist to prove
/// the filter is actually doing that — including the fail-safe case where
/// there is no signed-in company at all, which must show nothing rather than
/// everything.
/// </summary>
public class TenantIsolationTests
{
    [Fact]
    public async Task A_company_sees_only_its_own_trips()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.BookTripAsync(amount: 1000);

        var rival = await world.AddRivalCompanyAsync();

        // Book a trip as the rival company.
        world.CurrentUser.CompanyId = rival.CompanyId;
        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            db.Trips.Add(new Trip
            {
                CompanyId = rival.CompanyId,
                TripNo = "TRP99999",
                VehicleId = rival.VehicleId,
                DriverId = world.DriverId,
                PartyId = world.PartyId,
                FromCityId = world.FromCityId,
                ToCityId = world.ToCityId,
                ConsignorName = "Rival", ConsigneeName = "Rival",
                Amount = 5000
            });
            await db.SaveChangesAsync();
        }

        // Back as our own company: the rival's trip must be invisible.
        world.CurrentUser.CompanyId = world.CompanyId;
        var ours = (await world.Trips.GetTripListAsync()).Items;

        Assert.Single(ours);
        Assert.DoesNotContain(ours, t => t.TripNo == "TRP99999");
    }

    [Fact]
    public async Task A_company_cannot_fetch_another_companys_trip_by_id()
    {
        await using var world = await TestWorld.CreateAsync();
        var rival = await world.AddRivalCompanyAsync();

        Guid rivalTripId;
        world.CurrentUser.CompanyId = rival.CompanyId;
        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            var trip = new Trip
            {
                CompanyId = rival.CompanyId,
                TripNo = "TRP99999",
                VehicleId = rival.VehicleId,
                DriverId = world.DriverId,
                PartyId = world.PartyId,
                FromCityId = world.FromCityId,
                ToCityId = world.ToCityId,
                ConsignorName = "Rival", ConsigneeName = "Rival",
                Amount = 5000
            };
            db.Trips.Add(trip);
            await db.SaveChangesAsync();
            rivalTripId = trip.Id;
        }

        world.CurrentUser.CompanyId = world.CompanyId;

        // Knowing the id must not be enough — this is the case a hand-written
        // "scope by company" is most likely to miss.
        Assert.Null(await world.Trips.GetTripAsync(rivalTripId));
    }

    [Fact]
    public async Task With_no_signed_in_company_nothing_is_visible()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.BookTripAsync();

        world.CurrentUser.CompanyId = null;

        // Fails safe to empty, never to "everything".
        Assert.Empty((await world.Trips.GetTripListAsync()).Items);
    }

    [Fact]
    public async Task A_new_row_is_stamped_with_the_signed_in_company()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        await using var db = await world.Factory.CreateDbContextAsync();
        var stored = await db.Trips.IgnoreQueryFilters().SingleAsync(t => t.Id == tripId);

        Assert.Equal(world.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Each_company_numbers_its_own_trips_from_one()
    {
        await using var world = await TestWorld.CreateAsync();
        var ourFirst = await world.BookTripAsync();

        var rival = await world.AddRivalCompanyAsync();
        world.CurrentUser.CompanyId = rival.CompanyId;

        // The counter is per company, so the rival's first trip is also
        // TRP00001 — numbering must not leak across tenants.
        await using var db = await world.Factory.CreateDbContextAsync();
        var rivalNumber = await Data.NumberService.NextAsync(db, Data.NumberService.Trip);

        world.CurrentUser.CompanyId = world.CompanyId;
        var ourTrip = await world.Trips.GetTripAsync(ourFirst);

        Assert.Equal("TRP00001", ourTrip!.TripNo);
        Assert.Equal("TRP00001", rivalNumber);
    }
}
