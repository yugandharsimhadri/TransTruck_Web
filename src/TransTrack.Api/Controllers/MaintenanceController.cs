using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/maintenance")]
public class MaintenanceController(MaintenanceService maintenance) : ControllerBase
{
    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<ActionResult<List<VehicleMaintenance>>> GetForVehicle(Guid vehicleId)
        => Ok(await maintenance.GetForVehicleAsync(vehicleId));

    [HttpPost]
    public async Task<IActionResult> Save(VehicleMaintenance record)
    {
        await maintenance.SaveAsync(record);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await maintenance.DeleteAsync(id);
        return NoContent();
    }
}
