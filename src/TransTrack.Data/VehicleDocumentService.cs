using Microsoft.EntityFrameworkCore;
using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>What a caller needs to know about a vehicle's document without
/// fetching the bytes — drives the "one document, uploaded on such a date"
/// line on the vehicle form.</summary>
public record VehicleDocumentInfo(Guid VehicleId, string FileName, string ContentType, long SizeBytes, DateTime UploadedOn);

/// <summary>The one document held per vehicle. Kept apart from
/// <see cref="VehicleService"/> deliberately: nothing on the vehicle list,
/// trip or dashboard path ever calls in here, so a multi-megabyte upload can
/// never end up riding along with a routine query.</summary>
public class VehicleDocumentService(IDbContextFactory<AppDbContext> factory, IVehicleDocumentStorage storage)
{
    public long MaxBytes => Math.Max(1, AppConfig.Current.VehicleDocumentMaxMb) * 1024L * 1024L;

    public async Task<VehicleDocumentInfo?> GetInfoAsync(Guid vehicleId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.VehicleDocuments.AsNoTracking()
            .Where(d => d.VehicleId == vehicleId && !d.IsDeleted)
            .Select(d => new VehicleDocumentInfo(d.VehicleId, d.FileName, d.ContentType, d.SizeBytes, d.CreatedAt))
            .FirstOrDefaultAsync();
    }

    /// <summary>Saves (or replaces) a vehicle's document. Writes the bytes
    /// first and the row second: a file with no row is invisible clutter, but
    /// a row with no file is a broken download, so the ordering fails on the
    /// harmless side.</summary>
    public async Task SaveAsync(Guid vehicleId, string fileName, string contentType, Stream content, long sizeBytes)
    {
        if (sizeBytes <= 0) throw new InvalidOperationException("That file is empty.");
        if (sizeBytes > MaxBytes)
            throw new InvalidOperationException($"That file is larger than the {AppConfig.Current.VehicleDocumentMaxMb} MB limit.");

        await using var db = await factory.CreateDbContextAsync();

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && !v.IsDeleted)
                      ?? throw new InvalidOperationException("Vehicle not found.");

        var storedPath = await storage.SaveAsync(vehicle.CompanyId, vehicleId, fileName, content);

        var entity = await db.VehicleDocuments.FirstOrDefaultAsync(d => d.VehicleId == vehicleId);
        var isNew = entity is null;
        entity ??= new VehicleDocument { VehicleId = vehicleId };

        entity.FileName = Path.GetFileName(fileName);
        entity.ContentType = contentType;
        entity.SizeBytes = sizeBytes;
        entity.StoredPath = storedPath;
        entity.IsDeleted = false;

        if (isNew) db.VehicleDocuments.Add(entity);
        await db.SaveChangesAsync();
    }

    /// <summary>Opens a vehicle's document, or null when there isn't one —
    /// covering both "never uploaded" and "row exists but the file is gone".
    /// Both are answered the same calm way by the caller.</summary>
    public async Task<(Stream Content, string FileName, string ContentType)?> OpenAsync(Guid vehicleId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var doc = await db.VehicleDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.VehicleId == vehicleId && !d.IsDeleted);
        if (doc is null) return null;

        var stream = await storage.OpenAsync(doc.StoredPath);
        if (stream is null)
        {
            // The row outlived its file — a restore that missed the documents
            // folder, or a hand-cleaned disk. Report it as "nothing here" and
            // let the user re-upload, rather than failing the request.
            AppLog.Info($"Vehicle document missing on disk for vehicle {vehicleId} (path '{doc.StoredPath}').");
            return null;
        }

        return (stream, doc.FileName, doc.ContentType);
    }

    public async Task DeleteAsync(Guid vehicleId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var doc = await db.VehicleDocuments.FirstOrDefaultAsync(d => d.VehicleId == vehicleId && !d.IsDeleted);
        if (doc is null) return;

        await storage.DeleteAsync(doc.StoredPath);

        doc.IsDeleted = true;
        await db.SaveChangesAsync();
    }
}
