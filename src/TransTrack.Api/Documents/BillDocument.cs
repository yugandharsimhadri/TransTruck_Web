using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>The Cash Bill handed to the party — the freight invoice, not a
/// running account of the trip. It shows only what the party is being billed
/// for (weight, rate, the freight this works out to) and where that stands
/// (received so far, balance due) — trip expenses are the company's own
/// operating cost, not something the party is charged for, so they never
/// belonged on this document and are deliberately left off. Carries the
/// company logo (unlike the LR, which prints on pre-printed stationery).</summary>
public static class BillDocument
{
    public static byte[] Build(Trip trip, Company company, bool isReprint)
    {
        var document = Document.Create(container =>
            PdfHelpers.Page(container, page => page.Content().Column(col =>
            {
                col.Item().Element(c => PdfHelpers.CompanyHeader(c, company,
                    isReprint ? "CASH BILL (DUPLICATE)" : "CASH BILL", includeLogo: true));

                col.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Element(c => PdfHelpers.LabelValue(c, "S.No.", string.IsNullOrWhiteSpace(trip.BillNo) ? "—" : trip.BillNo));
                    row.RelativeItem().Element(c => PdfHelpers.LabelValue(c, "Date", trip.Date.ToString("dd-MMM-yyyy")));
                });

                col.Item().PaddingTop(4).Element(c => PdfHelpers.LabelValue(c, "M/s", trip.Party.Name, 10));

                col.Item().PaddingTop(2).Text($"Vehicle {trip.Vehicle.RegNo} — {trip.FromCity.Display} to {trip.ToCity.Display} (Trip {trip.TripNo})")
                    .FontSize(8.5f).FontColor(PdfHelpers.Muted);

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(6); c.RelativeColumn(3); });

                    table.Header(h =>
                    {
                        foreach (var text in new[] { "DESCRIPTION", "AMOUNT" })
                            h.Cell().BorderBottom(0.5f).BorderColor(PdfHelpers.Line).PaddingBottom(4).Text(text).FontSize(8).SemiBold().FontColor(PdfHelpers.Muted);
                    });

                    AddRow(table, "Weight", trip.Weight is { } w ? $"{w:N3} MT" : "—");
                    AddRow(table, "Rate per MT", trip.Rate is { } r ? $"{r:N2}" : "—");
                    AddRow(table, "Freight Amount", $"{trip.Amount:N2}", bold: true);
                    AddRow(table, "Amount Received", $"{trip.TotalApprovedReceived:N2}");
                    AddRow(table, "Balance Due", $"{trip.BalanceReceivable:N2}", bold: true);
                });

                col.Item().PaddingTop(6)
                    .Text($"Rupees in words: {NumberToWords.ToRupees(trip.Amount)}").FontSize(8.5f);

                // Opt-in per company, and only when actually filled in — see
                // Company.CanPrintBankDetails. A company that hasn't asked for
                // this never has its account number appear on a document.
                if (company.CanPrintBankDetails)
                {
                    col.Item().PaddingTop(8).Border(0.5f).BorderColor(PdfHelpers.Line).Padding(5).Column(bank =>
                    {
                        bank.Item().Text("BANK DETAILS").FontSize(7.5f).SemiBold().FontColor(PdfHelpers.Muted);

                        if (!string.IsNullOrWhiteSpace(company.BankAccountNo))
                            bank.Item().PaddingTop(1).Element(c => PdfHelpers.LabelValue(c, "A/C No.", company.BankAccountNo, 8.5f));

                        if (!string.IsNullOrWhiteSpace(company.Ifsc))
                            bank.Item().Element(c => PdfHelpers.LabelValue(c, "IFSC", company.Ifsc, 8.5f));
                    });
                }

                col.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().Text("Customer's sign: ____________________").FontSize(8.5f);
                    row.RelativeItem().AlignRight().Text($"For {(string.IsNullOrWhiteSpace(company.CompanyName) ? "TransTrack" : company.CompanyName)}").FontSize(8.5f);
                });
            })));

        return document.GeneratePdf();
    }

    private static void AddRow(TableDescriptor table, string label, string amount, bool bold = false)
    {
        if (bold)
        {
            table.Cell().PaddingVertical(4).Text(label).FontSize(9.5f).Bold();
            table.Cell().PaddingVertical(4).AlignRight().Text(amount).FontSize(9.5f).Bold();
        }
        else
        {
            table.Cell().PaddingVertical(4).Text(label).FontSize(9.5f);
            table.Cell().PaddingVertical(4).AlignRight().Text(amount).FontSize(9.5f);
        }
    }
}
