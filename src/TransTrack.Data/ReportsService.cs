using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>Read-only, filterable queries behind the Reports screen. Each
/// method takes the same three filters (vehicle, driver, date range) even
/// where a report has no driver of its own (maintenance), so the screen's
/// filter panel behaves identically across tabs — an unused filter is
/// simply ignored by that query.</summary>
public class ReportsService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Trip>> GetTripsAsync(Guid? vehicleId, Guid? driverId, DateTime? from, DateTime? to, VehicleOwnership? ownership = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var query = db.Trips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Party)
            .Include(t => t.FromCity)
            .Include(t => t.ToCity)
            .Include(t => t.Expenses)
            .Include(t => t.Transactions)
            .Where(t => !t.IsDeleted);

        if (vehicleId is { } v) query = query.Where(t => t.VehicleId == v);
        if (driverId is { } d) query = query.Where(t => t.DriverId == d);
        if (from is { } f) query = query.Where(t => t.Date >= f.Date);
        if (to is { } t2) query = query.Where(t => t.Date <= t2.Date);
        if (ownership is { } o) query = query.Where(t => t.Vehicle.Ownership == o);

        return await query.OrderByDescending(t => t.Date).ToListAsync();
    }

    public async Task<List<VehicleMaintenance>> GetMaintenanceAsync(Guid? vehicleId, DateTime? from, DateTime? to)
    {
        await using var db = await factory.CreateDbContextAsync();

        var query = db.VehicleMaintenances.AsNoTracking()
            .Include(m => m.Vehicle)
            .Include(m => m.MaintenanceCategory)
            .Where(m => !m.IsDeleted);

        if (vehicleId is { } v) query = query.Where(m => m.VehicleId == v);
        if (from is { } f) query = query.Where(m => m.Date >= f.Date);
        if (to is { } t) query = query.Where(m => m.Date <= t.Date);

        return await query.OrderByDescending(m => m.Date).ToListAsync();
    }

    /// <summary>Every trip expense and every amount received, merged into
    /// one dated ledger — the report for "how much came in, how much went
    /// out". Own-fleet rows count in the company's accounts; another
    /// owner's rows are shown for visibility but flagged as not counting,
    /// per <see cref="LedgerRow.CountsInCompanyAccounts"/>.</summary>
    public async Task<List<LedgerRow>> GetLedgerAsync(Guid? vehicleId, Guid? driverId, DateTime? from, DateTime? to, VehicleOwnership? ownership = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var expenseQuery = db.TripExpenses.AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Include(e => e.Trip).ThenInclude(t => t.Vehicle)
            .Include(e => e.Trip).ThenInclude(t => t.Driver)
            .Where(e => !e.IsDeleted);

        var incomeQuery = db.TripTransactions.AsNoTracking()
            .Include(t => t.Trip).ThenInclude(t => t.Vehicle)
            .Include(t => t.Trip).ThenInclude(t => t.Driver)
            .Where(t => !t.IsDeleted);

        if (vehicleId is { } v)
        {
            expenseQuery = expenseQuery.Where(e => e.Trip.VehicleId == v);
            incomeQuery = incomeQuery.Where(t => t.Trip.VehicleId == v);
        }
        if (driverId is { } d)
        {
            expenseQuery = expenseQuery.Where(e => e.Trip.DriverId == d);
            incomeQuery = incomeQuery.Where(t => t.Trip.DriverId == d);
        }
        if (from is { } f)
        {
            expenseQuery = expenseQuery.Where(e => e.Date >= f.Date);
            incomeQuery = incomeQuery.Where(t => t.Date >= f.Date);
        }
        if (to is { } t2)
        {
            expenseQuery = expenseQuery.Where(e => e.Date <= t2.Date);
            incomeQuery = incomeQuery.Where(t => t.Date <= t2.Date);
        }
        if (ownership is { } o)
        {
            expenseQuery = expenseQuery.Where(e => e.Trip.Vehicle.Ownership == o);
            incomeQuery = incomeQuery.Where(t => t.Trip.Vehicle.Ownership == o);
        }

        var expenses = await expenseQuery.ToListAsync();
        var income = await incomeQuery.ToListAsync();

        var rows = new List<LedgerRow>(expenses.Count + income.Count);

        rows.AddRange(expenses.Select(e => new LedgerRow(
            e.Date, e.Trip.TripNo, e.Trip.Vehicle.RegNo, e.Trip.Driver.Name,
            "Expense", e.ExpenseCategory.Name, e.Amount,
            e.Trip.Vehicle.Ownership != VehicleOwnership.Other)));

        rows.AddRange(income.Select(t => new LedgerRow(
            t.Date, t.Trip.TripNo, t.Trip.Vehicle.RegNo, t.Trip.Driver.Name,
            "Income", $"{t.PaymentMode} — {t.ApprovalStatus}", t.Amount,
            t.Trip.Vehicle.Ownership != VehicleOwnership.Other)));

        return rows.OrderByDescending(r => r.Date).ToList();
    }
}
