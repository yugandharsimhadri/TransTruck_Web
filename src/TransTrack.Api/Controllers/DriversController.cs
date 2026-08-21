using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/drivers")]
public class DriversController(DriverService drivers, DocumentService documents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Driver>>> Get() => Ok(await drivers.GetDriversAsync());

    [HttpPost]
    public async Task<IActionResult> Save(Driver driver)
    {
        try
        {
            // The id comes back so the form can stay open on a newly created
            // driver and offer its document upload straight away.
            return Ok(await drivers.SaveDriverAsync(driver));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await drivers.DeleteDriverAsync(id);
        return NoContent();
    }

    // ── Driver documents (Aadhaar card, driving licence, other) ────────────
    // Download and removal are shared with vehicles in DocumentsController,
    // since a document is addressed by its own id once it exists.

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<List<DocumentInfo>>> GetDocuments(Guid id)
        => Ok(await documents.ListAsync(DocumentOwnerKind.Driver, id));

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(Guid id, IFormFile file, [FromForm] DocumentType documentType)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a file to upload." });

        try
        {
            await using var stream = file.OpenReadStream();
            await documents.AddAsync(DocumentOwnerKind.Driver, id, documentType,
                file.FileName, file.ContentType ?? "application/octet-stream", stream, file.Length);

            return Ok(await documents.ListAsync(DocumentOwnerKind.Driver, id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
