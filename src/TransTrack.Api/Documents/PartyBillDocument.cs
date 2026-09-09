using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
    public static byte[] Build(PartyReport report, Company company)
    {
        // A bill carrying every addition plus the advance and balance runs to
        // fourteen columns. On A4 portrait that wraps registration numbers and
        // splits figures across lines, so such a bill is turned on its side
        // rather than shrunk: the type stays the size someone can actually read
        // a money column at.
        //
        // Only when it is needed, though. The ordinary bill — plain freight, no
        // advance — stays portrait, matching the LR and the per-trip bill a
        // party receives alongside it.
        var wide = report.HasAdvance
            && (report.HasWayment || report.HasLoading || report.HasUnloading);

        var bodyFont = 8f;
        var headFont = 7f;
        var gutter = 2f;

        return Document.Create(container => PdfHelpers.Page(container, page =>
        {
            page.Size(wide ? PageSizes.A4.Landscape() : PageSizes.A4);

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
                        // The route gives up the most room when space is
                        // short: it wraps onto a second line and stays
                        // readable, whereas a money column that wraps splits a
                        // figure across two lines.
                        c.ConstantColumn(20);                 // S.No
                        c.RelativeColumn(1.15f);              // Date
                        c.RelativeColumn(1.05f);              // LR No
                        c.RelativeColumn(1.5f);               // Vehicle
                        c.RelativeColumn(1.5f);               // Route
                        c.RelativeColumn(0.85f);              // Weight
                        c.RelativeColumn(0.95f);              // Rate
                        c.RelativeColumn(1.2f);               // Freight

                        // Each addition named in its own column rather than
                        // lumped into one "extras" figure: a party querying a
                        // bill asks about loading specifically, and a single
                        // total gives them nothing to check against.
                        if (report.HasWayment) c.RelativeColumn(1.15f);
                        if (report.HasLoading) c.RelativeColumn(1.15f);
                        if (report.HasUnloading) c.RelativeColumn(1.45f);

                        c.RelativeColumn(1.35f);              // Total

                        // Only when something was actually advanced. A party
                        // who always pays on receipt should get a plain bill
                        // rather than a column of zeroes beside a balance that
                        // just repeats the total.
                        if (report.HasAdvance)
                        {
                            c.RelativeColumn(1.25f);          // Advance
                            c.RelativeColumn(1.35f);          // Balance
                        }
                    });

                    table.Header(h =>
                    {
                        void Head(string text, bool right = false)
                        {
                            var cell = h.Cell().BorderBottom(0.75f).BorderColor(PdfHelpers.Line).PaddingBottom(4).PaddingRight(gutter);
                            (right ? cell.AlignRight() : cell).Text(text).FontSize(headFont).SemiBold().FontColor(PdfHelpers.Muted);
                        }

                        Head("#");
                        Head("DATE");
                        Head("LR NO");
                        Head("VEHICLE");
                        Head("FROM — TO");
                        Head("WEIGHT", right: true);
                        Head("RATE", right: true);
                        Head("FREIGHT", right: true);
                        if (report.HasWayment) Head("WAYMENT", right: true);
                        if (report.HasLoading) Head("LOADING", right: true);
                        if (report.HasUnloading) Head("UNLOADING", right: true);
                        Head("TOTAL", right: true);
                        if (report.HasAdvance)
                        {
                            Head("ADVANCE", right: true);
                            Head("BALANCE", right: true);
                        }
                    });

                    foreach (var r in report.Rows)
                    {
                        void Cell(string text, bool right = false)
                        {
                            var cell = table.Cell().PaddingVertical(2).PaddingRight(gutter);
                            (right ? cell.AlignRight() : cell).Text(text).FontSize(bodyFont);
                        }

                        Cell(r.SerialNo.ToString());
                        Cell(r.Date.ToString("dd-MMM-yy"));
                        Cell(string.IsNullOrWhiteSpace(r.LrNo) ? "—" : r.LrNo);
                        Cell(r.VehicleRegNo);
                        Cell($"{r.FromCity} — {r.ToCity}");
                        Cell(r.Weight is { } w ? $"{w:N3}" : "—", right: true);
                        Cell(r.Rate is { } rate ? $"{rate:N2}" : "—", right: true);
                        Cell($"{r.Amount:N2}", right: true);
                        if (report.HasWayment) Cell(r.WaymentCharge > 0 ? $"{r.WaymentCharge:N2}" : "—", right: true);
                        if (report.HasLoading) Cell(r.LoadingCharge > 0 ? $"{r.LoadingCharge:N2}" : "—", right: true);
                        if (report.HasUnloading) Cell(r.UnloadingCharge > 0 ? $"{r.UnloadingCharge:N2}" : "—", right: true);

                        // Before tax: GST is charged once on the bill's total
                        // beneath the table, so adding it per row as well would
                        // make the column sum to more than the bill.
                        Cell($"{r.TotalBeforeTax:N2}", right: true);

                        if (report.HasAdvance)
                        {
                            Cell(r.AdvanceReceived > 0 ? $"{r.AdvanceReceived:N2}" : "—", right: true);
                            Cell($"{r.BalanceDue:N2}", right: true);
                        }
                    }

                    // The foot repeats each column's own total, so the bill can
                    // be checked down a column rather than only across a row.
                    void Foot(string text, bool right = false, bool bold = true)
                    {
                        var cell = table.Cell().BorderTop(0.75f).BorderColor(PdfHelpers.Line).PaddingTop(4).PaddingRight(gutter);
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
                    if (report.HasWayment) Foot($"{report.TotalWayment:N2}", right: true);
                    if (report.HasLoading) Foot($"{report.TotalLoading:N2}", right: true);
                    if (report.HasUnloading) Foot($"{report.TotalUnloading:N2}", right: true);
                    Foot($"{report.TotalBeforeTax:N2}", right: true);
                    if (report.HasAdvance)
                    {
                        Foot($"{report.TotalAdvance:N2}", right: true);
                        Foot($"{report.TotalBeforeTax - report.TotalAdvance:N2}", right: true);
                    }
                });

                // The amount being asked for, spelled out. Tax is charged once
                // on the bill's total so it lives here rather than in the
                // table, and the advance comes off after it — GST is owed on
                // the value of the freight, not on the unpaid portion of it, so
                // deducting first would under-charge the tax.
                //
                // Skipped entirely only when there is nothing to add or take
                // off, in which case the table's own TOTAL is already the
                // amount payable and this block would just repeat it.
                if (report.HasGst || report.HasAdvance)
                {
                    col.Item().PaddingTop(10).AlignRight().Width(260).Table(summary =>
                    {
                        summary.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });

                        void Line(string label, decimal value, bool bold = false)
                        {
                            var l = summary.Cell().PaddingVertical(1).Text(label).FontSize(8.5f);
                            var v = summary.Cell().PaddingVertical(1).AlignRight().Text($"{value:N2}").FontSize(8.5f);
                            if (bold) { l.Bold(); v.Bold(); }
                        }

                        Line("Total before tax", report.TotalBeforeTax);

                        if (report.HasGst)
                        {
                            Line(report.GstLabel, report.TotalGst);
                            Line("Grand total", report.GrandTotal, bold: !report.HasAdvance);
                        }

                        if (report.HasAdvance)
                        {
                            Line("Less advance received", -report.TotalAdvance);
                            Line("Balance payable", report.BalancePayable, bold: true);
                        }
                    });
                }

                // The words name the sum being asked for, not the gross bill.
                // On a payment request those differ the moment an advance has
                // been taken, and the figure a payer reads back is this one.
                col.Item().PaddingTop(8)
                    .Text($"Rupees in words: {NumberToWords.ToRupees(report.BalancePayable)}").FontSize(8.5f);

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
}
