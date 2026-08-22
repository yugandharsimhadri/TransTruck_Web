using System.Text;
using Microsoft.EntityFrameworkCore;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Tests;

/// <summary>
/// Documents held against vehicles and drivers. These used to be one per
/// vehicle with no type at all; the rules worth pinning down now are that an
/// owner can hold several, that each carries a type, and that a type can't be
/// filed against the wrong kind of owner.
/// </summary>
[Collection(ProcessStateCollection.Name)]
public class DocumentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lo-docs-{Guid.NewGuid():N}");
    private readonly string? _original =
        Environment.GetEnvironmentVariable(FileSystemDocumentStorage.DirectoryOverrideVariable);

    public DocumentTests()
    {
        // Never write into the real C:\TransTruckWeb\VehicleDocs from a test.
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(FileSystemDocumentStorage.DirectoryOverrideVariable, _dir);
    }

    private static DocumentService ServiceFor(TestWorld world) =>
        new(world.Factory, new FileSystemDocumentStorage());

    /// <summary>A file that passes the format check: a real PDF signature
    /// followed by whatever payload the test wants to identify it by.</summary>
    private static Stream FileOf(string content = "pretend this is a scan") =>
        new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 " + content));

    private static Stream BytesOf(params byte[] bytes) => new MemoryStream(bytes);

    [Fact]
    public async Task A_vehicle_can_hold_several_documents_each_with_its_own_type()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        foreach (var type in new[] { DocumentType.Permit, DocumentType.Insurance, DocumentType.Pollution })
        {
            using var content = FileOf();
            await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, type,
                $"{type}.pdf", "application/pdf", content, content.Length);
        }

        var listed = await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId);

        Assert.Equal(3, listed.Count);
        Assert.Contains(listed, d => d.DocumentType == DocumentType.Permit);
        Assert.Contains(listed, d => d.DocumentType == DocumentType.Insurance);
        Assert.Contains(listed, d => d.DocumentType == DocumentType.Pollution);
    }

    [Fact]
    public async Task A_driver_holds_its_own_documents_separately_from_a_vehicle()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using (var content = FileOf())
            await documents.AddAsync(DocumentOwnerKind.Driver, world.DriverId, DocumentType.AadhaarCard,
                "aadhaar.pdf", "application/pdf", content, content.Length);

        using (var content = FileOf())
            await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Permit,
                "permit.pdf", "application/pdf", content, content.Length);

        var driverDocs = await documents.ListAsync(DocumentOwnerKind.Driver, world.DriverId);
        var vehicleDocs = await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId);

        Assert.Equal(DocumentType.AadhaarCard, Assert.Single(driverDocs).DocumentType);
        Assert.Equal(DocumentType.Permit, Assert.Single(vehicleDocs).DocumentType);
    }

    [Theory]
    [InlineData(DocumentOwnerKind.Driver, DocumentType.Insurance)]
    [InlineData(DocumentOwnerKind.Driver, DocumentType.Fitness)]
    [InlineData(DocumentOwnerKind.Vehicle, DocumentType.AadhaarCard)]
    [InlineData(DocumentOwnerKind.Vehicle, DocumentType.DriverLicence)]
    public async Task A_type_cannot_be_filed_against_the_wrong_kind_of_owner(
        DocumentOwnerKind kind, DocumentType type)
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);
        var ownerId = kind == DocumentOwnerKind.Driver ? world.DriverId : world.VehicleId;

        using var content = FileOf();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => documents.AddAsync(kind, ownerId, type, "x.pdf", "application/pdf", content, content.Length));

        Assert.Contains("not a document type", error.Message);
    }

    [Fact]
    public async Task Others_is_accepted_for_both_kinds()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using (var content = FileOf())
            await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Others,
                "misc.pdf", "application/pdf", content, content.Length);

        using (var content = FileOf())
            await documents.AddAsync(DocumentOwnerKind.Driver, world.DriverId, DocumentType.Others,
                "misc.pdf", "application/pdf", content, content.Length);

        Assert.Single(await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId));
        Assert.Single(await documents.ListAsync(DocumentOwnerKind.Driver, world.DriverId));
    }

    /// <summary>Uploading a second document must not overwrite the first on
    /// disk — they used to share a filename derived from the owner.</summary>
    [Fact]
    public async Task Two_documents_for_one_owner_keep_separate_files()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using (var a = FileOf("permit contents"))
            await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Permit,
                "same-name.pdf", "application/pdf", a, a.Length);

        using (var b = FileOf("insurance contents"))
            await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Insurance,
                "same-name.pdf", "application/pdf", b, b.Length);

        var listed = await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId);
        Assert.Equal(2, listed.Count);

        foreach (var doc in listed)
        {
            var opened = await documents.OpenAsync(doc.Id);
            Assert.NotNull(opened);

            using var reader = new StreamReader(opened!.Value.Content);
            var text = await reader.ReadToEndAsync();

            // The full round trip, header included: proves the bytes consumed
            // for format detection were put back rather than lost.
            Assert.Equal(
                doc.DocumentType == DocumentType.Permit
                    ? "%PDF-1.7 permit contents"
                    : "%PDF-1.7 insurance contents",
                text);
        }
    }

    [Fact]
    public async Task Removing_one_document_leaves_the_others()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using (var a = FileOf()) await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId,
            DocumentType.Permit, "p.pdf", "application/pdf", a, a.Length);
        using (var b = FileOf()) await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId,
            DocumentType.Fitness, "f.pdf", "application/pdf", b, b.Length);

        var listed = await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId);
        await documents.DeleteAsync(listed.First(d => d.DocumentType == DocumentType.Permit).Id);

        var remaining = await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId);
        Assert.Equal(DocumentType.Fitness, Assert.Single(remaining).DocumentType);
    }

    /// <summary>A row whose file has gone missing reads as "nothing here"
    /// rather than throwing — a restore that skipped the documents folder
    /// should not break the screen.</summary>
    [Fact]
    public async Task A_document_whose_file_vanished_opens_as_null()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using (var content = FileOf())
            await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Permit,
                "p.pdf", "application/pdf", content, content.Length);

        var doc = Assert.Single(await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId));

        foreach (var file in Directory.GetFiles(_dir, "*", SearchOption.AllDirectories)) File.Delete(file);

        Assert.Null(await documents.OpenAsync(doc.Id));
    }

    [Fact]
    public async Task A_document_cannot_be_attached_to_a_vehicle_that_does_not_exist()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using var content = FileOf();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => documents.AddAsync(DocumentOwnerKind.Vehicle, Guid.NewGuid(), DocumentType.Permit,
                "p.pdf", "application/pdf", content, content.Length));

        Assert.Contains("not found", error.Message);
    }

    // ── Accepted formats ──────────────────────────────────────────────────

    public static TheoryData<string, byte[]> AcceptedFiles => new()
    {
        { "application/pdf", "%PDF-1.7 body"u8.ToArray() },
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0] },
        { "image/webp", "RIFF....WEBPVP8 "u8.ToArray() },
        { "image/heic", "....ftypheic...."u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(AcceptedFiles))]
    public async Task A_pdf_or_ordinary_image_is_accepted_and_typed_by_its_bytes(
        string expectedContentType, byte[] bytes)
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using var content = BytesOf(bytes);
        await documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Permit,
            "scan.bin", "application/octet-stream", content, content.Length);

        var doc = Assert.Single(await documents.ListAsync(DocumentOwnerKind.Vehicle, world.VehicleId));

        // Typed from the file itself, not from the "application/octet-stream"
        // the caller claimed.
        Assert.Equal(expectedContentType, doc.ContentType);
    }

    [Theory]
    [InlineData("MZ ")]                 // Windows executable
    [InlineData("<html><script>alert(1)</script>")] // HTML, the stored-XSS case
    [InlineData("PK")]                 // zip/docx
    [InlineData("plain text, no signature")]
    public async Task Anything_else_is_refused(string payload)
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => documents.AddAsync(DocumentOwnerKind.Vehicle, world.VehicleId, DocumentType.Permit,
                "payload.pdf", "application/pdf", content, content.Length));

        Assert.Contains("not supported", error.Message);
    }

    /// <summary>The reason the check reads bytes rather than headers: the
    /// filename and content type are both whatever the client chose to send,
    /// and this file is served back later with the type recorded here.</summary>
    [Fact]
    public async Task An_html_file_claiming_to_be_a_pdf_is_refused()
    {
        await using var world = await TestWorld.CreateAsync();
        var documents = ServiceFor(world);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("<html><body>not a pdf</body></html>"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => documents.AddAsync(DocumentOwnerKind.Driver, world.DriverId, DocumentType.AadhaarCard,
                "aadhaar.pdf", "application/pdf", content, content.Length));

        Assert.Contains("not supported", error.Message);
        Assert.Empty(await documents.ListAsync(DocumentOwnerKind.Driver, world.DriverId));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(FileSystemDocumentStorage.DirectoryOverrideVariable, _original);
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
