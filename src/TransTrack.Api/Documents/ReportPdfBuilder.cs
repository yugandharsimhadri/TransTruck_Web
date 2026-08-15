using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>Builds a PDF from whatever rows the Reports screen queried —
/// QuestPDF port of the WPF app's ReportPdfBuilder, same three tabs
/// (trips/maintenance/ledger), same totals.</summary>
public static class ReportPdfBuilder
{
    private static void Header(IContainer container, Company company, string reportTitle, string filterSummary)
    {
        container.Row(row =>
        {
            if (company.HasLogo)
            {
                try
                {
                    row.ConstantItem(60).Height(40).Image(Convert.FromBase64String(company.LogoBase64!)).FitArea();
                }
                catch
                {
                    // A logo that fails to decode must never stop the report exporting.
                }
            }

            row.RelativeItem().Column(col =>
            {
                col.Item().Text(string.IsNullOrWhiteSpace(company.CompanyName) ? "TransTrack" : company.CompanyName).FontSize(16).Bold();
                col.Item().Text(reportTitle).FontSize(12).SemiBold();
                col.Item().Text(filterSummary).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    public static byte[] BuildTrips(IReadOnlyList<Trip> trips, Company company, string filterSummary) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Element(c => Header(c, company, "Trips report", filterSummary));

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f); c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.8f); c.RelativeColumn(1.4f); c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                });

                table.Header(h =>
                {
                    foreach (var text in new[] { "Trip No", "Date", "Vehicle", "Driver", "Party", "From", "To", "Amount", "Expenses", "Balance" })
                        h.Cell().BorderBottom(1).PaddingBottom(4).Text(text).SemiBold();
                });

                foreach (var t in trips)
                {
                    table.Cell().Text(t.TripNo);
                    table.Cell().Text(t.Date.ToString("dd-MMM-yy"));
                    table.Cell().Text(t.Vehicle.RegNo);
                    table.Cell().Text(t.Driver.Name);
                    table.Cell().Text(t.Party.Name);
                    table.Cell().Text(t.FromCity.Name);
                    table.Cell().Text(t.ToCity.Name);
                    table.Cell().AlignRight().Text($"{t.Amount:N2}");
                    table.Cell().AlignRight().Text($"{t.TotalExpenses:N2}");
                    table.Cell().AlignRight().Text($"{t.BalanceReceivable:N2}");
                }

                // Company revenue, not raw freight — an other-owner vehicle's
                // trip contributes only its commission to this total.
                table.Cell().ColumnSpan(7).BorderTop(1).PaddingTop(4).Text("Total (company revenue)").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{trips.Sum(t => t.CompanyRevenue):N2}").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{trips.Sum(t => t.TotalExpenses):N2}").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{trips.Sum(t => t.BalanceReceivable):N2}").Bold();
            });

            page.Footer().AlignCenter().Text(t => t.CurrentPageNumber().FontSize(8));
        })).GeneratePdf();

    public static byte[] BuildMaintenance(IReadOnlyList<VehicleMaintenance> records, Company company, string filterSummary) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Element(c => Header(c, company, "Vehicle maintenance report", filterSummary));

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(1.6f); c.RelativeColumn(1.8f);
                    c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f); c.RelativeColumn(2f);
                });

                table.Header(h =>
                {
                    foreach (var text in new[] { "Vehicle", "Date", "Category", "Vendor", "Amount", "Odometer", "Next due", "Remarks" })
                        h.Cell().BorderBottom(1).PaddingBottom(4).Text(text).SemiBold();
                });

                foreach (var m in records)
                {
                    table.Cell().Text(m.Vehicle.RegNo);
                    table.Cell().Text(m.Date.ToString("dd-MMM-yy"));
                    table.Cell().Text(m.MaintenanceCategory.Name);
                    table.Cell().Text(m.VendorName ?? "—");
                    table.Cell().AlignRight().Text($"{m.Amount:N2}");
                    table.Cell().AlignRight().Text(m.OdometerReading is { } o ? $"{o:N0}" : "—");
                    table.Cell().Text(m.NextDueDate?.ToString("dd-MMM-yy") ?? "—");
                    table.Cell().Text(m.Remarks ?? "—");
                }

                table.Cell().ColumnSpan(4).BorderTop(1).PaddingTop(4).Text("Total").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{records.Sum(m => m.Amount):N2}").Bold();
                table.Cell().ColumnSpan(3).BorderTop(1);
            });

            page.Footer().AlignCenter().Text(t => t.CurrentPageNumber().FontSize(8));
        })).GeneratePdf();

    /// <summary>The party's statement for a period, laid out to match the
    /// paper report this replaces: a single centred title carrying the party
    /// name and period, then S No / Date / Vehicle / From / To / Weight /
    /// Rate / Amount, closing on one total. Portrait, not landscape — eight
    /// narrow columns fit a page the customer can file alongside the old
    /// ones.</summary>
    public static byte[] BuildPartyReport(PartyReport report, Company company) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Column(col =>
            {
                col.Item().Element(c => Header(c, company, "Party-wise report", report.PeriodLabel));
                col.Item().PaddingTop(6).AlignCenter()
                    .Text($"{report.PartyName.ToUpperInvariant()} {report.PeriodLabel}")
                    .FontSize(12).Bold();
            });

            page.Content().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(0.7f); c.RelativeColumn(1.4f); c.RelativeColumn(1.8f);
                    c.RelativeColumn(1.8f); c.RelativeColumn(1.8f);
                    c.RelativeColumn(1.2f); c.RelativeColumn(1f); c.RelativeColumn(1.4f);
                });

                table.Header(h =>
                {
                    foreach (var text in new[] { "S NO", "DATE", "VEHICLE NO", "FROM", "TO", "WEIGHT", "RATE", "AMOUNT" })
                        h.Cell().BorderBottom(1).PaddingBottom(4).Text(text).SemiBold().FontSize(8.5f);
                });

                foreach (var r in report.Rows)
                {
                    table.Cell().PaddingVertical(2).Text($"{r.SerialNo}").FontSize(8.5f);
                    table.Cell().PaddingVertical(2).Text(r.Date.ToString("dd/MM/yyyy")).FontSize(8.5f);
                    table.Cell().PaddingVertical(2).Text(r.VehicleRegNo).FontSize(8.5f);
                    table.Cell().PaddingVertical(2).Text(r.FromCity).FontSize(8.5f);
                    table.Cell().PaddingVertical(2).Text(r.ToCity).FontSize(8.5f);
                    // Blank, not a dash, when a trip was billed as a flat
                    // amount — matching how these read on the paper report.
                    table.Cell().PaddingVertical(2).AlignRight().Text(r.Weight is { } w ? $"{w:N2}" : "").FontSize(8.5f);
                    table.Cell().PaddingVertical(2).AlignRight().Text(r.Rate is { } rate ? $"{rate:N0}" : "").FontSize(8.5f);
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{r.Amount:N2}").FontSize(8.5f);
                }

                table.Cell().ColumnSpan(6).BorderTop(1).PaddingTop(4);
                table.Cell().BorderTop(1).PaddingTop(4).Text("TOTAL").Bold().FontSize(9);
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{report.Total:N2}").Bold().FontSize(9);
            });

            page.Footer().AlignCenter().Text(t => t.CurrentPageNumber().FontSize(8));
        })).GeneratePdf();

    /// <summary>What each vehicle earned, spent and kept, month by month.
    /// Saving is revenue less both trip expenses and maintenance, so a
    /// vehicle that ran profitably but needed an expensive repair shows the
    /// month it actually had.</summary>
    public static byte[] BuildVehicleSavings(IReadOnlyList<VehicleMonthlySaving> rows, Company company, string filterSummary) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Element(c => Header(c, company, "Vehicle savings report", filterSummary));

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.4f); c.RelativeColumn(1.2f); c.RelativeColumn(0.8f);
                    c.RelativeColumn(1.3f); c.RelativeColumn(1.3f); c.RelativeColumn(1.3f);
                    c.RelativeColumn(1.3f); c.RelativeColumn(1.3f);
                });

                table.Header(h =>
                {
                    foreach (var text in new[] { "Vehicle", "Month", "Trips", "Revenue", "Trip expenses", "Maintenance", "Saving", "Saving / trip" })
                        h.Cell().BorderBottom(1).PaddingBottom(4).Text(text).SemiBold();
                });

                foreach (var r in rows)
                {
                    table.Cell().Text(r.VehicleRegNo);
                    table.Cell().Text(r.MonthLabel);
                    table.Cell().AlignRight().Text($"{r.Trips}");
                    table.Cell().AlignRight().Text($"{r.Revenue:N2}");
                    table.Cell().AlignRight().Text($"{r.TripExpenses:N2}");
                    table.Cell().AlignRight().Text($"{r.MaintenanceCost:N2}");
                    table.Cell().AlignRight().Text($"{r.Saving:N2}").SemiBold();
                    table.Cell().AlignRight().Text($"{r.SavingPerTrip:N2}");
                }

                table.Cell().ColumnSpan(2).BorderTop(1).PaddingTop(4).Text("Total").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{rows.Sum(r => r.Trips)}").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{rows.Sum(r => r.Revenue):N2}").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{rows.Sum(r => r.TripExpenses):N2}").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{rows.Sum(r => r.MaintenanceCost):N2}").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{rows.Sum(r => r.Saving):N2}").Bold();
                table.Cell().BorderTop(1);
            });

            page.Footer().AlignCenter().Text(t => t.CurrentPageNumber().FontSize(8));
        })).GeneratePdf();

    /// <summary>Both directions of cash flow — trip expenses and amounts
    /// received — in one dated list. Other-owner vehicle rows print with a
    /// note rather than being silently dropped, but the totals only ever
    /// count what belongs in the company's own accounts.</summary>
    public static byte[] BuildLedger(IReadOnlyList<LedgerRow> rows, Company company, string filterSummary) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Element(c => Header(c, company, "Transactions report", filterSummary));

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.2f); c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f);
                    c.RelativeColumn(1f); c.RelativeColumn(1.6f); c.RelativeColumn(1.2f); c.RelativeColumn(1.6f);
                });

                table.Header(h =>
                {
                    foreach (var text in new[] { "Trip No", "Date", "Vehicle", "Driver", "Type", "Detail", "Amount", "" })
                        h.Cell().BorderBottom(1).PaddingBottom(4).Text(text).SemiBold();
                });

                foreach (var r in rows)
                {
                    table.Cell().Text(r.TripNo);
                    table.Cell().Text(r.Date.ToString("dd-MMM-yy"));
                    table.Cell().Text(r.VehicleRegNo);
                    table.Cell().Text(r.DriverName);
                    table.Cell().Text(r.Kind);
                    table.Cell().Text(r.Detail);
                    table.Cell().AlignRight().Text($"{r.Amount:N2}");
                    table.Cell().Text(r.CountsInCompanyAccounts ? "" : "Other owner").FontColor(Colors.Grey.Darken1).FontSize(7.5f);
                }

                var income = rows.Where(r => r.CountsInCompanyAccounts && r.Kind == "Income").Sum(r => r.Amount);
                var expense = rows.Where(r => r.CountsInCompanyAccounts && r.Kind == "Expense").Sum(r => r.Amount);

                table.Cell().ColumnSpan(6).BorderTop(1).PaddingTop(4).Text("Income (company accounts)").Bold();
                table.Cell().BorderTop(1).PaddingTop(4).AlignRight().Text($"{income:N2}").Bold();
                table.Cell().BorderTop(1);
                table.Cell().ColumnSpan(6).PaddingTop(2).Text("Expenses (company accounts)").Bold();
                table.Cell().PaddingTop(2).AlignRight().Text($"{expense:N2}").Bold();
                table.Cell();
                table.Cell().ColumnSpan(6).PaddingTop(2).Text("Net").Bold();
                table.Cell().PaddingTop(2).AlignRight().Text($"{income - expense:N2}").Bold();
                table.Cell();
            });

            page.Footer().AlignCenter().Text(t => t.CurrentPageNumber().FontSize(8));
        })).GeneratePdf();
}
