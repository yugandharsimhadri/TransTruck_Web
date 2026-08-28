using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Api.Documents;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/trips")]
public class TripsController(TripService trips, MasterDataService masters, ICurrentUserContext currentUser) : ControllerBase
{
    /// <summary>The trips list. Returns the flat list projection rather than
    /// the full object graph — see TripService.GetTripListAsync for why.</summary>
    [HttpGet]
    public async Task<ActionResult<TripListPage>> Get(
        [FromQuery] TripStatus? status,
        [FromQuery] string? regNo,
        [FromQuery] TripListSort sort = TripListSort.DateDesc,
        [FromQuery] int skip = 0,
        [FromQuery] int take = TripService.DefaultPageSize)
        => Ok(await trips.GetTripListAsync(status, regNo, sort, skip, take));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Trip>> GetById(Guid id)
    {
        var trip = await trips.GetTripAsync(id);
        return trip is null ? NotFound() : Ok(trip);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Trip trip)
    {
        var id = await trips.SaveTripAsync(trip);
        return Ok(await trips.GetTripAsync(id));
    }

    /// <summary>Cancelling a trip. Owner-only, deliberately: a trip carries
    /// its own numbering, expenses and approved amounts, so withdrawing one
    /// is the most destructive act on the record and that authority sits with
    /// the Owner alone — the same rule already applied to deleting an
    /// approved amount.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await trips.DeleteTripAsync(id);
        return NoContent();
    }

    // ── Expenses ──────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/expenses")]
    public async Task<IActionResult> AddExpense(Guid id, TripExpense expense)
    {
        // Notably the closed-trip refusal: ApiExceptionHandler turns it into
        // a 400 with the message telling the user to reopen the trip, not a 500.
        await trips.AddExpenseAsync(id, expense);
        return Ok(await trips.GetTripAsync(id));
    }

    [HttpDelete("expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid expenseId)
    {
        await trips.DeleteExpenseAsync(expenseId);
        return NoContent();
    }

    // ── Printing numbers ──────────────────────────────────────────────────

    [HttpPost("{id:guid}/lr-number")]
    public async Task<IActionResult> AssignLrNumber(Guid id)
    {
        var (lrNo, isFirstPrint) = await trips.AssignLrNumberAsync(id);
        return Ok(new { lrNo, isFirstPrint });
    }

    [HttpPost("{id:guid}/bill-number")]
    public async Task<IActionResult> AssignBillNumber(Guid id)
    {
        var (billNo, isFirstPrint) = await trips.AssignBillNumberAsync(id);
        return Ok(new { billNo, isFirstPrint });
    }

    // ── Printable documents ─────────────────────────────────────────────────

    [HttpGet("{id:guid}/lr")]
    public async Task<IActionResult> GetLr(Guid id)
    {
        var trip = await trips.GetTripAsync(id);
        if (trip is null) return NotFound();

        var (lrNo, isFirstPrint) = await trips.AssignLrNumberAsync(id);
        trip.LrNo = lrNo;

        var company = await masters.GetCompanyAsync();
        var pdf = LrDocument.Build(trip, company, isReprint: !isFirstPrint);
        return File(pdf, "application/pdf", $"LR-{lrNo}.pdf");
    }

    [HttpGet("{id:guid}/bill")]
    public async Task<IActionResult> GetBill(Guid id)
    {
        var trip = await trips.GetTripAsync(id);
        if (trip is null) return NotFound();

        var (billNo, isFirstPrint) = await trips.AssignBillNumberAsync(id);
        trip.BillNo = billNo;

        var company = await masters.GetCompanyAsync();
        var pdf = BillDocument.Build(trip, company, isReprint: !isFirstPrint);
        return File(pdf, "application/pdf", $"Bill-{billNo}.pdf");
    }

    // ── Close / reopen ────────────────────────────────────────────────────

    /// <summary>Mirrors <c>TripEntryEditorViewModel.CloseTripAsync</c> — the
    /// party must be settled in full before a trip can be put away.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        var trip = await trips.GetTripAsync(id);
        if (trip is null) return NotFound();

        if (trip.BalanceReceivable > 0)
            return BadRequest(new { message = "Total amount not received." });

        await trips.CloseAsync(id, currentUser.UserId);
        return Ok(await trips.GetTripAsync(id));
    }

    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id)
    {
        await trips.ReopenAsync(id);
        return Ok(await trips.GetTripAsync(id));
    }
}
