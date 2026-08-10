using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

public class DashboardService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<DashboardSummary> GetSummaryAsync()
    {
        await using var db = await factory.CreateDbContextAsync();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var tripsThisMonth = await db.Trips.Include(t => t.Vehicle)
            .Where(t => !t.IsDeleted && t.Date >= monthStart && t.Date < monthEnd)
            .ToListAsync();

        // Only the owned fleet's freight counts as company revenue — an
        // other-owner vehicle's trip only contributes its commission.
        var revenueThisMonth = tripsThisMonth.Sum(t => t.CompanyRevenue);

        // Same rule for expenses: another owner's running costs are that
        // owner's money, not the company's, so they never reach this total.
        var expensesThisMonth = await db.TripExpenses
            .Include(e => e.Trip).ThenInclude(t => t.Vehicle)
            .Where(e => !e.IsDeleted && e.Date >= monthStart && e.Date < monthEnd
                        && e.Trip.Vehicle.Ownership != VehicleOwnership.Other)
            .SumAsync(e => e.Amount);

        var pendingApprovals = await db.TripTransactions
            .Where(t => !t.IsDeleted && t.ApprovalStatus == ApprovalStatus.Pending)
            .CountAsync();

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

        return new DashboardSummary(tripsThisMonth.Count, revenueThisMonth, expensesThisMonth,
            pendingApprovals, outstandingBalance, vehiclesExpiringSoon);
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
