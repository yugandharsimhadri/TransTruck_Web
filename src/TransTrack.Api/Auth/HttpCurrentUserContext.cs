using System.IdentityModel.Tokens.Jwt;
using TransTrack.Data;

namespace TransTrack.Api.Auth;

/// <summary>
/// Request-scoped <see cref="ICurrentUserContext"/> for the API. The desktop
/// app has exactly one signed-in user for the whole process; the API serves
/// concurrent requests from different users (and different companies), so
/// this reads the "sub" and "company_id" claims off the ambient
/// <see cref="IHttpContextAccessor"/> instead. Registered as a Singleton —
/// that's fine because <see cref="IHttpContextAccessor"/> tracks the current
/// request via <see cref="AsyncLocal{T}"/>, so it resolves correctly
/// regardless of which internal scope <c>IDbContextFactory.CreateDbContextAsync()</c>
/// creates its DbContext in. Both resolve to null for an EnterpriseAdmin
/// recovery token — that identity is never a Users row and never scoped to
/// a company, so AppDbContext's global tenant filter correctly shows it no
/// tenant data at all.
/// </summary>
public class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public Guid? UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? CompanyId
    {
        get
        {
            var companyId = accessor.HttpContext?.User?.FindFirst(TokenTypes.CompanyIdClaimType)?.Value;
            return Guid.TryParse(companyId, out var id) ? id : null;
        }
    }
}
