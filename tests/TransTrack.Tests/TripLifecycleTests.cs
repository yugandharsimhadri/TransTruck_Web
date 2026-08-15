using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>Booking, closing, reopening, and the rule that a settled trip
/// stops accepting changes until someone deliberately reopens it.</summary>
public class TripLifecycleTests
{
    [Fact]
    public async Task Booking_a_trip_assigns_a_sequential_trip_number()
    {
        await using var world = await TestWorld.CreateAsync();

        var first = await world.BookTripAsync();
        var second = await world.BookTripAsync();

        var a = await world.Trips.GetTripAsync(first);
        var b = await world.Trips.GetTripAsync(second);

        Assert.Equal("TRP00001", a!.TripNo);
        Assert.Equal("TRP00002", b!.TripNo);
    }

    [Fact]
    public async Task A_trip_starts_open()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        var trip = await world.Trips.GetTripAsync(tripId);

        Assert.Equal(TripStatus.Open, trip!.Status);
    }

    [Fact]
    public async Task Expenses_and_amounts_can_be_added_while_the_trip_is_open()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync(amount: 10000);

        await world.AddExpenseAsync(tripId, 500);
        await world.AddAmountAsync(tripId, 4000);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Single(trip!.Expenses);
        Assert.Single(trip.Transactions);
    }

    [Fact]
    public async Task Closing_a_trip_records_who_closed_it_and_when()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();

        await world.Trips.CloseAsync(tripId, world.UserId);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(TripStatus.Closed, trip!.Status);
        Assert.Equal(world.UserId, trip.ClosedByUserId);
        Assert.NotNull(trip.ClosedOn);
    }

    // ── Negative: the closed-trip lock ───────────────────────────────────

    [Fact]
    public async Task Adding_an_expense_to_a_closed_trip_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.Trips.CloseAsync(tripId, world.UserId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.AddExpenseAsync(tripId, 500));

        Assert.Equal(TripService.ClosedTripMessage, error.Message);
    }

    [Fact]
    public async Task Adding_an_amount_to_a_closed_trip_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.Trips.CloseAsync(tripId, world.UserId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.AddAmountAsync(tripId, 1000));

        Assert.Equal(TripService.ClosedTripMessage, error.Message);
    }

    [Fact]
    public async Task Deleting_an_expense_from_a_closed_trip_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.AddExpenseAsync(tripId, 500);

        var trip = await world.Trips.GetTripAsync(tripId);
        var expenseId = trip!.Expenses.First().Id;

        await world.Trips.CloseAsync(tripId, world.UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Trips.DeleteExpenseAsync(expenseId));
    }

    [Fact]
    public async Task Reopening_a_trip_lets_changes_through_again()
    {
        await using var world = await TestWorld.CreateAsync();
        var tripId = await world.BookTripAsync();
        await world.Trips.CloseAsync(tripId, world.UserId);

        await world.Trips.ReopenAsync(tripId);
        await world.AddExpenseAsync(tripId, 750);

        var trip = await world.Trips.GetTripAsync(tripId);
        Assert.Equal(TripStatus.Open, trip!.Status);
        Assert.Null(trip.ClosedOn);
        Assert.Equal(750m, trip.TotalExpenses);
    }

    [Fact]
    public async Task Adding_an_expense_to_a_trip_that_does_not_exist_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.AddExpenseAsync(Guid.NewGuid(), 100));
    }

    // ── Negative: required fields ────────────────────────────────────────

    [Theory]
    [InlineData("vehicle")]
    [InlineData("driver")]
    [InlineData("party")]
    [InlineData("fromCity")]
    [InlineData("toCity")]
    public async Task Booking_without_a_required_field_is_refused(string missing)
    {
        await using var world = await TestWorld.CreateAsync();

        var trip = new Trip
        {
            Date = DateTime.Today,
            VehicleId = missing == "vehicle" ? Guid.Empty : world.VehicleId,
            DriverId = missing == "driver" ? Guid.Empty : world.DriverId,
            PartyId = missing == "party" ? Guid.Empty : world.PartyId,
            FromCityId = missing == "fromCity" ? Guid.Empty : world.FromCityId,
            ToCityId = missing == "toCity" ? Guid.Empty : world.ToCityId,
            ConsignorName = "Consignor",
            ConsigneeName = "Consignee",
            Amount = 1000
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Trips.SaveTripAsync(trip));
    }

    /// <summary>Consignor and consignee used to be mandatory. Plenty of real
    /// trips are booked against the billing party alone with no separate
    /// consignor/consignee to name, so a booking that leaves both blank has
    /// to go through — and the blanks have to survive the round trip rather
    /// than being rejected or quietly filled in.</summary>
    [Fact]
    public async Task Booking_without_consignor_or_consignee_is_allowed()
    {
        await using var world = await TestWorld.CreateAsync();

        var id = await world.Trips.SaveTripAsync(new Trip
        {
            Date = DateTime.Today,
            VehicleId = world.VehicleId,
            DriverId = world.DriverId,
            PartyId = world.PartyId,
            FromCityId = world.FromCityId,
            ToCityId = world.ToCityId,
            ConsignorName = "",
            ConsigneeName = "",
            Amount = 1000
        });

        var trip = await world.Trips.GetTripAsync(id);
        Assert.NotNull(trip);
        Assert.Equal("", trip!.ConsignorName);
        Assert.Equal("", trip.ConsigneeName);
    }

    /// <summary>The way bill number is optional and, when given, is kept
    /// verbatim for the LR to print — blank must persist as null rather than
    /// an empty string, so "has a way bill number" is a single clean check.</summary>
    [Theory]
    [InlineData("WB-4471", "WB-4471")]
    [InlineData("  WB-88  ", "WB-88")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task Way_bill_number_is_optional_and_trimmed(string? entered, string? expected)
    {
        await using var world = await TestWorld.CreateAsync();

        var id = await world.Trips.SaveTripAsync(new Trip
        {
            Date = DateTime.Today,
            VehicleId = world.VehicleId,
            DriverId = world.DriverId,
            PartyId = world.PartyId,
            FromCityId = world.FromCityId,
            ToCityId = world.ToCityId,
            ConsignorName = "Consignor",
            ConsigneeName = "Consignee",
            WayBillNo = entered,
            Amount = 1000
        });

        var trip = await world.Trips.GetTripAsync(id);
        Assert.Equal(expected, trip!.WayBillNo);
    }
}
