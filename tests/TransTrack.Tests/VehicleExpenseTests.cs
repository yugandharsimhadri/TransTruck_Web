using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// A vehicle's paperwork costs. The rule worth pinning down is that a
/// recurring schedule generates one real, dated row per period — because the
/// dashboard's monthly figure counts those rows, and a schedule that
/// generated the wrong dates would quietly move money between months.
/// </summary>
public class VehicleExpenseTests
{
    private static VehicleExpenseService ServiceFor(TestWorld world) => new(world.Factory);

    private static VehicleExpenseSchedule Schedule(TestWorld world, ExpenseRecurrence recurrence,
        DateTime start, DateTime? end, decimal amount = 12000) => new()
        {
            CompanyId = world.CompanyId,
            VehicleId = world.VehicleId,
            Kind = VehicleExpenseKind.Insurance,
            Amount = amount,
            Recurrence = recurrence,
            StartDate = start,
            EndDate = end,
        };

    [Fact]
    public async Task A_one_off_expense_generates_exactly_one_entry_on_its_date()
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        await service.SaveScheduleAsync(
            Schedule(world, ExpenseRecurrence.None, new DateTime(2026, 3, 15), null, 8000));

        var entries = await service.GetExpensesAsync(world.VehicleId);

        var only = Assert.Single(entries);
        Assert.Equal(new DateTime(2026, 3, 15), only.Date);
        Assert.Equal(8000m, only.Amount);
    }

    [Theory]
    [InlineData(ExpenseRecurrence.Monthly, 12)]
    [InlineData(ExpenseRecurrence.Quarterly, 4)]
    [InlineData(ExpenseRecurrence.Yearly, 1)]
    public async Task A_year_long_schedule_generates_one_entry_per_period(
        ExpenseRecurrence recurrence, int expected)
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        // 1 Jan through 31 Dec — a full year, inclusive.
        await service.SaveScheduleAsync(Schedule(world, recurrence,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)));

        var entries = await service.GetExpensesAsync(world.VehicleId);

        Assert.Equal(expected, entries.Count);
        Assert.All(entries, e => Assert.Equal(12000m, e.Amount));
    }

    /// <summary>Stepping by months rather than by a fixed number of days is
    /// what keeps "the 31st" on the 31st. February can't have one, so it
    /// clamps — but the schedule must recover in March rather than drifting
    /// earlier for the rest of the year.</summary>
    [Fact]
    public async Task A_month_end_schedule_clamps_in_february_and_recovers_afterwards()
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        await service.SaveScheduleAsync(Schedule(world, ExpenseRecurrence.Monthly,
            new DateTime(2026, 1, 31), new DateTime(2026, 4, 30)));

        var dates = (await service.GetExpensesAsync(world.VehicleId))
            .Select(e => e.Date).OrderBy(d => d).ToList();

        Assert.Equal(
            [new DateTime(2026, 1, 31), new DateTime(2026, 2, 28), new DateTime(2026, 3, 31), new DateTime(2026, 4, 30)],
            dates);
    }

    /// <summary>Editing a schedule has to rewrite what it produced, or the old
    /// amount keeps counting toward every month it already covered.</summary>
    [Fact]
    public async Task Editing_a_schedule_replaces_the_entries_it_generated()
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        var id = await service.SaveScheduleAsync(Schedule(world, ExpenseRecurrence.Monthly,
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), amount: 1000));

        Assert.Equal(3, (await service.GetExpensesAsync(world.VehicleId)).Count);

        var revised = Schedule(world, ExpenseRecurrence.Monthly,
            new DateTime(2026, 1, 1), new DateTime(2026, 2, 28), amount: 2500);
        revised.Id = id;
        await service.SaveScheduleAsync(revised);

        var entries = await service.GetExpensesAsync(world.VehicleId);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(2500m, e.Amount));
    }

    /// <summary>A row somebody typed in by hand has no schedule behind it and
    /// must survive a schedule being edited or removed.</summary>
    [Fact]
    public async Task A_hand_entered_expense_is_untouched_by_schedule_changes()
    {
        await using var world = await TestWorld.CreateAsync();
        var service = ServiceFor(world);

        await service.SaveExpenseAsync(new VehicleExpense
        {
            CompanyId = world.CompanyId,
            VehicleId = world.VehicleId,
            Kind = VehicleExpenseKind.Other,
            Description = "Fitness certificate",
            Date = new DateTime(2026, 5, 4),
            Amount = 1500,
        });

        var scheduleId = await service.SaveScheduleAsync(Schedule(world, ExpenseRecurrence.Monthly,
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31)));
        await service.DeleteScheduleAsync(scheduleId);

        var survivor = Assert.Single(await service.GetExpensesAsync(world.VehicleId));
        Assert.Equal("Fitness certificate", survivor.Description);
        Assert.Null(survivor.ScheduleId);
    }

    [Fact]
    public async Task A_repeating_schedule_without_an_end_date_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServiceFor(world).SaveScheduleAsync(
                Schedule(world, ExpenseRecurrence.Monthly, new DateTime(2026, 1, 1), null)));

        Assert.Contains("end date", error.Message);
    }

    [Fact]
    public async Task An_absurdly_long_schedule_is_refused_rather_than_writing_thousands_of_rows()
    {
        await using var world = await TestWorld.CreateAsync();

        // A mistyped year rather than a real commitment.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServiceFor(world).SaveScheduleAsync(Schedule(world, ExpenseRecurrence.Monthly,
                new DateTime(2026, 1, 1), new DateTime(2126, 1, 1))));

        Assert.Contains("more than this is meant for", error.Message);
        Assert.Empty(await ServiceFor(world).GetExpensesAsync(world.VehicleId));
    }
}

/// <summary>Two drivers with the same name or number inside one company is
/// nearly always the same person entered twice, and once that happens their
/// trip history splits across both rows.</summary>
public class DriverDuplicateTests
{
    private static Driver New(string name, string phone) => new() { Name = name, Phone = phone, Salary = 20000 };

    [Fact]
    public async Task A_duplicate_driver_name_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000001"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000002")));

        Assert.Contains("already a driver here", error.Message);
    }

    [Fact]
    public async Task The_name_check_ignores_case_and_surrounding_space()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000001"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Drivers.SaveDriverAsync(New("  ravi kumar  ", "9000000002")));
    }

    [Fact]
    public async Task A_duplicate_phone_number_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        await world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000001"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Drivers.SaveDriverAsync(New("Suresh Babu", "9000000001")));

        Assert.Contains("already has the number", error.Message);
    }

    /// <summary>Editing a driver must not trip over that driver's own name.</summary>
    [Fact]
    public async Task Saving_an_existing_driver_unchanged_is_allowed()
    {
        await using var world = await TestWorld.CreateAsync();
        var id = await world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000001"));

        var again = New("Ravi Kumar", "9000000001");
        again.Id = id;

        await world.Drivers.SaveDriverAsync(again); // must not throw
    }

    /// <summary>A deactivated driver is still the same person, so their name
    /// stays taken — but a deleted one frees it up again.</summary>
    [Fact]
    public async Task A_deactivated_driver_still_holds_its_name_but_a_deleted_one_does_not()
    {
        await using var world = await TestWorld.CreateAsync();

        var id = await world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000001"));

        var deactivated = New("Ravi Kumar", "9000000001");
        deactivated.Id = id;
        deactivated.IsActive = false;
        await world.Drivers.SaveDriverAsync(deactivated);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000003")));

        await world.Drivers.DeleteDriverAsync(id);

        await world.Drivers.SaveDriverAsync(New("Ravi Kumar", "9000000003")); // must not throw
    }
}
