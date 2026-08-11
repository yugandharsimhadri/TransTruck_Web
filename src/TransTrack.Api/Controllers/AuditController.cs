using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>The audit trail, read-only. There is deliberately no write or
/// delete endpoint here — entries are produced automatically on save, and a
/// trail anyone could edit would be worth nothing.
///
/// The company-wide feed is limited to Owner/CoOwner: it spans every user's
/// activity, which is management information rather than something an
/// accountant needs day to day. A trip's own history is left open to anyone
/// who can already see that trip, since it explains records they're working
/// with.</summary>
[ApiController]
[Authorize]
[Route("api/audit")]
public class AuditController(AuditService audit) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ManageSettings)]
    public async Task<ActionResult<List<AuditEntryView>>> GetRecent(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? entityType, [FromQuery] int take = 100)
        => Ok(await audit.GetRecentAsync(from, to, entityType, take));

    [HttpGet("trip/{tripId:guid}")]
    public async Task<ActionResult<List<AuditEntryView>>> GetForTrip(Guid tripId)
        => Ok(await audit.GetForTripAsync(tripId));

    [HttpGet("record/{entityType}/{entityId:guid}")]
    public async Task<ActionResult<List<AuditEntryView>>> GetForRecord(string entityType, Guid entityId)
        => Ok(await audit.GetForRecordAsync(entityType, entityId));
}
