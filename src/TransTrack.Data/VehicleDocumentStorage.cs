using TransTrack.Core;

namespace TransTrack.Data;

/// <summary>
/// Where an uploaded document's bytes actually live. Everything the rest of
/// the app does with a document goes through this interface and a plain string
/// reference, never a file path it built itself — so moving to cloud object
/// storage later is a new implementation of this one interface plus a DI line,
/// with no change to the entity, the service, the controller or the database.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>Stores the bytes and returns the reference to keep on the
    /// record. Each document gets its own name, so uploading a second one for
    /// the same owner never overwrites the first.</summary>
    Task<string> SaveAsync(Guid companyId, DocumentOwnerKind ownerKind, Guid ownerId, Guid documentId,
        string fileName, Stream content, CancellationToken ct = default);

    /// <summary>Opens a stored document, or null when the bytes are missing —
    /// deliberately not an exception: a record whose file was moved, deleted
    /// or not yet migrated is a normal thing to tell the user about calmly,
    /// not a server error.</summary>
    Task<Stream?> OpenAsync(string storedPath, CancellationToken ct = default);

    /// <summary>Removes the bytes. Missing bytes are not an error — the
    /// desired end state is "gone", and it already is.</summary>
    Task DeleteAsync(string storedPath, CancellationToken ct = default);
}

/// <summary>
/// Local disk implementation — the deployment this runs on today. Files are
/// laid out per company so one company's uploads are never interleaved with
/// another's on disk, which keeps a manual copy or restore of a single tenant
/// straightforward.
/// </summary>
public class FileSystemDocumentStorage : IDocumentStorage
{
    /// <summary>Set TRANSTRUCKWEB_VEHICLEDOCS to point tests somewhere
    /// harmless, matching how the database and backup paths are overridden.
    /// Name kept from when this only held vehicle documents — renaming it
    /// would silently ignore the value on any machine already setting it.</summary>
    public const string DirectoryOverrideVariable = "TRANSTRUCKWEB_VEHICLEDOCS";

    public static string RootDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
            if (string.IsNullOrWhiteSpace(configured)) configured = AppConfig.Current.VehicleDocumentDirectory;

            var dir = string.IsNullOrWhiteSpace(configured)
                ? AppPaths.Under("VehicleDocs")
                : configured;

            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public async Task<string> SaveAsync(Guid companyId, DocumentOwnerKind ownerKind, Guid ownerId, Guid documentId,
        string fileName, Stream content, CancellationToken ct = default)
    {
        // company / kind / owner — so one vehicle's papers sit together, and a
        // company's whole set can still be copied as a unit.
        var folder = Path.Combine(RootDirectory, companyId.ToString("N"),
            ownerKind.ToString().ToLowerInvariant(), ownerId.ToString("N"));
        Directory.CreateDirectory(folder);

        // Named by document id, not by the user's filename: several documents
        // now live under one owner, and an awkward or hostile filename can
        // never collide with another or escape the folder.
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10) extension = string.Empty;
        var path = Path.Combine(folder, $"{documentId:N}{extension}");

        await using (var file = File.Create(path))
            await content.CopyToAsync(file, ct);

        // Stored relative to the root so the whole folder can be moved (or the
        // root reconfigured) without every row pointing at the old location.
        return Path.GetRelativePath(RootDirectory, path).Replace('\\', '/');
    }

    public Task<Stream?> OpenAsync(string storedPath, CancellationToken ct = default)
    {
        var full = Resolve(storedPath);
        if (full is null || !File.Exists(full)) return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(full));
    }

    public Task DeleteAsync(string storedPath, CancellationToken ct = default)
    {
        var full = Resolve(storedPath);
        if (full is not null && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    /// <summary>Turns a stored reference back into a full path, refusing
    /// anything that would resolve outside the root — a stored value should
    /// only ever have come from SaveAsync, but a path that escapes the folder
    /// is exactly the thing worth being certain about.</summary>
    private static string? Resolve(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        var root = Path.GetFullPath(RootDirectory);
        var full = Path.GetFullPath(Path.Combine(root, storedPath));

        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
