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

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
