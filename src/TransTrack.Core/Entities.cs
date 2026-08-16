namespace TransTrack.Core;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Who created this row, when login is on. Null for everything
    /// written before login existed, and null forever after too if login is
    /// never switched on — that is a normal, permanent state, not a gap to
    /// fill in.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Who last saved this row — which, since this app deletes by
    /// setting <see cref="IsDeleted"/> rather than removing the row, doubles
    /// as "who deleted it" for anything not otherwise edited in between.</summary>
    public Guid? UpdatedByUserId { get; set; }
}

// ── Multi-tenancy ────────────────────────────────────────────────────────

/// <summary>Implemented by every row that belongs to exactly one onboarded
/// company — everything except <see cref="Company"/> itself (the tenant
/// root) and <see cref="User"/>'s EnterpriseAdmin identity (which is never a
/// row at all, see <c>AuthService.EnterpriseAdminUsername</c>). AppDbContext
/// applies a global query filter to every entity implementing this, keyed
/// off the signed-in user's own CompanyId — so a forgotten "scope this by
/// company" in some service method fails safe (returns nothing) rather than
/// leaking another company's data.</summary>
public interface ITenantEntity
{
    Guid CompanyId { get; set; }
}

// ── Masters ──────────────────────────────────────────────────────────────

/// <summary>Implemented by the plain "just a Name" masters — State,
/// ExpenseCategory, MaintenanceCategory — so the app layer can share one
/// list-plus-form view model across all three instead of writing the same
/// plumbing three times.</summary>
public interface INamedEntity
{
    string Name { get; set; }
}

public class State : BaseEntity, INamedEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<City> Cities { get; set; } = [];
}

public class City : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid StateId { get; set; }
    public State State { get; set; } = null!;

    public string Display => State is null ? Name : $"{Name}, {State.Name}";
}

/// <summary>An owner of a vehicle the company operates but does not own —
/// the company earns commission rather than the full freight on these
/// vehicles' trips. The company's own fleet needs no row here.</summary>
public class Owner : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankAccountNo { get; set; }
    public string? Ifsc { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>The billing party a trip is invoiced to — the "PARTY" column on
/// a trip and the "M/s" line on the Cash Bill.</summary>
public class Party : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
}

public class Driver : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime? JoiningDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string Display => $"{EmployeeCode} — {Name}";
}

public class Vehicle : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string RegNo { get; set; } = string.Empty;
    public VehicleOwnership Ownership { get; set; } = VehicleOwnership.Own;

    /// <summary>Required when <see cref="Ownership"/> is Other; null for the
    /// company's own fleet.</summary>
    public Guid? OwnerId { get; set; }
    public Owner? Owner { get; set; }

    public string? VehicleType { get; set; }
    public decimal? Capacity { get; set; }

    // Compliance dates rather than a bare yes/no — "up to date" is derived
    // (date >= today) instead of needing separate manual upkeep, and a
    // report can flag what's expiring or already expired.
    public DateTime? PermitUpto { get; set; }
    public DateTime? NationalPermitUpto { get; set; }
    public DateTime? InsuranceUpto { get; set; }
    public DateTime? FitnessUpto { get; set; }
    public DateTime? PollutionUpto { get; set; }

    public bool IsActive { get; set; } = true;

    public string Display => Ownership == VehicleOwnership.Own
        ? $"{RegNo} (Own)"
        : $"{RegNo} ({Owner?.Name ?? "Other"})";

    public static bool IsUpToDate(DateTime? upto) => upto.HasValue && upto.Value.Date >= DateTime.Today;
    public static bool IsExpiringSoon(DateTime? upto, int withinDays = 30) =>
        upto.HasValue && upto.Value.Date >= DateTime.Today && upto.Value.Date <= DateTime.Today.AddDays(withinDays);
}

/// <summary>The single document held against a vehicle (RC book, permit scan,
/// insurance copy — whatever the company keeps). Deliberately its own table
/// rather than columns on <see cref="Vehicle"/>: it is written once in a while
/// from Settings and read only when someone asks for it, so keeping it out of
/// the vehicle row means no list, trip or dashboard query can ever drag a
/// multi-megabyte file along with it.
///
/// Only the *reference* lives here — the bytes sit on disk (see
/// VehicleDocumentStorage). <see cref="StoredPath"/> is deliberately a plain
/// string rather than anything filesystem-specific, so moving to cloud object
/// storage later means swapping the storage implementation and putting an
/// object key in this same column, with no schema change.</summary>
public class VehicleDocument : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>The name the user's file had when they uploaded it — what the
    /// download is named, so it arrives recognisable.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    /// <summary>Where the bytes are: a path today, an object key after the
    /// move to cloud storage.</summary>
    public string StoredPath { get; set; } = string.Empty;
}

public class ExpenseCategory : BaseEntity, INamedEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class MaintenanceCategory : BaseEntity, INamedEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// The tenant root — one row per onboarded company, created only by
/// EnterpriseAdmin via the onboarding flow, never by a company's own users.
/// Doubles as the letterhead used on every printed LR and Bill (the fields
/// carried over from the old single-tenant CompanySettings) and as the
/// license record that gates sign-in. Every other tenant-scoped table
/// (see <see cref="ITenantEntity"/>) points back at this by Id.
/// </summary>
public class Company : BaseEntity
{
    // ── Onboarding / license ─────────────────────────────────────────────

    /// <summary>The person EnterpriseAdmin onboarded — not necessarily the
    /// Owner user's login name, just who to call.</summary>
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;

    public DateTime LicenseStartsOn { get; set; } = DateTime.UtcNow;

    /// <summary>Defaults to one year from onboarding; EnterpriseAdmin can
    /// choose a different length at onboarding time and extend it later via
    /// the renew-license endpoint.</summary>
    public DateTime LicenseExpiresOn { get; set; } = DateTime.UtcNow.AddYears(1);

    /// <summary>EnterpriseAdmin's kill switch, independent of the license
    /// date — suspends sign-in immediately without needing to backdate an
    /// expiry.</summary>
    public bool IsActive { get; set; } = true;

    public bool IsLicenseValid => IsActive && DateTime.UtcNow <= LicenseExpiresOn;

    // ── Branding / letterhead (printed on the Bill, LR and reports) ─────

    public string CompanyName { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? AddressLine { get; set; }
    public string? Phone { get; set; }
    public string? Cell { get; set; }
    public string? Pan { get; set; }
    public string? Gstin { get; set; }
    public string? JurisdictionNote { get; set; }

    /// <summary>The uploaded logo, stored inline as base64 — small enough
    /// (a letterhead mark, not a photo) that a separate file or table is
    /// more ceremony than the data warrants. Printed on every generated
    /// document: the Bill, the LR, and report exports.</summary>
    public string? LogoBase64 { get; set; }
    public string? LogoFileName { get; set; }

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoBase64);

    // ── Bank details (optionally printed on the Bill) ────────────────────

    public string? BankAccountNo { get; set; }
    public string? Ifsc { get; set; }

    /// <summary>Whether to print the bank details on the Cash Bill. Off by
    /// default: a company that hasn't opted in must never start leaking its
    /// account number onto documents just because the fields exist.</summary>
    public bool ShowBankDetailsOnBill { get; set; }

    /// <summary>Bank details are only printable when the company asked for
    /// them *and* actually filled them in — a toggle switched on against
    /// empty fields prints nothing rather than an empty labelled row.</summary>
    public bool CanPrintBankDetails =>
        ShowBankDetailsOnBill
        && (!string.IsNullOrWhiteSpace(BankAccountNo) || !string.IsNullOrWhiteSpace(Ifsc));

    /// <summary>The chosen theme, applied on startup and whenever the
    /// sidebar toggle is flipped — so it survives to the next launch.</summary>
    public AppThemeKind Theme { get; set; } = AppThemeKind.Light;
}

/// <summary>Sequential document numbers, one set per company so each
/// company's Trip/LR/Bill/Employee numbering starts fresh at 1 regardless
/// of how many other companies are onboarded.</summary>
public class Counter : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int LastNumber { get; set; }
}

/// <summary>A login belonging to exactly one company (Owner/CoOwner/
/// Accountant) — never the EnterpriseAdmin identity, which is not a row
/// here at all. Username is unique across the whole system (not just
/// within a company) so login needs no separate "which company" step.</summary>
public class User : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Accountant;
    public bool IsActive { get; set; } = true;

    /// <summary>Forces the change-password screen on next login — set on the
    /// Owner account EnterpriseAdmin creates at onboarding and on every new
    /// user Owner creates afterwards.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginOn { get; set; }

    public string Display => $"{Username} — {DisplayName} ({Role})";
}

// ── Transactional ───────────────────────────────────────────────────────

public class Trip : BaseEntity, ITenantEntity, IAuditable
{
    public Guid CompanyId { get; set; }
    public string TripNo { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    public Guid PartyId { get; set; }
    public Party Party { get; set; } = null!;

    public Guid FromCityId { get; set; }
    public City FromCity { get; set; } = null!;
    public string? FromAddress { get; set; }

    public Guid ToCityId { get; set; }
    public City ToCity { get; set; } = null!;
    public string? ToAddress { get; set; }

    // Free text, not master-backed — these vary trip to trip and are not
    // necessarily the same as the billing Party.
    public string ConsignorName { get; set; } = string.Empty;
    public string? ConsignorAddress { get; set; }
    public string ConsigneeName { get; set; } = string.Empty;
    public string? ConsigneeAddress { get; set; }

    public decimal? Weight { get; set; }
    public decimal? Rate { get; set; }
    public decimal Amount { get; set; }

    public decimal StartReading { get; set; }
    public decimal? EndReading { get; set; }

    /// <summary>Only meaningful when the vehicle's Ownership is Other —
    /// entered by hand per trip, no stored commission rate.</summary>
    public decimal? CommissionAmount { get; set; }

    public string? Remarks { get; set; }

    /// <summary>Assigned by NumberService on first LR print; reused on every reprint.</summary>
    public string? LrNo { get; set; }

    /// <summary>The carrier's own way bill number for this trip, captured by
    /// hand rather than allocated — optional, and printed on the LR only when
    /// it was actually entered.</summary>
    public string? WayBillNo { get; set; }

    /// <summary>Assigned by NumberService on first Bill print; reused on every reprint.</summary>
    public string? BillNo { get; set; }

    /// <summary>Open through booking and reconciliation; Closed is a
    /// deliberate step on the Close Trip screen, always reversible via
    /// Reopen — never a delete, never automatic.</summary>
    public TripStatus Status { get; set; } = TripStatus.Open;
    public DateTime? ClosedOn { get; set; }
    public Guid? ClosedByUserId { get; set; }

    public ICollection<TripExpense> Expenses { get; set; } = [];
    public ICollection<TripTransaction> Transactions { get; set; } = [];

    // ── Derived — never stored, always computed from child rows ──────────

    public decimal TotalExpenses => Expenses.Sum(e => e.Amount);

    public decimal TotalApprovedReceived =>
        Transactions.Where(t => t.ApprovalStatus == ApprovalStatus.Approved).Sum(t => t.Amount);

    /// <summary>What the party still owes against the billed freight amount.
    /// Only Approved transactions count — a Pending entry does not move
    /// this until the Owner approves it.</summary>
    public decimal BalanceReceivable => Amount - TotalApprovedReceived;

    /// <summary>The company's/owner's actual take after operating expenses
    /// and — for an Other-owner vehicle — the commission paid out.</summary>
    public decimal NetAfterExpenses => Amount - TotalExpenses - (CommissionAmount ?? 0m);

    /// <summary>Whether this trip's expenses and amounts received are the
    /// company's own money — true for the owned fleet, false for another
    /// owner's vehicle, where that money is just passing through and only
    /// the commission is actually the company's.</summary>
    public bool IsOwnAccounting => Vehicle is null || Vehicle.Ownership != VehicleOwnership.Other;

    /// <summary>Revenue that belongs in the company's own books: the full
    /// freight for an owned vehicle, or just the commission for another
    /// owner's vehicle — never the freight itself, which is collected on
    /// that owner's behalf and passed on.</summary>
    public decimal CompanyRevenue => IsOwnAccounting ? Amount : (CommissionAmount ?? 0m);

    /// <summary>This trip's expenses, but only when they're the company's
    /// own — zero for another owner's vehicle, whose running costs are that
    /// owner's, not the company's.</summary>
    public decimal CompanyExpenses => IsOwnAccounting ? TotalExpenses : 0m;
}

public class TripExpense : BaseEntity, ITenantEntity, IAuditable
{
    // Denormalized from Trip.CompanyId (rather than only reachable by
    // joining through Trip) so a by-id lookup like DeleteExpenseAsync stays
    // safely tenant-scoped by the same global query filter as everything
    // else, with no risk of forgetting a manual join.
    public Guid CompanyId { get; set; }

    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.Today;

    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;

    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>An amount received against a trip. Sits Pending until the Owner
/// approves it — the entity the Approvals screen lists and acts on.</summary>
public class TripTransaction : BaseEntity, ITenantEntity, IAuditable
{
    public Guid CompanyId { get; set; }

    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    public string? Remarks { get; set; }

    public Guid? EnteredByUserId { get; set; }

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public string? ApprovalRemarks { get; set; }
}

public class VehicleMaintenance : BaseEntity, ITenantEntity, IAuditable
{
    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.Today;

    public Guid MaintenanceCategoryId { get; set; }
    public MaintenanceCategory MaintenanceCategory { get; set; } = null!;

    public decimal? OdometerReading { get; set; }
    public string? VendorName { get; set; }
    public decimal Amount { get; set; }

    public DateTime? NextDueDate { get; set; }
    public decimal? NextDueOdometer { get; set; }
    public string? Remarks { get; set; }
}

public class DriverLedgerEntry : BaseEntity, ITenantEntity, IAuditable
{
    public Guid CompanyId { get; set; }
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.Today;
    public DriverLedgerEntryType Type { get; set; } = DriverLedgerEntryType.AdvanceGiven;
    public decimal Amount { get; set; }

    /// <summary>SalaryPaid entries only, e.g. "2026-08".</summary>
    public string? ForMonth { get; set; }
    public string? Remarks { get; set; }
}

// ── Audit trail ──────────────────────────────────────────────────────────

/// <summary>Marks a row whose every creation, change and deletion is written
/// to the audit trail. Opt-in by design: masters like City or ExpenseCategory
/// change rarely and carry no money, so auditing them would bury the entries
/// that actually matter — the ones touching cash and vehicle records.
/// AppDbContext picks this up automatically on save, so implementing it is
/// the only step needed to bring a new entity under audit.</summary>
public interface IAuditable
{
}

/// <summary>One row per change to an <see cref="IAuditable"/> entity: who did
/// it, when, and — for an edit — exactly which fields moved and from what to
/// what. Append-only in practice; nothing in the app updates or deletes these,
/// which is the whole point of keeping them.</summary>
public class AuditLog : BaseEntity, ITenantEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>The entity's type name, e.g. "TripExpense" — stored as plain
    /// text rather than an enum so adding a newly audited entity never needs
    /// a migration here.</summary>
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    /// <summary>The trip this change belongs to, when it belongs to one, so a
    /// trip's whole history can be fetched in a single indexed query instead
    /// of joining back through each child table.</summary>
    public Guid? TripId { get; set; }

    public AuditAction Action { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedOn { get; set; } = DateTime.Now;

    /// <summary>A one-line plain-English description of the change, written at
    /// capture time — the reader shouldn't have to parse JSON to see what
    /// happened, and the phrasing can't drift later as the code changes.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Field-level detail as JSON: [{ "field": ..., "from": ..., "to": ... }].
    /// Null for a creation, where there is no "from" worth recording.</summary>
    public string? Changes { get; set; }
}
