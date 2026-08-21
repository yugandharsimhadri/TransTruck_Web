using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>One stored document, without its bytes — enough to list what a
/// vehicle or driver has on file.</summary>
public record DocumentInfo(
    Guid Id,
    DocumentType DocumentType,
    string DocumentTypeLabel,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedOn);

/// <summary>Documents held against a vehicle or a driver. Kept apart from
/// <see cref="VehicleService"/> and <see cref="DriverService"/> deliberately:
/// nothing on the vehicle list, driver list, trip or dashboard path calls in
/// here, so a multi-megabyte upload can never ride along with a routine
/// query.</summary>
public class DocumentService(IDbContextFactory<AppDbContext> factory, IDocumentStorage storage)
{
    public long MaxBytes => Math.Max(1, AppConfig.Current.VehicleDocumentMaxMb) * 1024L * 1024L;

    /// <summary>Everything on file for one owner, newest first. An owner with
    /// nothing uploaded returns an empty list, which is a normal state.</summary>
    public async Task<List<DocumentInfo>> ListAsync(DocumentOwnerKind ownerKind, Guid ownerId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.OwnerKind == ownerKind && d.OwnerId == ownerId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return rows
            .Select(d => new DocumentInfo(d.Id, d.DocumentType, d.DocumentType.Label(),
                d.FileName, d.ContentType, d.SizeBytes, d.CreatedAt))
            .ToList();
    }

    /// <summary>Adds a document. Writes the bytes first and the row second: a
    /// file with no row is invisible clutter, but a row with no file is a
    /// broken download, so the ordering fails on the harmless side.</summary>
    public async Task<Guid> AddAsync(DocumentOwnerKind ownerKind, Guid ownerId, DocumentType documentType,
        string fileName, string contentType, Stream content, long sizeBytes)
    {
        if (sizeBytes <= 0) throw new InvalidOperationException("That file is empty.");
        if (sizeBytes > MaxBytes)
            throw new InvalidOperationException($"That file is larger than the {AppConfig.Current.VehicleDocumentMaxMb} MB limit.");

        if (!DocumentTypes.IsValidFor(ownerKind, documentType))
            throw new InvalidOperationException($"{documentType.Label()} is not a document type for a {ownerKind.ToString().ToLowerInvariant()}.");

        await using var db = await factory.CreateDbContextAsync();

        var companyId = await ResolveOwnerCompanyAsync(db, ownerKind, ownerId)
                        ?? throw new InvalidOperationException(
                            ownerKind == DocumentOwnerKind.Driver ? "Driver not found." : "Vehicle not found.");

        var document = new StoredDocument
        {
            Id = Guid.NewGuid(),
            OwnerKind = ownerKind,
            OwnerId = ownerId,
            DocumentType = documentType,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
        };

        document.StoredPath = await storage.SaveAsync(companyId, ownerKind, ownerId, document.Id, fileName, content);

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return document.Id;
    }

    /// <summary>Opens one document, or null when there isn't one — covering
    /// both "no such row" and "row exists but the file is gone". Both are
    /// answered the same calm way by the caller.</summary>
    public async Task<(Stream Content, string FileName, string ContentType)?> OpenAsync(Guid documentId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);
        if (doc is null) return null;

        var stream = await storage.OpenAsync(doc.StoredPath);
        if (stream is null)
        {
            // The row outlived its file — a restore that missed the documents
            // folder, or a hand-cleaned disk. Report it as "nothing here" and
            // let the user re-upload, rather than failing the request.
            AppLog.Info($"Document missing on disk: {documentId} (path '{doc.StoredPath}').");
            return null;
        }

        return (stream, doc.FileName, doc.ContentType);
    }

    public async Task DeleteAsync(Guid documentId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);
        if (doc is null) return;

        await storage.DeleteAsync(doc.StoredPath);

        doc.IsDeleted = true;
        await db.SaveChangesAsync();
    }

    /// <summary>The owner's company, which also proves the owner exists and is
    /// visible to the caller — the tenant query filter means another company's
    /// vehicle simply isn't found.</summary>
    private static async Task<Guid?> ResolveOwnerCompanyAsync(AppDbContext db, DocumentOwnerKind kind, Guid ownerId) =>
        kind == DocumentOwnerKind.Driver
            ? await db.Drivers.Where(d => d.Id == ownerId && !d.IsDeleted).Select(d => (Guid?)d.CompanyId).FirstOrDefaultAsync()
            : await db.Vehicles.Where(v => v.Id == ownerId && !v.IsDeleted).Select(v => (Guid?)v.CompanyId).FirstOrDefaultAsync();
}
