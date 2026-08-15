using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>The two new reports — party-wise and vehicle savings — plus the
/// thing that prompted them: a report has to return every matching row, not
/// just the first.</summary>
public class ReportTests
{
    private static ReportsService ServiceFor(TestWorld world) => new(world.Factory);

    /// <summary>The complaint behind this was "the report only exports one
    /// trip". It turned out to be a company that genuinely had one trip, but
    /// the guarantee is worth pinning down: every trip in range comes back.</summary>
    [Fact]
    public async Task Trips_report_returns_every_trip_not_just_the_first()
    {
        await using var world = await TestWorld.CreateAsync();

        for (var i = 0; i < 12; i++) await world.BookTripAsync(1000 + i);

        var trips = await ServiceFor(world).GetTripsAsync(null, null, null, null);

        Assert.Equal(12, trips.Count);
    }

    [Fact]
    public async Task Party_report_numbers_rows_from_one_and_totals_the_amounts()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.BookTripAsync(5000);
        await world.BookTripAsync(7500);
        await world.BookTripAsync(2500);

        var report = await ServiceFor(world).GetPartyReportAsync(world.PartyId, null, null);

        Assert.Equal("Test Party", report.PartyName);
        Assert.Equal(3, report.Rows.Count);
        Assert.Equal([1, 2, 3], report.Rows.Select(r => r.SerialNo));
        Assert.Equal(15000m, report.Total);
    }

    /// <summary>Only that party's trips — a statement handed to one customer
    /// must never carry another customer's freight on it.</summary>
    [Fact]
    public async Task Party_report_excludes_other_parties_trips()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.BookTripAsync(5000);

        Guid otherPartyId;
        await using (var db = await world.Factory.CreateDbContextAsync())
        {
            var other = new Party { CompanyId = world.CompanyId, Name = "Other Party", Phone = "9111111111" };
            db.Parties.Add(other);
            await db.SaveChangesAsync();
            otherPartyId = other.Id;
        }

        await world.Trips.SaveTripAsync(new Trip
        {
            Date = DateTime.Today,
            VehicleId = world.VehicleId,
            DriverId = world.DriverId,
            PartyId = otherPartyId,
            FromCityId = world.FromCityId,
            ToCityId = world.ToCityId,
            Amount = 99999
        });

        var report = await ServiceFor(world).GetPartyReportAsync(world.PartyId, null, null);

        Assert.Single(report.Rows);
        Assert.Equal(5000m, report.Total);
    }

    [Fact]
    public async Task Party_report_respects_the_date_range()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.BookTripAsync(1000);

        var report = await ServiceFor(world).GetPartyReportAsync(
            world.PartyId, DateTime.Today.AddDays(1), DateTime.Today.AddDays(30));

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.Total);
    }

    /// <summary>A range inside one calendar month prints as that month, the
    /// way the customer's existing paper report is titled.</summary>
    [Theory]
    [InlineData("2026-07-01", "2026-07-31", "JULY-2026")]
    [InlineData("2026-07-01", "2026-08-31", "01-Jul-2026 to 31-Aug-2026")]
    public void Period_label_reads_as_a_month_when_the_range_is_one_month(string from, string to, string expected)
    {
        var label = ReportsService.DescribePeriod(DateTime.Parse(from), DateTime.Parse(to));
        Assert.Equal(expected, label);
    }

    [Fact]
    public void Period_label_covers_the_open_ended_cases()
    {
        Assert.Equal("ALL DATES", ReportsService.DescribePeriod(null, null));
        Assert.StartsWith("From ", ReportsService.DescribePeriod(DateTime.Today, null));
        Assert.StartsWith("To ", ReportsService.DescribePeriod(null, DateTime.Today));
    }

    /// <summary>Saving is what the company actually kept: revenue less trip
    /// expenses *and* less maintenance, so an expensive repair shows up as
    /// the month it really was.</summary>
    [Fact]
    public async Task Vehicle_savings_subtracts_both_expenses_and_maintenance()
    {
        await using var world = await TestWorld.CreateAsync();

        var tripId = await world.BookTripAsync(20000);
        await world.AddExpenseAsync(tripId, 5000);
        await world.Maintenance.SaveAsync(new VehicleMaintenance
        {
            CompanyId = world.CompanyId,
            VehicleId = world.VehicleId,
            Date = DateTime.Today,
            MaintenanceCategoryId = world.MaintenanceCategoryId,
            Amount = 3000
        });

        var rows = await ServiceFor(world).GetVehicleSavingsAsync(null, null, null);
        var row = Assert.Single(rows);

        Assert.Equal(1, row.Trips);
        Assert.Equal(20000m, row.Revenue);
        Assert.Equal(5000m, row.TripExpenses);
        Assert.Equal(3000m, row.MaintenanceCost);
        Assert.Equal(12000m, row.Saving);
        Assert.Equal(12000m, row.SavingPerTrip);
    }

    /// <summary>An other-owner vehicle's freight is collected on that owner's
    /// behalf, so only the commission is the company's saving — the same rule
    /// the rest of the app's accounting already follows.</summary>
    [Fact]
    public async Task Vehicle_savings_counts_only_commission_for_another_owners_vehicle()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.BookTripAsync(50000, world.OtherOwnerVehicleId, commission: 4000);

        var rows = await ServiceFor(world).GetVehicleSavingsAsync(null, null, null);
        var row = Assert.Single(rows);

        Assert.Equal(4000m, row.Revenue);
        Assert.Equal(0m, row.TripExpenses);
        Assert.Equal(4000m, row.Saving);
    }

    /// <summary>A month with maintenance but no trips is still a real cost,
    /// and dropping it would overstate what the vehicle saved.</summary>
    [Fact]
    public async Task Vehicle_savings_includes_a_month_with_maintenance_but_no_trips()
    {
        await using var world = await TestWorld.CreateAsync();

        await world.Maintenance.SaveAsync(new VehicleMaintenance
        {
            CompanyId = world.CompanyId,
            VehicleId = world.VehicleId,
            Date = DateTime.Today,
            MaintenanceCategoryId = world.MaintenanceCategoryId,
            Amount = 2500
        });

        var rows = await ServiceFor(world).GetVehicleSavingsAsync(null, null, null);
        var row = Assert.Single(rows);

        Assert.Equal(0, row.Trips);
        Assert.Equal(-2500m, row.Saving);
        Assert.Equal(0m, row.SavingPerTrip);
    }
}
