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
