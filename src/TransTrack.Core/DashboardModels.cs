namespace TransTrack.Core;

/// <summary>The headline numbers on the Dashboard for one chosen month, plus
/// the three things that mean nothing month-by-month and are therefore always
/// as-of-now: pending approvals, compliance dates running out, and what is
/// still owed.
///
/// <see cref="OutstandingBalance"/> is deliberately NOT filtered by the
/// selected month. It is every unclosed trip since the company started — the
/// question it answers is "how much is still out there", and an answer that
/// only counted one month would be worse than useless, because it would look
/// like a real total while quietly hiding older debt.</summary>
public record DashboardSummary(
    int Trips,
    decimal TripEarnings,
    decimal TripExpenses,
    decimal MaintenanceCost,
    decimal ExpectedSalaries,
    decimal RecurringExpenses,
    int PendingApprovals,
    decimal OutstandingBalance,
    int VehiclesExpiringSoon)
{
    /// <summary>What the month actually made: earnings less every cost that
    /// belongs to it. Expected salaries count whether or not they have been
    /// paid yet — the wage bill is owed for the month regardless.</summary>
    public decimal NetProfit =>
        TripEarnings - TripExpenses - MaintenanceCost - ExpectedSalaries - RecurringExpenses;
}

/// <summary>One selectable month on the dashboard, with the label already
/// formatted — the client shouldn't have to know how a month is spelled.</summary>
public record MonthOption(int Year, int Month, string Label);

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
    decimal Amount,
    string? LrNo = null,
    decimal WaymentCharge = 0,
    decimal LoadingCharge = 0,
    decimal UnloadingCharge = 0,
    decimal GstAmount = 0,
    decimal? GstPercentage = null,
    decimal AdvanceReceived = 0)
{
    public decimal TotalExtras => WaymentCharge + LoadingCharge + UnloadingCharge;

    /// <summary>Freight plus extras, before tax.</summary>
    public decimal TotalBeforeTax => Amount + TotalExtras;

    /// <summary>What this trip contributes to the party's bill.</summary>
    public decimal GrandTotal => TotalBeforeTax + GstAmount;

    /// <summary>What is still owed on this trip once the advance already taken
    /// against it is knocked off. Measured against the pre-tax total because
    /// that is what the TOTAL column beside it shows — the tax is charged once
    /// on the whole bill, beneath the table.</summary>
    public decimal BalanceDue => TotalBeforeTax - AdvanceReceived;
}

/// <summary>The party-wise report: the party's name and the period it covers
/// (both printed in the title), its rows, and the one total that matters.</summary>
public record PartyReport(
    string PartyName,
    string PeriodLabel,
    IReadOnlyList<PartyTripRow> Rows)
{
    /// <summary>Freight only — kept as it was so the existing report's total
    /// column still means what it always did.</summary>
    public decimal Total => Rows.Sum(r => r.Amount);

    public decimal TotalWayment => Rows.Sum(r => r.WaymentCharge);
    public decimal TotalLoading => Rows.Sum(r => r.LoadingCharge);
    public decimal TotalUnloading => Rows.Sum(r => r.UnloadingCharge);
    public decimal TotalExtras => Rows.Sum(r => r.TotalExtras);
    public decimal TotalBeforeTax => Rows.Sum(r => r.TotalBeforeTax);
    public decimal TotalGst => Rows.Sum(r => r.GstAmount);

    /// <summary>What the party is actually billed for the period.</summary>
    public decimal GrandTotal => Rows.Sum(r => r.GrandTotal);

    /// <summary>Advances already taken against these trips.</summary>
    public decimal TotalAdvance => Rows.Sum(r => r.AdvanceReceived);

    /// <summary>
    /// What the party is being asked to pay: the whole bill, tax included, less
    /// what they have already advanced.
    ///
    /// Deducted after the tax rather than before it, because GST is charged on
    /// the value of the freight, not on the portion left unpaid — taking the
    /// advance off first would under-charge the tax.
    /// </summary>
    public decimal BalancePayable => GrandTotal - TotalAdvance;

    /// <summary>Whether anything was advanced in this period. The advance and
    /// balance columns are hidden when nothing was, so a party that always
    /// pays on receipt gets a plain bill rather than a column of zeroes.</summary>
    public bool HasAdvance => TotalAdvance != 0;

    /// <summary>Whether any trip in the period carried an extra or tax — the
    /// bill hides those columns entirely when none did, rather than printing
    /// a block of zeroes.</summary>
    public bool HasExtras => TotalExtras > 0;
    public bool HasGst => TotalGst > 0;

    // Each addition gets its own column on the bill, and only when something
    // in the period actually used it — a party that never pays loading should
    // not be handed a column of dashes.
    public bool HasWayment => TotalWayment > 0;
    public bool HasLoading => TotalLoading > 0;
    public bool HasUnloading => TotalUnloading > 0;

    /// <summary>The single GST rate this bill was charged at, or null when the
    /// period spans trips booked at different rates.
    ///
    /// Tax is charged on the bill's total rather than shown against each trip,
    /// so the footer needs one rate to name. Rates are snapshotted per trip and
    /// can genuinely differ across a month if the party's rate changed mid-way,
    /// and in that case naming any one of them would be a lie — the line says
    /// plain "GST" instead.</summary>
    public decimal? GstRate
    {
        get
        {
            var rates = Rows.Where(r => r.GstAmount > 0 && r.GstPercentage is > 0)
                            .Select(r => r.GstPercentage!.Value)
                            .Distinct()
                            .ToList();
            return rates.Count == 1 ? rates[0] : null;
        }
    }

    /// <summary>"GST @ 5%" when the whole bill shares a rate, otherwise "GST".</summary>
    public string GstLabel => GstRate is { } rate ? $"GST @ {rate:0.##}%" : "GST";
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
