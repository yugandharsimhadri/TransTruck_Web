using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>Shared helpers so the LR and Bill QuestPDF templates stay
/// readable — the QuestPDF port of the WPF app's FlowDocument-based
/// DocumentBuilder, same visual layout, different rendering API.</summary>
internal static class PdfHelpers
{
    public static readonly string Muted = Colors.Grey.Darken1;
    public static readonly string Line = Colors.Grey.Darken2;

    public static void Page(IDocumentContainer container, Action<PageDescriptor> build) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9));
            build(page);
        });

    /// <summary>The boxed company letterhead every printed document opens
    /// with. <paramref name="copyLabel"/> prints as a small tag under the
    /// title (e.g. "VEHICLE COPY") — the same role the carbon-copy book's
    /// coloured layer plays on the paper stationery this replaces.</summary>
    public static void CompanyHeader(IContainer container, Company company, string documentTitle, bool includeLogo, string? copyLabel = null)
    {
        container.Border(1).BorderColor(Line).Padding(8).Column(col =>
        {
            void Details(ColumnDescriptor c)
            {
                var name = string.IsNullOrWhiteSpace(company.CompanyName) ? "TransTrack" : company.CompanyName;
                c.Item().AlignCenter().Text(name).FontSize(16).Bold();

                if (!string.IsNullOrWhiteSpace(company.Tagline))
                    c.Item().AlignCenter().Text(company.Tagline).FontSize(9.5f).SemiBold().FontColor(Muted);

                if (!string.IsNullOrWhiteSpace(company.AddressLine))
                    c.Item().AlignCenter().Text(company.AddressLine).FontSize(8).FontColor(Muted);

                var contact = string.Join("   |   ", new[] { company.Phone, company.Cell }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (contact.Length > 0)
                    c.Item().AlignCenter().Text($"Ph {contact}").FontSize(8).FontColor(Muted);

                var registration = string.Join("   |   ", new[]
                {
                    string.IsNullOrWhiteSpace(company.Pan) ? null : $"PAN {company.Pan}",
                    string.IsNullOrWhiteSpace(company.Gstin) ? null : $"GSTIN {company.Gstin}"
                }.Where(s => s is not null));
                if (registration.Length > 0)
                    c.Item().AlignCenter().Text(registration).FontSize(8).FontColor(Muted);

                c.Item().PaddingTop(4).AlignCenter().Text(documentTitle).FontSize(9).Bold();

                if (!string.IsNullOrWhiteSpace(copyLabel))
                    c.Item().PaddingTop(1).AlignCenter().Text(copyLabel.ToUpperInvariant()).FontSize(8).Bold().FontColor(Muted);
            }

            if (includeLogo && company.HasLogo)
            {
                col.Item().Row(row =>
                {
                    try
                    {
                        row.ConstantItem(140).Height(75).Image(Convert.FromBase64String(company.LogoBase64!)).FitArea();
                    }
                    catch
                    {
                        // A logo that fails to decode must never stop the document rendering.
                        row.ConstantItem(140);
                    }

                    row.RelativeItem().Column(Details);
                });
            }
            else
            {
                col.Item().Column(Details);
            }
        });
    }

    /// <summary>A boxed label/value cell for a grid layout — label small and
    /// muted above, value normal-weight below.</summary>
    public static void LabelledCell(IContainer container, string label, string value)
    {
        container.Border(0.5f).BorderColor(Line).Padding(5).Column(col =>
        {
            col.Item().Text(label).FontSize(7.5f).SemiBold().FontColor(Muted);
            col.Item().PaddingTop(1).Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(9.5f);
        });
    }

    /// <summary>A single "Label: value" line, bold label and ordinary weight value.</summary>
    public static void LabelValue(IContainer container, string label, string value, float size = 9)
    {
        container.Text(text =>
        {
            text.Span($"{label}: ").FontSize(size).Bold();
            text.Span(value).FontSize(size);
        });
    }
}
