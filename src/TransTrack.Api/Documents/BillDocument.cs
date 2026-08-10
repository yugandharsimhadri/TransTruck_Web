using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>The Cash Bill handed to the party — QuestPDF port of the WPF
/// app's BillDocument, same layout as the paper bill book (S.No, M/s,
/// particulars/qty/rate/amount, total, signature). Carries the company logo
/// (unlike the LR, which prints on pre-printed stationery).</summary>
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

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1); c.RelativeColumn(5.5f); c.RelativeColumn(1.5f); c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        foreach (var text in new[] { "S.No", "PARTICULARS", "QTY", "AMOUNT" })
                            h.Cell().BorderBottom(0.5f).BorderColor(PdfHelpers.Line).PaddingBottom(4).Text(text).FontSize(8).SemiBold().FontColor(PdfHelpers.Muted);
                    });

                    var particulars = $"Freight — {trip.Vehicle.RegNo} — {trip.FromCity.Display} to {trip.ToCity.Display} (Trip {trip.TripNo})";
                    AddRow(table, "1", particulars, "1", $"{trip.Amount:N2}");

                    var index = 2;
                    foreach (var expense in trip.Expenses.OrderBy(e => e.Date))
                    {
                        var detail = $"{expense.ExpenseCategory.Name}{(string.IsNullOrWhiteSpace(expense.Remarks) ? "" : $" — {expense.Remarks}")}";
                        AddRow(table, $"{index++}", detail, "1", $"{expense.Amount:N2}");
                    }

                    // One trailing blank row, same as the paper bill book leaves
                    // under its last written line before the ruled table ends.
                    AddRow(table, "", "", "", "");
                });

                var total = trip.Amount + trip.TotalExpenses;
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem(4).Text("TOTAL").FontSize(10).Bold();
                    row.RelativeItem().AlignRight().Text($"{total:N2}").FontSize(10).Bold();
                });

                col.Item().PaddingTop(6).PaddingBottom(40).Text($"Rupees in words: {NumberToWords.ToRupees(total)}").FontSize(8.5f);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Customer's sign: ____________________").FontSize(8.5f);
                    row.RelativeItem().AlignRight().Text($"For {(string.IsNullOrWhiteSpace(company.CompanyName) ? "TransTrack" : company.CompanyName)}").FontSize(8.5f);
                });
            })));

        return document.GeneratePdf();
    }

    private static void AddRow(TableDescriptor table, string sNo, string particulars, string qty, string amount)
    {
        table.Cell().PaddingVertical(3).Text(sNo).FontSize(9);
        table.Cell().PaddingVertical(3).Text(particulars).FontSize(9);
        table.Cell().PaddingVertical(3).AlignRight().Text(qty).FontSize(9);
        table.Cell().PaddingVertical(3).AlignRight().Text(amount).FontSize(9);
    }
}
