using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>
/// One bill covering every trip run for a party over a period — the invoice a
/// transport office sends at month end, rather than the per-trip bill.
///
/// Deliberately built on the same <see cref="PdfHelpers.CompanyHeader"/> as the
/// LR and the per-trip bill: a customer receiving both should see the same
/// letterhead and the same centred title, not two documents that look like
/// they came from different companies.
///
/// The extras and GST columns appear only when something in the period
/// actually carried them, so a party that pays plain freight gets a plain
/// four-column bill rather than a wall of zeroes.
/// </summary>
public static class PartyBillDocument
{
    public static byte[] Build(PartyReport report, Company company) =>
        Document.Create(container => PdfHelpers.Page(container, page =>
        {
            page.Size(QuestPDF.Helpers.PageSizes.A4);

            page.Content().Column(col =>
            {
                col.Item().Element(c => PdfHelpers.CompanyHeader(c, company, "BILL", includeLogo: true));

                if (!string.IsNullOrWhiteSpace(company.JurisdictionNote))
                    col.Item().PaddingTop(4).AlignCenter().Text(company.JurisdictionNote)
                        .FontSize(7.5f).FontColor(PdfHelpers.Muted);

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Element(c => PdfHelpers.LabelValue(c, "M/s", report.PartyName, 10));
                    row.ConstantItem(160).Element(c => PdfHelpers.LabelValue(c, "Period", report.PeriodLabel));
                });

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(24);                 // S.No
                        c.RelativeColumn(1.2f);               // Date
                        c.RelativeColumn(1.1f);               // LR No
                        c.RelativeColumn(1.3f);               // Vehicle
                        c.RelativeColumn(2.2f);               // Route
                        c.RelativeColumn(1f);                 // Weight
                        c.RelativeColumn(1f);                 // Rate
                        c.RelativeColumn(1.2f);               // Freight

                        if (report.HasExtras) c.RelativeColumn(1.2f);   // Extras
                        if (report.HasGst) c.RelativeColumn(1.1f);      // GST

                        c.RelativeColumn(1.3f);               // Total
                    });

                    table.Header(h =>
                    {
                        void Head(string text, bool right = false)
                        {
                            var cell = h.Cell().BorderBottom(0.75f).BorderColor(PdfHelpers.Line).PaddingBottom(4).PaddingRight(2);
                            (right ? cell.AlignRight() : cell).Text(text).FontSize(7.5f).SemiBold().FontColor(PdfHelpers.Muted);
                        }

                        Head("#");
                        Head("DATE");
                        Head("LR NO");
                        Head("VEHICLE");
                        Head("FROM — TO");
                        Head("WEIGHT", right: true);
                        Head("RATE", right: true);
                        Head("FREIGHT", right: true);
                        if (report.HasExtras) Head("EXTRAS", right: true);
                        if (report.HasGst) Head("GST", right: true);
                        Head("TOTAL", right: true);
                    });

                    foreach (var r in report.Rows)
                    {
                        void Cell(string text, bool right = false)
                        {
                            var cell = table.Cell().PaddingVertical(2).PaddingRight(2);
                            (right ? cell.AlignRight() : cell).Text(text).FontSize(8);
                        }

                        Cell(r.SerialNo.ToString());
                        Cell(r.Date.ToString("dd-MMM-yy"));
                        Cell(string.IsNullOrWhiteSpace(r.LrNo) ? "—" : r.LrNo);
                        Cell(r.VehicleRegNo);
                        Cell($"{r.FromCity} — {r.ToCity}");
                        Cell(r.Weight is { } w ? $"{w:N3}" : "—", right: true);
                        Cell(r.Rate is { } rate ? $"{rate:N2}" : "—", right: true);
                        Cell($"{r.Amount:N2}", right: true);
                        if (report.HasExtras) Cell(r.TotalExtras > 0 ? $"{r.TotalExtras:N2}" : "—", right: true);
                        if (report.HasGst) Cell(r.GstAmount > 0 ? $"{r.GstAmount:N2}" : "—", right: true);
                        Cell($"{r.GrandTotal:N2}", right: true);
                    }

                    // The foot repeats each column's own total, so the bill can
                    // be checked down a column rather than only across a row.
                    void Foot(string text, bool right = false, bool bold = true)
                    {
                        var cell = table.Cell().BorderTop(0.75f).BorderColor(PdfHelpers.Line).PaddingTop(4).PaddingRight(2);
                        var styled = (right ? cell.AlignRight() : cell).Text(text).FontSize(8.5f);
                        if (bold) styled.Bold();
                    }

                    Foot("");
                    Foot("");
                    Foot("");
                    Foot("");
                    Foot($"{report.Rows.Count} trip{(report.Rows.Count == 1 ? "" : "s")}");
                    Foot("", right: true);
                    Foot("TOTAL", right: true);
                    Foot($"{report.Total:N2}", right: true);
                    if (report.HasExtras) Foot($"{report.TotalExtras:N2}", right: true);
                    if (report.HasGst) Foot($"{report.TotalGst:N2}", right: true);
                    Foot($"{report.GrandTotal:N2}", right: true);
                });

                // The breakdown, spelled out beneath the table — the same
                // freight → extras → tax → total the per-trip bill shows, so
                // the two reconcile line for line.
                if (report.HasExtras || report.HasGst)
                {
                    col.Item().PaddingTop(10).AlignRight().Width(240).Table(summary =>
                    {
                        summary.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });

                        void Line(string label, decimal value, bool bold = false)
                        {
                            var l = summary.Cell().PaddingVertical(1).Text(label).FontSize(8.5f);
                            var v = summary.Cell().PaddingVertical(1).AlignRight().Text($"{value:N2}").FontSize(8.5f);
                            if (bold) { l.Bold(); v.Bold(); }
                        }

                        Line("Freight", report.Total);
                        if (report.TotalWayment > 0) Line("Wayment", report.TotalWayment);
                        if (report.TotalLoading > 0) Line("Loading", report.TotalLoading);
                        if (report.TotalUnloading > 0) Line("Unloading", report.TotalUnloading);
                        if (report.HasExtras) Line("Total before tax", report.TotalBeforeTax, bold: true);
                        if (report.HasGst) Line("GST", report.TotalGst);
                        Line("Grand total", report.GrandTotal, bold: true);
                    });
                }

                col.Item().PaddingTop(8)
                    .Text($"Rupees in words: {NumberToWords.ToRupees(report.GrandTotal)}").FontSize(8.5f);

                if (company.CanPrintBankDetails)
                {
                    col.Item().PaddingTop(8).Border(0.5f).BorderColor(PdfHelpers.Line).Padding(5).Column(bank =>
                    {
                        bank.Item().Text("BANK DETAILS").FontSize(7.5f).SemiBold().FontColor(PdfHelpers.Muted);

                        if (!string.IsNullOrWhiteSpace(company.BankAccountNo))
                            bank.Item().Text($"A/c {company.BankAccountNo}").FontSize(8.5f);

                        if (!string.IsNullOrWhiteSpace(company.Ifsc))
                            bank.Item().Text($"IFSC {company.Ifsc}").FontSize(8.5f);
                    });
                }

                col.Item().PaddingTop(30).AlignRight()
                    .Text($"For {(string.IsNullOrWhiteSpace(company.CompanyName) ? "LorryOwner" : company.CompanyName)}")
                    .FontSize(8.5f);
            });

            page.Footer().AlignCenter().Text(t => t.CurrentPageNumber().FontSize(8));
        })).GeneratePdf();
}
