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
/// into the company's own totals. <see cref="ReceiptType"/> is null for an
/// Expense row — the Advance/Payment split only applies to money coming in.</summary>
public record LedgerRow(
    DateTime Date,
    string TripNo,
    string VehicleRegNo,
    string DriverName,
    string Kind,
    string Detail,
    decimal Amount,
    bool CountsInCompanyAccounts,
    ReceiptType? ReceiptType = null);

/// <summary>One line of the party-wise report — the freight billed to a party
/// for a single trip, in the shape the customer's existing paper report uses:
/// date, vehicle, route, weight, rate, amount. Weight and rate are nullable
/// because plenty of real trips are billed as a flat amount with neither.</summary>
public record PartyTripRow(
    int SerialNo,
    DateTime Date,
    string VehicleRegNo,
    string FromCity,
    string ToCity,
    decimal? Weight,
    decimal? Rate,
    decimal Amount);

/// <summary>The party-wise report: the party's name and the period it covers
/// (both printed in the title), its rows, and the one total that matters.</summary>
public record PartyReport(
    string PartyName,
    string PeriodLabel,
    IReadOnlyList<PartyTripRow> Rows)
{
    public decimal Total => Rows.Sum(r => r.Amount);
}

/// <summary>One vehicle's figures for one calendar month: what its trips
/// earned the company, what they cost it, and what was left. Revenue and
/// expenses both use the company-accounts view — an other-owner vehicle
/// contributes its commission, not the freight it collected on someone
/// else's behalf — so Saving is genuinely what the company kept.</summary>
public record VehicleMonthlySaving(
    string VehicleRegNo,
    string MonthLabel,
    int Trips,
    decimal Revenue,
    decimal TripExpenses,
    decimal MaintenanceCost)
{
    public decimal Saving => Revenue - TripExpenses - MaintenanceCost;

    /// <summary>Average kept per trip — the "savings per trip" half of the
    /// report. Zero trips means zero rather than a divide-by-zero.</summary>
    public decimal SavingPerTrip => Trips == 0 ? 0m : Saving / Trips;
}
