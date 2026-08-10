namespace TransTrack.Api.Auth;

/// <summary>
/// The three shapes a JWT this API issues can take, carried as a claim on
/// the token itself. Mirrors <c>LoginViewModel.Stage</c> on the desktop —
/// Credentials/ChangePassword/Recovery there is Full/ChangePassword/Recovery
/// here, just expressed as "what this token is allowed to do" instead of
/// "what screen is showing".
/// </summary>
public static class TokenTypes
{
    public const string ClaimType = "token_type";

    /// <summary>Carries the signed-in user's CompanyId on a full-session
    /// token only — never on a recovery token, since EnterpriseAdmin is
    /// never scoped to a company.</summary>
    public const string CompanyIdClaimType = "company_id";

    /// <summary>A normal signed-in session — every endpoint except the auth
    /// ones requires this.</summary>
    public const string Full = "full";

    /// <summary>Issued after a correct password when <c>MustChangePassword</c>
    /// is set. Only authorizes <c>POST /api/auth/change-password</c>.</summary>
    public const string ChangePassword = "change-password";

    /// <summary>Issued to EnterpriseAdmin. Only authorizes the two recovery
    /// endpoints — never a normal session, whatever role claim it might
    /// otherwise have carried.</summary>
    public const string Recovery = "recovery";
}

/// <summary>Named authorization policies built from <see cref="TokenTypes"/>
/// and the JWT role claim.</summary>
public static class Policies
{
    /// <summary>The default policy — a full session, any role.</summary>
    public const string FullSession = "FullSession";

    public const string ChangePasswordToken = "ChangePasswordToken";
    public const string RecoveryToken = "RecoveryToken";

    /// <summary>Mirrors <c>CurrentUserService.CanApprove</c> — Owner only.</summary>
    public const string Owner = "Owner";

    /// <summary>Mirrors <c>CurrentUserService.CanManageSettings</c> — Owner or CoOwner.</summary>
    public const string ManageSettings = "ManageSettings";
}
