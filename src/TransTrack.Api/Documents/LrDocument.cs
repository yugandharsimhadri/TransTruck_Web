using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>The Lorry Receipt / Way Bill — QuestPDF port of the WPF app's
/// FlowDocument-based LrDocument, same layout (Consignor/Consignee, vehicle,
/// route, freight breakdown, balance to pay, terms, signature). Prints as
/// three copies — Vehicle, Consignee, Owner — one per page of the same PDF,
/// mirroring the carbon-copy book handed out per trip. Carries the company
/// logo: this now prints on plain paper rather than the pre-printed
/// stationery it was originally designed against, so the letterhead has to
/// come from the document itself.</summary>
public static class LrDocument
{
    private static readonly string[] CopyLabels = ["Vehicle Copy", "Consignee Copy", "Owner Copy"];

    public static byte[] Build(Trip trip, Company company, bool isReprint)
    {
        var document = Document.Create(container =>
        {
            foreach (var copyLabel in CopyLabels)
                PdfHelpers.Page(container, page => BuildCopyPage(page, trip, company, isReprint, copyLabel));
        });

        return document.GeneratePdf();
    }

    private static void BuildCopyPage(PageDescriptor page, Trip trip, Company company, bool isReprint, string copyLabel)
    {
        page.Content().Column(col =>
        {
            col.Item().Element(c => PdfHelpers.CompanyHeader(c, company,
                isReprint ? "LORRY RECEIPT (DUPLICATE)" : "LORRY RECEIPT", includeLogo: true, copyLabel: copyLabel));

            if (!string.IsNullOrWhiteSpace(company.JurisdictionNote))
                col.Item().PaddingTop(4).AlignCenter().Text(company.JurisdictionNote).FontSize(7.5f).FontColor(PdfHelpers.Muted);

            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Element(c => PdfHelpers.LabelValue(c, "LR No.", string.IsNullOrWhiteSpace(trip.LrNo) ? "—" : trip.LrNo));

                // Only when one was actually entered — an absent way bill
                // number prints nothing at all rather than an empty label.
                if (!string.IsNullOrWhiteSpace(trip.WayBillNo))
                    row.RelativeItem().Element(c => PdfHelpers.LabelValue(c, "Way Bill No.", trip.WayBillNo));

                row.RelativeItem().Element(c => PdfHelpers.LabelValue(c, "Date", trip.Date.ToString("dd-MMM-yyyy")));
            });

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "CONSIGNOR (From)", Multiline(trip.ConsignorName, trip.ConsignorAddress)));
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "CONSIGNEE (To)", Multiline(trip.ConsigneeName, trip.ConsigneeAddress)));
            });

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "VEHICLE NO.", trip.Vehicle.RegNo));
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "FROM", Multiline(trip.FromCity.Display, trip.FromAddress)));
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "TO", Multiline(trip.ToCity.Display, trip.ToAddress)));
            });

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem(2).Element(c => PdfHelpers.LabelledCell(c, "PARTY", trip.Party.Name));
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "WEIGHT", trip.Weight is { } w ? $"{w:N3} MT" : "—"));
                row.RelativeItem().Element(c => PdfHelpers.LabelledCell(c, "RATE PER MT", trip.Rate is { } r ? $"{r:N2}" : "—"));
            });

            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });
                table.Cell().Text("Freight").FontSize(9);
                table.Cell().AlignRight().Text($"{trip.Amount:N2}").FontSize(9);
                table.Cell().Text("Advance received").FontSize(9);
                table.Cell().AlignRight().Text($"{trip.TotalApprovedReceived:N2}").FontSize(9);
                table.Cell().PaddingTop(2).Text("Balance to pay").FontSize(9).Bold();
                table.Cell().PaddingTop(2).AlignRight().Text($"{trip.BalanceReceivable:N2}").FontSize(9).Bold();
            });

            col.Item().PaddingVertical(6).Text($"Rupees in words: {NumberToWords.ToRupees(trip.Amount)}").FontSize(8.5f);

            if (!string.IsNullOrWhiteSpace(trip.Remarks))
                col.Item().Element(c => PdfHelpers.LabelValue(c, "Remarks", trip.Remarks, 8));

            col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(PdfHelpers.Line);
            col.Item().PaddingTop(4).Text("1. This goods Way Bill is a Manifesto as declared by the Consignor.").FontSize(7.5f).FontColor(PdfHelpers.Muted);
            col.Item().Text("2. Goods Undertaken for transportation basis.").FontSize(7.5f).FontColor(PdfHelpers.Muted);

            col.Item().PaddingTop(40).Row(row =>
            {
                row.RelativeItem().Text("Unloading by consignee at: ____________________").FontSize(8);
                row.RelativeItem().AlignRight().Text($"For {(string.IsNullOrWhiteSpace(company.CompanyName) ? "LorryOwner" : company.CompanyName)}").FontSize(8.5f);
            });
        });
    }

    private static string Multiline(string primary, string? secondary) =>
        string.IsNullOrWhiteSpace(secondary) ? primary : $"{primary}\n{secondary}";
}
