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

        // Both child collections are filtered, and deliberately so: expenses
        // and amounts are soft-deleted, and this report's figures come from
        // Trip.TotalExpenses / TotalApprovedReceived / BalanceReceivable,
        // which sum whatever rows the query loaded. Loading them unfiltered
        // made the report — and its PDF and Excel exports — count money the
        // user had already removed from the trip.
        //
        // AsSplitQuery because there are two collection includes: in a single
        // query SQLite has to return one row per expense *per* amount, so a
        // trip with 10 of each fetches 100 rows to build 20. Split issues one
        // query per collection instead, which is the cheaper shape here.
        var query = db.Trips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Party)
            .Include(t => t.FromCity)
            .Include(t => t.ToCity)
            .Include(t => t.Expenses.Where(e => !e.IsDeleted))
            .Include(t => t.Transactions.Where(x => !x.IsDeleted))
            .AsSplitQuery()
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

    /// <summary>Every trip billed to one party over a period, oldest first —
    /// the running statement a party is sent at month end. Ordered ascending
    /// (unlike the other reports) because the serial numbers are only
    /// meaningful counting forwards through the month.</summary>
    public async Task<PartyReport> GetPartyReportAsync(Guid partyId, DateTime? from, DateTime? to)
    {
        await using var db = await factory.CreateDbContextAsync();

        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partyId)
                    ?? throw new InvalidOperationException("Party not found.");

        var query = db.Trips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.FromCity)
            .Include(t => t.ToCity)
            .Where(t => !t.IsDeleted && t.PartyId == partyId);

        if (from is { } f) query = query.Where(t => t.Date >= f.Date);
        if (to is { } t2) query = query.Where(t => t.Date <= t2.Date);

        var trips = await query.OrderBy(t => t.Date).ThenBy(t => t.TripNo).ToListAsync();

        var rows = trips.Select((t, i) => new PartyTripRow(
            i + 1, t.Date, t.Vehicle.RegNo, t.FromCity.Name, t.ToCity.Name,
            t.Weight, t.Rate, t.Amount)).ToList();

        return new PartyReport(party.Name, DescribePeriod(from, to), rows);
    }

    /// <summary>The period as it appears in the report title. A range landing
    /// inside a single calendar month prints as "JULY-2026", matching the
    /// customer's existing paper report; anything else spells out both ends
    /// rather than pretending to be one month.</summary>
    public static string DescribePeriod(DateTime? from, DateTime? to)
    {
        if (from is { } f && to is { } t)
        {
            return f.Year == t.Year && f.Month == t.Month
                ? f.ToString("MMMM-yyyy").ToUpperInvariant()
                : $"{f:dd-MMM-yyyy} to {t:dd-MMM-yyyy}";
        }

        if (from is { } onlyFrom) return $"From {onlyFrom:dd-MMM-yyyy}";
        if (to is { } onlyTo) return $"To {onlyTo:dd-MMM-yyyy}";
        return "ALL DATES";
    }

    /// <summary>Per vehicle, per calendar month: what the company earned,
    /// what it spent, and what it kept. Maintenance is counted alongside trip
    /// expenses because a vehicle's real cost for a month includes what it
    /// took to keep it on the road, not just what its trips burned in fuel.</summary>
    public async Task<List<VehicleMonthlySaving>> GetVehicleSavingsAsync(Guid? vehicleId, DateTime? from, DateTime? to)
    {
        await using var db = await factory.CreateDbContextAsync();

        var tripQuery = db.Trips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Expenses.Where(e => !e.IsDeleted))
            .Where(t => !t.IsDeleted);

        var maintenanceQuery = db.VehicleMaintenances.AsNoTracking()
            .Include(m => m.Vehicle)
            .Where(m => !m.IsDeleted);

        if (vehicleId is { } v)
        {
            tripQuery = tripQuery.Where(t => t.VehicleId == v);
            maintenanceQuery = maintenanceQuery.Where(m => m.VehicleId == v);
        }
        if (from is { } f)
        {
            tripQuery = tripQuery.Where(t => t.Date >= f.Date);
            maintenanceQuery = maintenanceQuery.Where(m => m.Date >= f.Date);
        }
        if (to is { } t2)
        {
            tripQuery = tripQuery.Where(t => t.Date <= t2.Date);
            maintenanceQuery = maintenanceQuery.Where(m => m.Date <= t2.Date);
        }

        var trips = await tripQuery.ToListAsync();
        var maintenance = await maintenanceQuery.ToListAsync();

        // Keyed on (vehicle, year, month) so a vehicle with no trips in a
        // month it was nonetheless serviced in still shows up — that month's
        // maintenance is a real cost and hiding it would overstate savings.
        var buckets = new Dictionary<(string Vehicle, int Year, int Month), (int Trips, decimal Revenue, decimal Expenses, decimal Maintenance)>();

        foreach (var t in trips)
        {
            var key = (t.Vehicle.RegNo, t.Date.Year, t.Date.Month);
            var current = buckets.GetValueOrDefault(key);
            buckets[key] = (current.Trips + 1,
                current.Revenue + t.CompanyRevenue,
                current.Expenses + t.CompanyExpenses,
                current.Maintenance);
        }

        foreach (var m in maintenance)
        {
            var key = (m.Vehicle.RegNo, m.Date.Year, m.Date.Month);
            var current = buckets.GetValueOrDefault(key);
            buckets[key] = (current.Trips, current.Revenue, current.Expenses, current.Maintenance + m.Amount);
        }

        return buckets
            .OrderBy(b => b.Key.Vehicle).ThenByDescending(b => b.Key.Year).ThenByDescending(b => b.Key.Month)
            .Select(b => new VehicleMonthlySaving(
                b.Key.Vehicle,
                new DateTime(b.Key.Year, b.Key.Month, 1).ToString("MMM yyyy"),
                b.Value.Trips, b.Value.Revenue, b.Value.Expenses, b.Value.Maintenance))
            .ToList();
    }
}
