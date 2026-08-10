using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class TripTransactionsController(TripTransactionService transactions, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost("trips/{tripId:guid}/transactions")]
    public async Task<IActionResult> Add(Guid tripId, TripTransaction transaction)
    {
        try
        {
            await transactions.AddAsync(tripId, transaction, currentUser.UserId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Every pending transaction across every trip — the
    /// Owner-only Approvals screen.</summary>
    [HttpGet("approvals/pending")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<List<TripTransaction>>> GetPending() => Ok(await transactions.GetPendingAsync());

    public record ApprovalRequest(string? Remarks);

    [HttpPost("approvals/{transactionId:guid}/approve")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<IActionResult> Approve(Guid transactionId, ApprovalRequest request)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        await transactions.ApproveAsync(transactionId, userId, request.Remarks);
        return Ok();
    }

    [HttpPost("approvals/{transactionId:guid}/reject")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<IActionResult> Reject(Guid transactionId, ApprovalRequest request)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        await transactions.RejectAsync(transactionId, userId, request.Remarks);
        return Ok();
    }
}
