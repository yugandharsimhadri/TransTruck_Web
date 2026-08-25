using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>Downloading and removing a stored document. Shared by vehicles and
/// drivers because once a document exists it is addressed by its own id — the
/// owner only matters when listing or adding one, which those controllers
/// handle.</summary>
[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController(DocumentService documents) : ControllerBase
{
    /// <summary>The file itself, for download or sharing. 404 with a plain
    /// message when there is nothing stored — including when the row exists
    /// but its file has gone missing, which the client shows as "not
    /// available" rather than an error.</summary>
    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId)
    {
        var doc = await documents.OpenAsync(documentId);
        if (doc is null) return NotFound(new { message = "That document is no longer available." });

        return File(doc.Value.Content, doc.Value.ContentType, doc.Value.FileName);
    }

    /// <summary>The upload rules this server is configured with, so the
    /// client can reject an oversized file before spending a phone's data
    /// sending it — and so the limit lives in one place (appsettings.json)
    /// rather than being repeated as a magic number on the frontend.</summary>
    [HttpGet("limits")]
    public ActionResult<object> Limits() => Ok(new
    {
        maxBytes = documents.MaxBytes,
        maxMb = documents.MaxMb,
        accepted = DocumentFormats.Accepted,
    });

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId)
    {
        await documents.DeleteAsync(documentId);
        return NoContent();
    }
}
