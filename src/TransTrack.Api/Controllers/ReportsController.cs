using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Documents;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>The same three filterable views the desktop Reports screen
/// has — trips, maintenance, and the combined income/expense ledger — each
/// taking the same vehicle/driver/date/ownership filters so the frontend's
/// one filter panel drives all three. Each also has a PDF and Excel export,
/// built from exactly the rows the matching JSON query returns — export
/// always matches what's on screen, never a fresh query of its own.</summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(ReportsService reports, MasterDataService masters) : ControllerBase
{
    [HttpGet("trips")]
    public async Task<ActionResult<List<Trip>>> GetTrips(
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] VehicleOwnership? ownership)
        => Ok(await reports.GetTripsAsync(vehicleId, driverId, from, to, ownership));

    [HttpGet("maintenance")]
    public async Task<ActionResult<List<VehicleMaintenance>>> GetMaintenance(
        [FromQuery] Guid? vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await reports.GetMaintenanceAsync(vehicleId, from, to));

    [HttpGet("ledger")]
    public async Task<ActionResult<List<LedgerRow>>> GetLedger(
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] VehicleOwnership? ownership)
        => Ok(await reports.GetLedgerAsync(vehicleId, driverId, from, to, ownership));

    // ── Exports ──────────────────────────────────────────────────────────────

    [HttpGet("trips/export.pdf")]
    public async Task<IActionResult> ExportTripsPdf(
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] VehicleOwnership? ownership)
    {
        var trips = await reports.GetTripsAsync(vehicleId, driverId, from, to, ownership);
        var company = await masters.GetCompanyAsync();
        var pdf = ReportPdfBuilder.BuildTrips(trips, company, FilterSummary(from, to, ownership));
        return File(pdf, "application/pdf", "trips-report.pdf");
    }

    [HttpGet("trips/export.xlsx")]
    public async Task<IActionResult> ExportTripsExcel(
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] VehicleOwnership? ownership)
    {
        var trips = await reports.GetTripsAsync(vehicleId, driverId, from, to, ownership);
        var xlsx = ReportExcelBuilder.BuildTrips(trips);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "trips-report.xlsx");
    }

    [HttpGet("maintenance/export.pdf")]
    public async Task<IActionResult> ExportMaintenancePdf(
        [FromQuery] Guid? vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var records = await reports.GetMaintenanceAsync(vehicleId, from, to);
        var company = await masters.GetCompanyAsync();
        var pdf = ReportPdfBuilder.BuildMaintenance(records, company, FilterSummary(from, to, null));
        return File(pdf, "application/pdf", "maintenance-report.pdf");
    }

    [HttpGet("maintenance/export.xlsx")]
    public async Task<IActionResult> ExportMaintenanceExcel(
        [FromQuery] Guid? vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var records = await reports.GetMaintenanceAsync(vehicleId, from, to);
        var xlsx = ReportExcelBuilder.BuildMaintenance(records);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "maintenance-report.xlsx");
    }

    [HttpGet("ledger/export.pdf")]
    public async Task<IActionResult> ExportLedgerPdf(
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] VehicleOwnership? ownership)
    {
        var rows = await reports.GetLedgerAsync(vehicleId, driverId, from, to, ownership);
        var company = await masters.GetCompanyAsync();
        var pdf = ReportPdfBuilder.BuildLedger(rows, company, FilterSummary(from, to, ownership));
        return File(pdf, "application/pdf", "transactions-report.pdf");
    }

    [HttpGet("ledger/export.xlsx")]
    public async Task<IActionResult> ExportLedgerExcel(
        [FromQuery] Guid? vehicleId, [FromQuery] Guid? driverId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] VehicleOwnership? ownership)
    {
        var rows = await reports.GetLedgerAsync(vehicleId, driverId, from, to, ownership);
        var xlsx = ReportExcelBuilder.BuildLedger(rows);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "transactions-report.xlsx");
    }

    private static string FilterSummary(DateTime? from, DateTime? to, VehicleOwnership? ownership)
    {
        var parts = new List<string>();
        if (from is { } f) parts.Add($"From {f:dd-MMM-yyyy}");
        if (to is { } t) parts.Add($"To {t:dd-MMM-yyyy}");
        if (ownership is { } o) parts.Add(o == VehicleOwnership.Own ? "Own fleet only" : "Other-owner only");
        return parts.Count == 0 ? "All records" : string.Join("   |   ", parts);
    }
}
