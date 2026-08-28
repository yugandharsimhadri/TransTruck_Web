using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicles")]
public class VehiclesController(VehicleService vehicles, DocumentService documents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Vehicle>>> Get() => Ok(await vehicles.GetVehiclesAsync());

    [HttpPost]
    public async Task<IActionResult> Save(Vehicle vehicle)
        // The id comes back so the form can stay open on a newly created
        // vehicle and offer its document upload straight away.
        => Ok(await vehicles.SaveVehicleAsync(vehicle));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await vehicles.DeleteVehicleAsync(id);
        return NoContent();
    }

    // ── Vehicle documents (many per vehicle, each with a type) ──────────────

    /// <summary>Everything on file for this vehicle, newest first. An empty
    /// list is a normal answer, not a 404 — plenty of vehicles have no papers
    /// uploaded yet.</summary>
    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<List<DocumentInfo>>> GetDocuments(Guid id)
        => Ok(await documents.ListAsync(DocumentOwnerKind.Vehicle, id));

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(Guid id, IFormFile file, [FromForm] DocumentType documentType)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a file to upload." });

        await using var stream = file.OpenReadStream();
        await documents.AddAsync(DocumentOwnerKind.Vehicle, id, documentType,
            file.FileName, file.ContentType ?? "application/octet-stream", stream, file.Length);

        return Ok(await documents.ListAsync(DocumentOwnerKind.Vehicle, id));
    }
}
