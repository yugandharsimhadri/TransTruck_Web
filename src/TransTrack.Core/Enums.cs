namespace TransTrack.Core;

public enum UserRole
{
    Owner = 1,
    CoOwner = 2,
    Accountant = 3
}

/// <summary>Who may create or change whom.</summary>
public static class UserRoleHierarchy
{
    /// <summary>
    /// True when <paramref name="actor"/> is allowed to create a user with
    /// <paramref name="target"/>'s role, or to edit an existing one holding it.
    ///
    /// The rule is simply "never above your own level": the enum is ordered by
    /// authority (Owner = 1 is the highest), so an Owner manages everyone, a
    /// CoOwner manages CoOwners and Accountants but never an Owner, and an
    /// Accountant manages only other Accountants.
    ///
    /// Peers are deliberately included — an Accountant can add an Accountant,
    /// which is the case that decided it. Without that, the rule would have to
    /// be "strictly below", and the lowest role could add nobody at all.
    /// </summary>
    public static bool CanManage(this UserRole actor, UserRole target) => actor <= target;
}

/// <summary>What a stored document is attached to. One table serves both
/// rather than a near-identical table per owner — the file handling, the
/// storage layout and the "no document yet" behaviour are the same either
/// way, and parties or trips can be added later without another copy.</summary>
public enum DocumentOwnerKind
{
    Vehicle = 0,
    Driver = 1
}

/// <summary>The kind of paper a stored document is. Every one is optional —
/// a vehicle or driver with nothing uploaded is a normal state, not an
/// incomplete record.</summary>
public enum DocumentType
{
    // Vehicle papers.
    Permit = 0,
    NationalPermit = 1,
    Insurance = 2,
    Fitness = 3,
    Pollution = 4,

    // Driver papers.
    AadhaarCard = 20,
    DriverLicence = 21,

    /// <summary>Anything the lists above don't cover — deliberately last, and
    /// available to both, so an unusual document still has a home instead of
    /// being filed under a label that misdescribes it.</summary>
    Others = 99
}

public static class DocumentTypes
{
    private static readonly DocumentType[] VehicleTypes =
    [
        DocumentType.Permit, DocumentType.NationalPermit, DocumentType.Insurance,
        DocumentType.Fitness, DocumentType.Pollution, DocumentType.Others
    ];

    private static readonly DocumentType[] DriverTypes =
    [
        DocumentType.AadhaarCard, DocumentType.DriverLicence, DocumentType.Others
    ];

    /// <summary>The types offered for one kind of owner. Enforced server-side
    /// as well as in the picker, so an Insurance document can never end up
    /// filed against a driver.</summary>
    public static IReadOnlyList<DocumentType> For(DocumentOwnerKind kind) =>
        kind == DocumentOwnerKind.Driver ? DriverTypes : VehicleTypes;

    public static bool IsValidFor(DocumentOwnerKind kind, DocumentType type) => For(kind).Contains(type);

    /// <summary>How the type reads on screen — the enum names are compressed
    /// for code, not for a lorry owner.</summary>
    public static string Label(this DocumentType type) => type switch
    {
        DocumentType.NationalPermit => "National permit",
        DocumentType.AadhaarCard => "Aadhaar card",
        DocumentType.DriverLicence => "Driving licence",
        _ => type.ToString()
    };
}

public enum VehicleOwnership
{
    Own = 1,
    Other = 2
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>A trip stays Open through booking, LR printing and expense/
/// payment entry. Closing it is a deliberate step on the Close Trip screen
/// once the amounts have been reconciled — never automatic — and can always
/// be undone with Reopen.</summary>
public enum TripStatus
{
    Open = 1,
    Closed = 2
}

public enum PaymentMode
{
    Cash = 1,
    Bank = 2,
    Upi = 3,
    Cheque = 4
}

/// <summary>What an amount received against a trip actually is — money paid
/// up front against the freight, or a payment toward the settlement. Every
/// TripTransaction is exactly one of these; the two together, summed, are
/// the whole of what a trip has received (Trip.TotalApprovedReceived).
/// Deliberately not the same concept as DriverLedgerEntryType.AdvanceGiven,
/// which is a driver's wage advance — this is the party's money, not the
/// driver's.</summary>
public enum ReceiptType
{
    Advance = 1,
    Payment = 2
}

public enum DriverLedgerEntryType
{
    SalaryPaid = 1,
    AdvanceGiven = 2,
    Deduction = 3
}

public enum AppThemeKind
{
    Light = 1,
    Dark = 2
}

/// <summary>What happened to a row, as recorded in the audit trail. A soft
/// delete (IsDeleted flipped on) is reported as <see cref="Deleted"/> rather
/// than as the Update it technically is at the database level — the audit
/// trail is there to answer "what did someone do", not "what did EF do".</summary>
public enum AuditAction
{
    Created,
    Updated,
    Deleted
}
