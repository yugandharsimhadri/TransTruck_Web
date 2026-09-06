using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>A vehicle's paperwork costs — insurance, road tax and the like.
/// Separate from MaintenanceController: maintenance is a repair that happened,
/// this is a renewal that comes round. A schedule and the instalments it
/// generates are both reachable here, since the screen shows them together.</summary>
[ApiController]
[Authorize]
[Route("api/vehicles/{vehicleId:guid}")]
public class VehicleExpensesController(VehicleExpenseService expenses) : ControllerBase
{
    [HttpGet("expense-schedules")]
    public async Task<ActionResult<List<VehicleExpenseSchedule>>> GetSchedules(Guid vehicleId)
        => Ok(await expenses.GetSchedulesAsync(vehicleId));

    [HttpPost("expense-schedules")]
    public async Task<ActionResult<Guid>> SaveSchedule(Guid vehicleId, VehicleExpenseSchedule schedule)
    {
        // The route is the authority on which vehicle this belongs to — a body
        // naming a different one is a bug in the caller, not a second opinion.
        schedule.VehicleId = vehicleId;
        return Ok(await expenses.SaveScheduleAsync(schedule));
    }

    [HttpDelete("expense-schedules/{scheduleId:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid scheduleId)
    {
        await expenses.DeleteScheduleAsync(scheduleId);
        return NoContent();
    }

    [HttpGet("expenses")]
    public async Task<ActionResult<List<VehicleExpense>>> GetExpenses(Guid vehicleId)
        => Ok(await expenses.GetExpensesAsync(vehicleId));

    [HttpPost("expenses")]
    public async Task<ActionResult<Guid>> SaveExpense(Guid vehicleId, VehicleExpense expense)
    {
        expense.VehicleId = vehicleId;
        return Ok(await expenses.SaveExpenseAsync(expense));
    }

    [HttpDelete("expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid expenseId)
    {
        await expenses.DeleteExpenseAsync(expenseId);
        return NoContent();
    }
}
