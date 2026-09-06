using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>Figures for one month. No year/month means the current one,
    /// so an older client calling this without parameters keeps working.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary(
        [FromQuery] int? year, [FromQuery] int? month)
        => Ok(await dashboard.GetSummaryAsync(year, month));

    [HttpGet("months")]
    public async Task<ActionResult<List<MonthOption>>> GetMonths([FromQuery] int minimumMonths = 6)
        => Ok(await dashboard.GetSelectableMonthsAsync(minimumMonths));

    [HttpGet("monthly")]
    public async Task<ActionResult<List<MonthlyFigure>>> GetMonthly([FromQuery] int months = 6)
        => Ok(await dashboard.GetMonthlyFiguresAsync(months));

    [HttpGet("expense-by-category")]
    public async Task<ActionResult<List<CategoryFigure>>> GetExpenseByCategory([FromQuery] int months = 6)
        => Ok(await dashboard.GetExpenseByCategoryAsync(months));

    [HttpGet("compliance-alerts")]
    public async Task<ActionResult<List<ComplianceAlert>>> GetComplianceAlerts([FromQuery] int withinDays = 30)
        => Ok(await dashboard.GetComplianceAlertsAsync(withinDays));
}
