namespace TransTrack.Tests;

/// <summary>
/// Document numbers have to be sequential and unique per company — they are
/// LR and bill numbers a business has to account for. The desktop app got
/// this for free by having one user; the API serves concurrent requests, so
/// the guarantee now has to be tested rather than assumed.
/// </summary>
public class DocumentNumberingTests
{
    [Fact]
    public async Task An_lr_number_is_assigned_on_first_print()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        var (lrNo, isFirstPrint) = await world.Trips.AssignLrNumberAsync(tripId);

        Assert.Equal("LR00001", lrNo);
        Assert.True(isFirstPrint);
    }

    [Fact]
    public async Task Reprinting_reuses_the_same_lr_number()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        var first = await world.Trips.AssignLrNumberAsync(tripId);
        var second = await world.Trips.AssignLrNumberAsync(tripId);

        // A reprint is the same document, so it must not consume a new number
        // or the company's LR book develops phantom gaps.
        Assert.Equal(first.LrNo, second.LrNo);
        Assert.True(first.IsFirstPrint);
        Assert.False(second.IsFirstPrint);
    }

    [Fact]
    public async Task Lr_and_bill_numbers_run_on_separate_sequences()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        var (lrNo, _) = await world.Trips.AssignLrNumberAsync(tripId);
        var (billNo, _) = await world.Trips.AssignBillNumberAsync(tripId);

        Assert.Equal("LR00001", lrNo);
        Assert.Equal("BILL00001", billNo);
    }

    [Fact]
    public async Task Numbers_stay_sequential_across_many_trips()
    {
        await using var world = await TestWorld.CreateAsync();

        var numbers = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var tripId = await world.BookTripAsync();
            var trip = await world.Trips.GetTripAsync(tripId);
            numbers.Add(trip!.TripNo);
        }

        Assert.Equal(["TRP00001", "TRP00002", "TRP00003", "TRP00004", "TRP00005"], numbers);
    }

    [Fact]
    public async Task Concurrent_bookings_never_produce_a_duplicate_trip_number()
    {
        await using var world = await TestWorld.CreateAsync();

        // The regression this guards: the counter used to be read, incremented
        // in memory and flushed by the caller, so two simultaneous bookings
        // both took the same number and the unique index rejected one with a
        // raw database error. The counter is now a concurrency token and the
        // allocation retries.
        const int simultaneous = 8;

        var bookings = Enumerable.Range(0, simultaneous)
            .Select(_ => world.BookTripAsync(amount: 1000))
            .ToArray();

        var ids = await Task.WhenAll(bookings);

        var trips = (await world.Trips.GetTripListAsync()).Items;
        var numbers = trips.Where(t => ids.Contains(t.Id)).Select(t => t.TripNo).ToList();

        Assert.Equal(simultaneous, numbers.Count);
        Assert.Equal(simultaneous, numbers.Distinct().Count());
    }

    [Fact]
    public async Task Concurrent_lr_prints_on_different_trips_get_different_numbers()
    {
        await using var world = await TestWorld.CreateAsync();

        var tripIds = new List<Guid>();
        for (var i = 0; i < 6; i++) tripIds.Add(await world.BookTripAsync());

        var results = await Task.WhenAll(tripIds.Select(id => world.Trips.AssignLrNumberAsync(id)));

        var lrNumbers = results.Select(r => r.LrNo).ToList();
        Assert.Equal(6, lrNumbers.Distinct().Count());
    }

    [Fact]
    public async Task Numbering_a_trip_that_does_not_exist_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Trips.AssignLrNumberAsync(Guid.NewGuid()));
    }
}
