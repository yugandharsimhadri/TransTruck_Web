using ClosedXML.Excel;
using TransTrack.Core;

namespace TransTrack.Api.Documents;

/// <summary>Same rows as <see cref="ReportPdfBuilder"/>, as an .xlsx byte
/// array instead of a PDF — QuestPDF port target is a download response, not
/// a SaveFileDialog path, so this returns bytes rather than writing to disk.</summary>
public static class ReportExcelBuilder
{
    public static byte[] BuildTrips(IReadOnlyList<Trip> trips)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Trips");

        string[] headers = ["Trip No", "Date", "Vehicle", "Driver", "Party", "From", "To", "Weight", "Rate", "Amount", "Expenses", "Balance", "LR No", "Bill No"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var t in trips)
        {
            sheet.Cell(row, 1).Value = t.TripNo;
            sheet.Cell(row, 2).Value = t.Date;
            sheet.Cell(row, 3).Value = t.Vehicle.RegNo;
            sheet.Cell(row, 4).Value = t.Driver.Name;
            sheet.Cell(row, 5).Value = t.Party.Name;
            sheet.Cell(row, 6).Value = t.FromCity.Name;
            sheet.Cell(row, 7).Value = t.ToCity.Name;
            sheet.Cell(row, 8).Value = t.Weight;
            sheet.Cell(row, 9).Value = t.Rate;
            sheet.Cell(row, 10).Value = t.Amount;
            sheet.Cell(row, 11).Value = t.TotalExpenses;
            sheet.Cell(row, 12).Value = t.BalanceReceivable;
            sheet.Cell(row, 13).Value = t.LrNo;
            sheet.Cell(row, 14).Value = t.BillNo;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildMaintenance(IReadOnlyList<VehicleMaintenance> records)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Maintenance");

        string[] headers = ["Vehicle", "Date", "Category", "Vendor", "Amount", "Odometer", "Next due date", "Next due odometer", "Remarks"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var m in records)
        {
            sheet.Cell(row, 1).Value = m.Vehicle.RegNo;
            sheet.Cell(row, 2).Value = m.Date;
            sheet.Cell(row, 3).Value = m.MaintenanceCategory.Name;
            sheet.Cell(row, 4).Value = m.VendorName;
            sheet.Cell(row, 5).Value = m.Amount;
            sheet.Cell(row, 6).Value = m.OdometerReading;
            sheet.Cell(row, 7).Value = m.NextDueDate;
            sheet.Cell(row, 8).Value = m.NextDueOdometer;
            sheet.Cell(row, 9).Value = m.Remarks;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildLedger(IReadOnlyList<LedgerRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Transactions");

        string[] headers = ["Trip No", "Date", "Vehicle", "Driver", "Type", "Detail", "Amount", "Counts in company accounts"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.TripNo;
            sheet.Cell(row, 2).Value = r.Date;
            sheet.Cell(row, 3).Value = r.VehicleRegNo;
            sheet.Cell(row, 4).Value = r.DriverName;
            sheet.Cell(row, 5).Value = r.Kind;
            sheet.Cell(row, 6).Value = r.Detail;
            sheet.Cell(row, 7).Value = r.Amount;
            sheet.Cell(row, 8).Value = r.CountsInCompanyAccounts ? "Yes" : "No — other owner";
            row++;
        }

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildPartyReport(PartyReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Party report");

        // The title line the paper report carries, kept above the header row
        // so the exported sheet is self-describing once it leaves the app.
        sheet.Cell(1, 1).Value = $"{report.PartyName.ToUpperInvariant()} {report.PeriodLabel}";
        sheet.Range(1, 1, 1, 8).Merge().Style.Font.Bold = true;

        string[] headers = ["S NO", "DATE", "VEHICLE NO", "FROM", "TO", "WEIGHT", "RATE", "AMOUNT"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(2, i + 1).Value = headers[i];
        sheet.Row(2).Style.Font.Bold = true;

        var row = 3;
        foreach (var r in report.Rows)
        {
            sheet.Cell(row, 1).Value = r.SerialNo;
            sheet.Cell(row, 2).Value = r.Date;
            sheet.Cell(row, 3).Value = r.VehicleRegNo;
            sheet.Cell(row, 4).Value = r.FromCity;
            sheet.Cell(row, 5).Value = r.ToCity;
            sheet.Cell(row, 6).Value = r.Weight;
            sheet.Cell(row, 7).Value = r.Rate;
            sheet.Cell(row, 8).Value = r.Amount;
            row++;
        }

        sheet.Cell(row, 7).Value = "TOTAL";
        sheet.Cell(row, 8).Value = report.Total;
        sheet.Row(row).Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildVehicleSavings(IReadOnlyList<VehicleMonthlySaving> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Vehicle savings");

        string[] headers = ["Vehicle", "Month", "Trips", "Revenue", "Trip expenses", "Maintenance", "Saving", "Saving per trip"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.VehicleRegNo;
            sheet.Cell(row, 2).Value = r.MonthLabel;
            sheet.Cell(row, 3).Value = r.Trips;
            sheet.Cell(row, 4).Value = r.Revenue;
            sheet.Cell(row, 5).Value = r.TripExpenses;
            sheet.Cell(row, 6).Value = r.MaintenanceCost;
            sheet.Cell(row, 7).Value = r.Saving;
            sheet.Cell(row, 8).Value = r.SavingPerTrip;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
