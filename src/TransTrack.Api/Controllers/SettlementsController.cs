using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>
/// Bulk settlement: one payment from a party against many open trips.
///
/// Creating one is ordinary staff work — it records money arriving, which is
/// no more privileged than recording a single receipt. Deciding one is
/// Owner-only, matching every other approval: the whole point is that the
/// books are written by the Owner's decision.
/// </summary>
[ApiController]
[Authorize]
[Route("api/settlements")]
public class SettlementsController(SettlementService settlements, ICurrentUserContext currentUser) : ControllerBase
{
    /// <summary>What this party still owes, trip by trip — the list the
    /// settlement screen ticks through.</summary>
    [HttpGet("settleable")]
    public async Task<ActionResult<List<SettleableTrip>>> GetSettleable([FromQuery] Guid partyId)
        => Ok(await settlements.GetSettleableTripsAsync(partyId));

    public record CreateRequest(
        Guid PartyId,
        DateTime Date,
        PaymentMode PaymentMode,
        List<Guid> TripIds,
        string? Remarks);

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateRequest request)
        => Ok(await settlements.CreateAsync(
            request.PartyId, request.Date, request.PaymentMode,
            request.TripIds, request.Remarks, currentUser.UserId));

    [HttpGet("pending")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<List<Settlement>>> GetPending() => Ok(await settlements.GetPendingAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Settlement>> Get(Guid id)
        => await settlements.GetAsync(id) is { } settlement ? Ok(settlement) : NotFound();

    public record DecisionRequest(string? Remarks);

    /// <summary>Approves the whole settlement: posts every receipt and closes
    /// every trip it covers, together.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<IActionResult> Approve(Guid id, DecisionRequest request)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        await settlements.ApproveAsync(id, userId, request.Remarks);
        return Ok();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<IActionResult> Reject(Guid id, DecisionRequest request)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        await settlements.RejectAsync(id, userId, request.Remarks);
        return Ok();
    }
}
