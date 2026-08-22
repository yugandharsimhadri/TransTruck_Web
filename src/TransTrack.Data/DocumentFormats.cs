namespace TransTrack.Data;

/// <summary>
/// What may be uploaded as a document: a PDF, or an ordinary photo/scan.
///
/// The check is on the file's actual leading bytes, not on the content type
/// or the extension, because both of those are simply whatever the client
/// chose to send. That matters more here than for a typical upload: these
/// files are served back later with the content type recorded at upload, so
/// accepting an HTML file labelled "application/pdf" would hand every
/// subsequent viewer a stored-script problem on the app's own origin.
///
/// The sniffed type is what gets stored, so the record describes the file
/// rather than the claim made about it.
/// </summary>
public static class DocumentFormats
{
    /// <summary>Enough bytes for every signature below; HEIC's brand sits at
    /// offset 8.</summary>
    public const int HeaderBytes = 16;

    public static readonly string Accepted = "PDF, JPG, PNG, WebP or HEIC";

    /// <summary>The content type implied by the file's own bytes, or null when
    /// it is not one of the accepted formats.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (Starts(header, "%PDF"u8)) return "application/pdf";

        // JPEG: FF D8 FF, any variant.
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (Starts(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";

        // RIFF....WEBP — the four size bytes in between are skipped.
        if (Starts(header, "RIFF"u8) && header.Length >= 12 && header[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";

        // ISO-BMFF: "ftyp" at offset 4, then the brand. Covers what an iPhone
        // produces when it hands over the original rather than a converted JPEG.
        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = header[8..12];
            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) ||
                brand.SequenceEqual("hevc"u8) || brand.SequenceEqual("heim"u8) ||
                brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
                return "image/heic";
        }

        return null;
    }

    private static bool Starts(ReadOnlySpan<byte> header, ReadOnlySpan<byte> signature) =>
        header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);
}
