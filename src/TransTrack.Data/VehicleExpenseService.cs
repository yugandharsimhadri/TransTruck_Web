using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>
/// A vehicle's paperwork costs — insurance, road tax, and whatever else comes
/// round on a cycle. A schedule describes the commitment; the instalments it
/// generates are what actually count as money spent in a month.
///
/// Generating real rows (rather than working the amounts out on the fly when a
/// report asks) is deliberate: an instalment that was actually paid late, or
/// for a different amount than planned, can be corrected on its own without
/// the correction being wiped out the next time something recalculates.
/// </summary>
public class VehicleExpenseService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>A recurring schedule can't run forever, and a very long one is
    /// nearly always a mistyped end date rather than a real hundred-year
    /// commitment — so it is refused with the count, rather than quietly
    /// writing thousands of rows.</summary>
    public const int MaxInstalments = 600;

    public async Task<List<VehicleExpenseSchedule>> GetSchedulesAsync(Guid vehicleId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.VehicleExpenseSchedules.AsNoTracking()
            .Where(s => s.VehicleId == vehicleId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    /// <summary>Every instalment for one vehicle, newest first — what the
    /// vehicle screen lists under its expenses.</summary>
    public async Task<List<VehicleExpense>> GetExpensesAsync(Guid vehicleId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.VehicleExpenses.AsNoTracking()
            .Where(x => x.VehicleId == vehicleId && !x.IsDeleted)
            .OrderByDescending(x => x.Date)
            .ToListAsync();
    }

    /// <summary>Creates or replaces a schedule and the instalments it implies.
    ///
    /// On an edit the previously generated rows are removed and rewritten,
    /// which is the only way a changed amount or end date can be reflected —
    /// but only the generated ones. Anything entered by hand has a null
    /// ScheduleId and is never touched.</summary>
    public async Task<Guid> SaveScheduleAsync(VehicleExpenseSchedule schedule)
    {
        if (schedule.Amount <= 0)
            throw new InvalidOperationException("Enter an amount greater than zero.");

        if (schedule.Recurrence != ExpenseRecurrence.None)
        {
            if (schedule.EndDate is null)
                throw new InvalidOperationException("A repeating expense needs an end date, otherwise it would never stop.");

            if (schedule.EndDate.Value.Date < schedule.StartDate.Date)
                throw new InvalidOperationException("The end date is before the start date.");
        }

        var dates = InstalmentDates(schedule).ToList();
        if (dates.Count > MaxInstalments)
            throw new InvalidOperationException(
                $"That works out to {dates.Count:N0} entries, which is more than this is meant for. " +
                "Check the end date and how often it repeats.");

        await using var db = await factory.CreateDbContextAsync();

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == schedule.VehicleId && !v.IsDeleted)
                      ?? throw new InvalidOperationException("Vehicle not found.");

        var entity = schedule.Id == Guid.Empty
            ? null
            : await db.VehicleExpenseSchedules.Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == schedule.Id && !s.IsDeleted);

        var isNew = entity is null;
        entity ??= new VehicleExpenseSchedule { Id = Guid.NewGuid(), VehicleId = vehicle.Id };

        entity.Kind = schedule.Kind;
        entity.Description = string.IsNullOrWhiteSpace(schedule.Description) ? null : schedule.Description.Trim();
        entity.Amount = schedule.Amount;
        entity.Recurrence = schedule.Recurrence;
        entity.StartDate = schedule.StartDate.Date;
        entity.EndDate = schedule.EndDate?.Date;
        entity.Remarks = string.IsNullOrWhiteSpace(schedule.Remarks) ? null : schedule.Remarks.Trim();

        if (isNew) db.VehicleExpenseSchedules.Add(entity);

        // Soft-delete rather than remove: the audit trail has to be able to
        // show what a schedule used to produce, which a hard delete erases.
        if (!isNew)
        {
            foreach (var existing in entity.Entries.Where(x => !x.IsDeleted))
                existing.IsDeleted = true;
        }

        foreach (var date in dates)
        {
            db.VehicleExpenses.Add(new VehicleExpense
            {
                Id = Guid.NewGuid(),
                VehicleId = entity.VehicleId,
                ScheduleId = entity.Id,
                Kind = entity.Kind,
                Description = entity.Description,
                Date = date,
                Amount = entity.Amount,
                Remarks = entity.Remarks,
            });
        }

        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteScheduleAsync(Guid scheduleId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var entity = await db.VehicleExpenseSchedules.Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted);
        if (entity is null) return;

        entity.IsDeleted = true;
        foreach (var instalment in entity.Entries.Where(x => !x.IsDeleted))
            instalment.IsDeleted = true;

        await db.SaveChangesAsync();
    }

    /// <summary>A one-off cost recorded directly, with no schedule behind it —
    /// the common case of "I paid this today".</summary>
    public async Task<Guid> SaveExpenseAsync(VehicleExpense expense)
    {
        if (expense.Amount <= 0)
            throw new InvalidOperationException("Enter an amount greater than zero.");

        await using var db = await factory.CreateDbContextAsync();

        _ = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == expense.VehicleId && !v.IsDeleted)
            ?? throw new InvalidOperationException("Vehicle not found.");

        var entity = expense.Id == Guid.Empty
            ? null
            : await db.VehicleExpenses.FirstOrDefaultAsync(x => x.Id == expense.Id && !x.IsDeleted);

        var isNew = entity is null;
        entity ??= new VehicleExpense { Id = Guid.NewGuid(), VehicleId = expense.VehicleId };

        entity.Kind = expense.Kind;
        entity.Description = string.IsNullOrWhiteSpace(expense.Description) ? null : expense.Description.Trim();
        entity.Date = expense.Date.Date;
        entity.Amount = expense.Amount;
        entity.Remarks = string.IsNullOrWhiteSpace(expense.Remarks) ? null : expense.Remarks.Trim();

        if (isNew) db.VehicleExpenses.Add(entity);

        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task DeleteExpenseAsync(Guid expenseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.VehicleExpenses.FirstOrDefaultAsync(x => x.Id == expenseId && !x.IsDeleted);
        if (entity is null) return;

        entity.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    /// <summary>The dates a schedule falls due on. A one-off is a schedule of
    /// exactly one, so both shapes come out of the same place.
    ///
    /// Stepping by months from the start date (rather than adding a fixed
    /// number of days) is what keeps "the 15th" landing on the 15th every
    /// time; .NET clamps a day that a shorter month doesn't have, so a
    /// schedule starting on the 31st falls on the 28th in February and back
    /// on the 31st in March rather than drifting earlier for good.</summary>
    public static IEnumerable<DateTime> InstalmentDates(VehicleExpenseSchedule schedule)
    {
        var start = schedule.StartDate.Date;

        if (schedule.Recurrence == ExpenseRecurrence.None || schedule.EndDate is null)
        {
            yield return start;
            yield break;
        }

        var end = schedule.EndDate.Value.Date;
        if (end < start) yield break;

        var step = schedule.Recurrence switch
        {
            ExpenseRecurrence.Monthly => 1,
            ExpenseRecurrence.Quarterly => 3,
            ExpenseRecurrence.Yearly => 12,
            _ => 0,
        };

        if (step == 0)
        {
            yield return start;
            yield break;
        }

        for (var i = 0; ; i++)
        {
            var date = start.AddMonths(step * i);
            if (date > end) yield break;

            yield return date;

            // Belt and braces against a schedule that somehow can't advance —
            // an infinite loop here would write rows until the disk filled.
            if (i > MaxInstalments) yield break;
        }
    }
}
