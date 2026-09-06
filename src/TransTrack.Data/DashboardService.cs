using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class DashboardService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>The dashboard's figures for one month. Defaults to the current
    /// month when no year/month is given, so an unparameterised call behaves
    /// exactly as it did before the month picker existed.</summary>
    public async Task<DashboardSummary> GetSummaryAsync(int? year = null, int? month = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var monthStart = StartOfMonth(year, month);
        var monthEnd = monthStart.AddMonths(1);

        var trips = await db.Trips.Include(t => t.Vehicle)
            .Where(t => !t.IsDeleted && t.Date >= monthStart && t.Date < monthEnd)
            .ToListAsync();

        // Only the owned fleet's freight counts as company revenue — an
        // other-owner vehicle's trip only contributes its commission. Excludes
        // GST, which is collected for the government rather than earned.
        var tripEarnings = trips.Sum(t => t.CompanyRevenue);

        // Same rule for expenses: another owner's running costs are that
        // owner's money, not the company's, so they never reach this total.
        var tripExpenses = await db.TripExpenses
            .Include(e => e.Trip).ThenInclude(t => t.Vehicle)
            .Where(e => !e.IsDeleted && e.Date >= monthStart && e.Date < monthEnd
                        && e.Trip.Vehicle.Ownership != VehicleOwnership.Other)
            .SumAsync(e => e.Amount);

        var maintenanceCost = await db.VehicleMaintenances
            .Where(m => !m.IsDeleted && m.Date >= monthStart && m.Date < monthEnd)
            .SumAsync(m => m.Amount);

        // The paperwork instalments (insurance, road tax, ...) falling due in
        // this month. Generated rows, so a month with nothing due is simply
        // zero rather than a share of an annual premium.
        var recurringExpenses = await db.VehicleExpenses
            .Where(x => !x.IsDeleted && x.Date >= monthStart && x.Date < monthEnd)
            .SumAsync(x => x.Amount);

        // Deliberately a calculation, not a lookup: what the month's wage bill
        // *is*, from the drivers currently on the books and their monthly
        // salary — never what has actually been paid out. Reading the driver
        // ledger here would answer a different question (cash paid, including
        // advances against other months) and would make the profit line move
        // every time someone settled up.
        var expectedSalaries = await db.Drivers
            .Where(d => !d.IsDeleted && d.IsActive)
            .SumAsync(d => d.Salary);

        var pendingApprovals = await db.TripTransactions
            .Where(t => !t.IsDeleted && t.ApprovalStatus == ApprovalStatus.Pending)
            .CountAsync();

        // Never month-filtered, on purpose. This is every unclosed trip since
        // the company started — "how much is still out there" — and scoping it
        // to the selected month would look like a real total while hiding
        // every older debt.
        var openTrips = await db.Trips
            .Include(t => t.Transactions)
            .Where(t => !t.IsDeleted && t.Status != TripStatus.Closed)
            .ToListAsync();
        var outstandingBalance = openTrips.Sum(t => t.BalanceReceivable);

        var expiringSoon = DateTime.Today.AddDays(30);
        var vehiclesExpiringSoon = await db.Vehicles
            .Where(v => !v.IsDeleted && v.IsActive && (
                (v.InsuranceUpto != null && v.InsuranceUpto <= expiringSoon) ||
                (v.FitnessUpto != null && v.FitnessUpto <= expiringSoon) ||
                (v.PollutionUpto != null && v.PollutionUpto <= expiringSoon) ||
                (v.PermitUpto != null && v.PermitUpto <= expiringSoon) ||
                (v.NationalPermitUpto != null && v.NationalPermitUpto <= expiringSoon)))
            .CountAsync();

        return new DashboardSummary(
            trips.Count, tripEarnings, tripExpenses, maintenanceCost, expectedSalaries,
            recurringExpenses, pendingApprovals, outstandingBalance, vehiclesExpiringSoon);
    }

    /// <summary>The months the picker offers, newest first — at least the last
    /// six, extended back to the company's first trip when there is more
    /// history than that, so an older month is still reachable.</summary>
    public async Task<List<MonthOption>> GetSelectableMonthsAsync(int minimumMonths = 6)
    {
        await using var db = await factory.CreateDbContextAsync();

        var earliestTrip = await db.Trips.Where(t => !t.IsDeleted)
            .OrderBy(t => t.Date).Select(t => (DateTime?)t.Date).FirstOrDefaultAsync();

        var thisMonth = StartOfMonth(null, null);
        var oldest = earliestTrip is { } first
            ? new DateTime(first.Year, first.Month, 1)
            : thisMonth;

        // Always at least the minimum, however little history there is.
        var floor = thisMonth.AddMonths(-(Math.Max(1, minimumMonths) - 1));
        if (oldest > floor) oldest = floor;

        var months = new List<MonthOption>();
        for (var cursor = thisMonth; cursor >= oldest; cursor = cursor.AddMonths(-1))
            months.Add(new MonthOption(cursor.Year, cursor.Month, cursor.ToString("MMMM yyyy")));

        return months;
    }

    private static DateTime StartOfMonth(int? year, int? month)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        // A nonsensical month from a hand-edited URL falls back to today's
        // rather than throwing — the dashboard should still draw.
        if (m is < 1 or > 12 || y < 1900 || y > 9999) return new DateTime(today.Year, today.Month, 1);

        return new DateTime(y, m, 1);
    }

    /// <summary>Revenue and expenses for the last <paramref name="months"/>
    /// calendar months, oldest first, including months with no trips at all.</summary>
    public async Task<List<MonthlyFigure>> GetMonthlyFiguresAsync(int months = 6)
    {
        await using var db = await factory.CreateDbContextAsync();

        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));

        var trips = await db.Trips.AsNoTracking().Include(t => t.Vehicle)
            .Where(t => !t.IsDeleted && t.Date >= start).ToListAsync();
        var expenses = await db.TripExpenses.AsNoTracking().Include(e => e.Trip).ThenInclude(t => t.Vehicle)
            .Where(e => !e.IsDeleted && e.Date >= start && e.Trip.Vehicle.Ownership != VehicleOwnership.Other)
            .ToListAsync();

        var figures = new List<MonthlyFigure>();
        for (var i = 0; i < months; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);

            var revenue = trips.Where(t => t.Date >= monthStart && t.Date < monthEnd).Sum(t => t.CompanyRevenue);
            var expense = expenses.Where(e => e.Date >= monthStart && e.Date < monthEnd).Sum(e => e.Amount);

            figures.Add(new MonthlyFigure(monthStart.ToString("MMM yy"), revenue, expense));
        }

        return figures;
    }

    /// <summary>Expense totals by category over the last <paramref name="months"/> months.</summary>
    public async Task<List<CategoryFigure>> GetExpenseByCategoryAsync(int months = 6)
    {
        await using var db = await factory.CreateDbContextAsync();

        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));

        // Grouped and summed in SQL, but ordered after materialising: EF
        // Core cannot translate ORDER BY against a property of the record
        // just constructed in the projection above, and the row count here
        // (one per category) is far too small for that to matter.
        var totals = await db.TripExpenses.AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Include(e => e.Trip).ThenInclude(t => t.Vehicle)
            .Where(e => !e.IsDeleted && e.Date >= start && e.Trip.Vehicle.Ownership != VehicleOwnership.Other)
            .GroupBy(e => e.ExpenseCategory.Name)
            .Select(g => new CategoryFigure(g.Key, g.Sum(e => e.Amount)))
            .ToListAsync();

        return totals.OrderByDescending(f => f.Amount).ToList();
    }

    public async Task<List<ComplianceAlert>> GetComplianceAlertsAsync(int withinDays = 30)
    {
        await using var db = await factory.CreateDbContextAsync();

        var threshold = DateTime.Today.AddDays(withinDays);
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(v => !v.IsDeleted && v.IsActive)
            .ToListAsync();

        var alerts = new List<ComplianceAlert>();
        foreach (var v in vehicles)
        {
            AddIfDue(alerts, v.RegNo, "Insurance", v.InsuranceUpto, threshold);
            AddIfDue(alerts, v.RegNo, "Fitness", v.FitnessUpto, threshold);
            AddIfDue(alerts, v.RegNo, "Pollution", v.PollutionUpto, threshold);
            AddIfDue(alerts, v.RegNo, "Permit", v.PermitUpto, threshold);
            AddIfDue(alerts, v.RegNo, "National permit", v.NationalPermitUpto, threshold);
        }

        return alerts.OrderBy(a => a.Upto).ToList();
    }

    private static void AddIfDue(List<ComplianceAlert> alerts, string regNo, string document, DateTime? upto, DateTime threshold)
    {
        if (upto is null || upto > threshold) return;
        alerts.Add(new ComplianceAlert(regNo, document, upto.Value, upto.Value.Date < DateTime.Today));
    }
}
