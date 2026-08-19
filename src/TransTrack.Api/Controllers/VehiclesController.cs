using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicles")]
public class VehiclesController(VehicleService vehicles, VehicleDocumentService documents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Vehicle>>> Get() => Ok(await vehicles.GetVehiclesAsync());

    [HttpPost]
    public async Task<IActionResult> Save(Vehicle vehicle)
    {
        try
        {
            // The id comes back so the form can stay open on a newly created
            // vehicle and offer its document upload straight away.
            return Ok(await vehicles.SaveVehicleAsync(vehicle));
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

    // ── Vehicle document (one per vehicle, managed from the vehicle form) ──

    /// <summary>Whether this vehicle has a document, and what it is. Returns
    /// 200 with a null body when there isn't one — "no document yet" is a
    /// normal answer the form shows a gentle line for, not a 404 the client
    /// has to treat as an error.</summary>
    [HttpGet("{id:guid}/document")]
    public async Task<ActionResult<VehicleDocumentInfo?>> GetDocumentInfo(Guid id)
        => Ok(await documents.GetInfoAsync(id));

    [HttpPost("{id:guid}/document")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(Guid id, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a file to upload." });

        try
        {
            await using var stream = file.OpenReadStream();
            await documents.SaveAsync(id, file.FileName, file.ContentType ?? "application/octet-stream", stream, file.Length);
            return Ok(await documents.GetInfoAsync(id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>The file itself, for download or sharing. 404 with a plain
    /// message when there is nothing stored — including when the row exists
    /// but its file has gone missing, which the client shows as "no document
    /// uploaded" rather than an error.</summary>
    [HttpGet("{id:guid}/document/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var doc = await documents.OpenAsync(id);
        if (doc is null) return NotFound(new { message = "No document has been uploaded for this vehicle." });

        return File(doc.Value.Content, doc.Value.ContentType, doc.Value.FileName);
    }

    [HttpDelete("{id:guid}/document")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        await documents.DeleteAsync(id);
        return NoContent();
    }
}
