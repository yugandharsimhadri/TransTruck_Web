namespace TransTrack.Data;

/// <summary>
/// The two things <see cref="AppDbContext"/> needs to know about who is
/// using the application: their id, so every row it saves can be stamped
/// with it, and their company, so every tenant-scoped row is both stamped
/// and filtered by it. EnterpriseAdmin — never a Users row, never scoped to
/// a company — resolves both as null; its endpoints work in terms of an
/// explicit companyId parameter instead, deliberately bypassing the
/// per-request company scoping this interface drives everywhere else.
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    Guid? CompanyId { get; }
}

/// <summary>The default when nobody is signed in — every design-time and
/// test context that has no real session gets this, so a missing DI
/// registration is a null user rather than a startup crash.</summary>
public class NullCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId => null;
    public Guid? CompanyId => null;
}
