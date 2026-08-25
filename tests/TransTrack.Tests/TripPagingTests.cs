using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// The trips list is paged, which means the filters and the sort have to be
/// applied by the database across every matching row — not by the browser
/// across whichever rows a page happened to contain. These tests pin that
/// down, because the failure mode is silent: the list simply shows the wrong
/// trips, and looks perfectly normal doing it.
/// </summary>
[Collection(ProcessStateCollection.Name)]
public class TripPagingTests
{
    [Fact]
    public async Task A_page_returns_its_slice_and_the_total_behind_it()
    {
        await using var world = await TestWorld.CreateAsync();
        for (var i = 0; i < 12; i++) await world.BookTripAsync(amount: 1000 + i);

        var page = await world.Trips.GetTripListAsync(skip: 0, take: 5);

        Assert.Equal(5, page.Items.Count);
        Assert.Equal(12, page.Total);
    }

    [Fact]
    public async Task Paging_walks_every_trip_exactly_once()
    {
        await using var world = await TestWorld.CreateAsync();
        for (var i = 0; i < 12; i++) await world.BookTripAsync(amount: 1000 + i);

        var seen = new List<Guid>();
        for (var skip = 0; skip < 12; skip += 5)
            seen.AddRange((await world.Trips.GetTripListAsync(skip: skip, take: 5)).Items.Select(t => t.Id));

        // No duplicates and nothing missed — what an unstable sort would break.
        Assert.Equal(12, seen.Count);
        Assert.Equal(12, seen.Distinct().Count());
    }

    [Fact]
    public async Task Filtering_by_status_searches_every_trip_not_just_the_first_page()
    {
        await using var world = await TestWorld.CreateAsync();

        // One closed trip, then enough open ones to push it well past the end
        // of the first page. Filtering client-side would look at the first
        // five open trips, find no closed one, and report none at all.
        var closed = await world.BookTripAsync(amount: 500);
        await world.Trips.CloseAsync(closed, world.UserId);
        for (var i = 0; i < 10; i++) await world.BookTripAsync(amount: 1000 + i);

        var page = await world.Trips.GetTripListAsync(status: TripStatus.Closed, skip: 0, take: 5);

        Assert.Equal(1, page.Total);
        Assert.Equal(closed, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Sorting_orders_every_trip_not_just_the_first_page()
    {
        await using var world = await TestWorld.CreateAsync();
        for (var i = 1; i <= 10; i++) await world.BookTripAsync(amount: i * 1000);

        var page = await world.Trips.GetTripListAsync(sort: TripListSort.AmountDesc, skip: 0, take: 3);

        // The largest amount is on the last trip booked; a page sorted after
        // slicing would surface whichever three came back first instead.
        Assert.Equal(10000m, page.Items[0].Amount);
        Assert.Equal(9000m, page.Items[1].Amount);
        Assert.Equal(8000m, page.Items[2].Amount);
    }

    [Fact]
    public async Task A_take_beyond_the_ceiling_is_clamped()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.BookTripAsync();

        // The clamp is the point of paging: no caller, however it asks, gets a
        // response that grows without limit.
        var page = await world.Trips.GetTripListAsync(take: 10_000);

        Assert.True(page.Items.Count <= TripService.MaxPageSize);
    }

    [Fact]
    public async Task The_total_counts_matches_not_the_page()
    {
        await using var world = await TestWorld.CreateAsync();
        var closed = await world.BookTripAsync();
        await world.Trips.CloseAsync(closed, world.UserId);
        for (var i = 0; i < 4; i++) await world.BookTripAsync();

        var open = await world.Trips.GetTripListAsync(status: TripStatus.Open, take: 2);

        Assert.Equal(2, open.Items.Count);
        Assert.Equal(4, open.Total);
    }
}
