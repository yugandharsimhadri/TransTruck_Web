using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// Retired masters leave the lists you choose from, and come back when asked
/// for.
///
/// The risk in a default like this is not that it filters, but that something
/// which needs the full list quietly stops seeing half its data — a report over
/// a lorry since sold, a former driver's ledger. So both directions are pinned:
/// off by default, complete when requested.
/// </summary>
public class ActiveFilterTests
{
    private static async Task RetireAsync<T>(TestWorld world, Guid id) where T : class
    {
        await using var db = await world.Factory.CreateDbContextAsync();
        var entity = await db.Set<T>().FindAsync(id);
        entity!.GetType().GetProperty("IsActive")!.SetValue(entity, false);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_retired_driver_leaves_the_list_but_is_still_reachable()
    {
        await using var world = await TestWorld.CreateAsync();
        await RetireAsync<Driver>(world, world.DriverId);

        Assert.DoesNotContain(await world.Drivers.GetDriversAsync(), d => d.Id == world.DriverId);
        Assert.Contains(await world.Drivers.GetDriversAsync(includeInactive: true), d => d.Id == world.DriverId);
    }

    [Fact]
    public async Task A_retired_vehicle_leaves_the_list_but_is_still_reachable()
    {
        await using var world = await TestWorld.CreateAsync();
        await RetireAsync<Vehicle>(world, world.VehicleId);

        Assert.DoesNotContain(await world.Vehicles.GetVehiclesAsync(), v => v.Id == world.VehicleId);
        Assert.Contains(await world.Vehicles.GetVehiclesAsync(includeInactive: true), v => v.Id == world.VehicleId);
    }

    [Fact]
    public async Task A_retired_place_leaves_the_list_but_is_still_reachable()
    {
        await using var world = await TestWorld.CreateAsync();
        var masters = new MasterDataService(world.Factory, world.CurrentUser);

        await RetireAsync<City>(world, world.FromCityId);

        Assert.DoesNotContain(await masters.GetCitiesAsync(), c => c.Id == world.FromCityId);
        Assert.Contains(await masters.GetCitiesAsync(includeInactive: true), c => c.Id == world.FromCityId);
    }

    [Fact]
    public async Task Everything_is_active_to_begin_with()
    {
        await using var world = await TestWorld.CreateAsync();
        var masters = new MasterDataService(world.Factory, world.CurrentUser);

        // The migration defaults these to true rather than false, so an
        // existing installation does not wake up with empty route pickers.
        Assert.All(await masters.GetCitiesAsync(includeInactive: true), c => Assert.True(c.IsActive));
        Assert.All(await masters.GetStatesAsync(includeInactive: true), s => Assert.True(s.IsActive));
    }

    [Fact]
    public async Task Retiring_a_driver_does_not_touch_the_trips_they_ran()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10_000m);

        await RetireAsync<Driver>(world, world.DriverId);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(world.DriverId, trip!.DriverId);
        Assert.Single((await world.Trips.GetTripListAsync()).Items, r => r.Id == tripId);
    }
}

/// <summary>
/// The trips list, narrowed. Filtering happens in the database rather than the
/// browser — a filter applied to the pages already fetched would silently
/// answer for a fraction of the data and look no different.
/// </summary>
public class TripListFilterTests
{
    private static async Task<Guid> AddPartyAsync(TestWorld world, string name)
    {
        await using var db = await world.Factory.CreateDbContextAsync();
        var party = new Party { CompanyId = world.CompanyId, Name = name, Phone = "9111100009" };
        db.Parties.Add(party);
        await db.SaveChangesAsync();
        return party.Id;
    }

    private static async Task<Guid> BookOnAsync(TestWorld world, DateTime date, Guid partyId)
    {
        return await world.Trips.SaveTripAsync(new Trip
        {
            Date = date,
            VehicleId = world.VehicleId,
            DriverId = world.DriverId,
            PartyId = partyId,
            FromCityId = world.FromCityId,
            ToCityId = world.ToCityId,
            ConsignorName = "Consignor",
            ConsigneeName = "Consignee",
            Amount = 5_000m,
        });
    }

    [Fact]
    public async Task Trips_are_listed_oldest_first()
    {
        await using var world = await TestWorld.CreateAsync();
        var newer = await BookOnAsync(world, new DateTime(2026, 9, 20), world.PartyId);
        var older = await BookOnAsync(world, new DateTime(2026, 9, 1), world.PartyId);

        var items = (await world.Trips.GetTripListAsync()).Items;

        Assert.Equal(older, items.First().Id);
        Assert.Equal(newer, items.Last().Id);
    }

    [Fact]
    public async Task A_party_filter_returns_only_that_partys_trips()
    {
        await using var world = await TestWorld.CreateAsync();
        var other = await AddPartyAsync(world, "Other Party");

        var mine = await BookOnAsync(world, new DateTime(2026, 9, 5), world.PartyId);
        await BookOnAsync(world, new DateTime(2026, 9, 6), other);

        var page = await world.Trips.GetTripListAsync(partyId: world.PartyId);

        Assert.Equal(mine, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task The_date_range_includes_trips_on_both_end_dates()
    {
        await using var world = await TestWorld.CreateAsync();
        var first = await BookOnAsync(world, new DateTime(2026, 9, 1), world.PartyId);
        var last = await BookOnAsync(world, new DateTime(2026, 9, 30), world.PartyId);
        await BookOnAsync(world, new DateTime(2026, 10, 1), world.PartyId);

        var page = await world.Trips.GetTripListAsync(
            from: new DateTime(2026, 9, 1), to: new DateTime(2026, 9, 30));

        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, t => t.Id == first);
        Assert.Contains(page.Items, t => t.Id == last);
    }

    [Fact]
    public async Task The_total_counts_the_filtered_set_not_every_trip()
    {
        await using var world = await TestWorld.CreateAsync();
        var other = await AddPartyAsync(world, "Other Party");
        await BookOnAsync(world, new DateTime(2026, 9, 5), world.PartyId);
        await BookOnAsync(world, new DateTime(2026, 9, 6), other);
        await BookOnAsync(world, new DateTime(2026, 9, 7), other);

        // "25 of 312" has to count what the filters match, or paging lies.
        Assert.Equal(2, (await world.Trips.GetTripListAsync(partyId: other)).Total);
    }
}

/// <summary>Settling a period rather than everything a party has ever owed.</summary>
public class SettleablePeriodTests
{
    [Fact]
    public async Task Only_trips_inside_the_period_are_offered()
    {
        await using var world = await TestWorld.CreateAsync();

        async Task<Guid> BookOn(DateTime date) => await world.Trips.SaveTripAsync(new Trip
        {
            Date = date,
            VehicleId = world.VehicleId,
            DriverId = world.DriverId,
            PartyId = world.PartyId,
            FromCityId = world.FromCityId,
            ToCityId = world.ToCityId,
            ConsignorName = "Consignor",
            ConsigneeName = "Consignee",
            Amount = 5_000m,
        });

        var august = await BookOn(new DateTime(2026, 8, 20));
        var september = await BookOn(new DateTime(2026, 9, 10));

        var inSeptember = await world.Settlements.GetSettleableTripsAsync(
            world.PartyId, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));

        Assert.Equal(september, Assert.Single(inSeptember).TripId);

        // And with no period at all, everything still owed.
        var everything = await world.Settlements.GetSettleableTripsAsync(world.PartyId);
        Assert.Equal(2, everything.Count);
        Assert.Contains(everything, t => t.TripId == august);
    }
}
