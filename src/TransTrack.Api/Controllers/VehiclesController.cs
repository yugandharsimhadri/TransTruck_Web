using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicles")]
public class VehiclesController(VehicleService vehicles) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Vehicle>>> Get() => Ok(await vehicles.GetVehiclesAsync());

    [HttpPost]
    public async Task<IActionResult> Save(Vehicle vehicle)
    {
        try
        {
            await vehicles.SaveVehicleAsync(vehicle);
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
        await vehicles.DeleteVehicleAsync(id);
        return NoContent();
    }
}
