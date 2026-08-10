namespace TransTrack.Core;

/// <summary>The headline numbers on the Dashboard — this calendar month,
/// plus the two things that need attention regardless of month: pending
/// approvals and compliance dates running out.</summary>
public record DashboardSummary(
    int TripsThisMonth,
    decimal RevenueThisMonth,
    decimal ExpensesThisMonth,
    int PendingApprovals,
    decimal OutstandingBalance,
    int VehiclesExpiringSoon);

/// <summary>One month's revenue and expenses, for the trend chart.</summary>
public record MonthlyFigure(string Label, decimal Revenue, decimal Expenses);

/// <summary>One expense category's total, for the breakdown chart.</summary>
public record CategoryFigure(string Category, decimal Amount);

/// <summary>A vehicle with a compliance date already expired or expiring
/// within the alert window.</summary>
public record ComplianceAlert(string VehicleRegNo, string DocumentName, DateTime Upto, bool IsExpired);

/// <summary>One row of the combined Transactions report — either a trip
/// expense or an amount received, merged into one dated list so a trip's
/// whole cash flow (both directions) can be filtered and reviewed together.
/// <see cref="CountsInCompanyAccounts"/> is false for an other-owner
/// vehicle's expense/income rows — shown when asked for, but never summed
/// into the company's own totals.</summary>
public record LedgerRow(
    DateTime Date,
    string TripNo,
    string VehicleRegNo,
    string DriverName,
    string Kind,
    string Detail,
    decimal Amount,
    bool CountsInCompanyAccounts);
