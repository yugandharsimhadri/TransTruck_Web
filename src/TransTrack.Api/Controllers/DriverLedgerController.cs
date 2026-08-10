using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/driver-ledger")]
public class DriverLedgerController(DriverLedgerService ledger) : ControllerBase
{
    [HttpGet("driver/{driverId:guid}")]
    public async Task<ActionResult<List<DriverLedgerEntry>>> GetForDriver(Guid driverId)
        => Ok(await ledger.GetForDriverAsync(driverId));

    [HttpGet("driver/{driverId:guid}/advance-outstanding")]
    public async Task<ActionResult<decimal>> GetAdvanceOutstanding(Guid driverId)
        => Ok(await ledger.GetAdvanceOutstandingAsync(driverId));

    [HttpPost]
    public async Task<IActionResult> Save(DriverLedgerEntry entry)
    {
        try
        {
            await ledger.SaveAsync(entry);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await ledger.DeleteAsync(id);
        return NoContent();
    }
}
