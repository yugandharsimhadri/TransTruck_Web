using TransTrack.Api.Documents;
using TransTrack.Core;

namespace TransTrack.Tests;

/// <summary>
/// The party's month-end bill: what each trip cost, itemised, and the tax on
/// the whole thing.
///
/// Two properties matter enough to pin down. First, the columns are per
/// addition — wayment, loading and unloading each named — because a party
/// querying a bill asks about one of them specifically and a lumped "extras"
/// figure gives them nothing to check. Second, GST is charged once on the
/// bill's total rather than per trip, so the rows must sum to the pre-tax total
/// and the tax must appear beneath them exactly once.
///
/// Generating the PDF is itself part of the test: QuestPDF throws when a
/// table's header, body and footer disagree about how many columns there are,
/// and a conditional column is precisely where that goes wrong.
/// </summary>
public class PartyBillLayoutTests
{
    static PartyBillLayoutTests()
    {
        // Program.cs does this at startup; a test that generates a document
        // without going through the host has to say it too, or QuestPDF refuses
        // to render at all.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static Company TestCompany() => new()
    {
        CompanyName = "Test Transport",
        OwnerName = "Owner",
        OwnerPhone = "9999999999",
    };

    private static PartyTripRow Row(
        int serial,
        decimal amount,
        decimal wayment = 0,
        decimal loading = 0,
        decimal unloading = 0,
        decimal gst = 0) =>
        new(serial, new DateTime(2026, 9, serial), "KA01AA1111", "Bengaluru", "Hyderabad",
            10m, amount / 10m, amount, $"LR{serial:00000}", wayment, loading, unloading, gst,
            gst > 0 ? 5m : null);

    private static PartyReport ReportOf(params PartyTripRow[] rows) =>
        new("Sri Traders", "SEPTEMBER-2026", rows);

    [Fact]
    public void Each_addition_is_its_own_column_only_when_something_used_it()
    {
        var report = ReportOf(
            Row(1, 10_000m, wayment: 500m),
            Row(2, 8_000m, wayment: 250m));

        Assert.True(report.HasWayment);
        Assert.False(report.HasLoading);
        Assert.False(report.HasUnloading);
    }

    [Fact]
    public void Row_totals_are_before_tax_so_the_column_sums_to_the_bill()
    {
        var report = ReportOf(
            Row(1, 10_000m, wayment: 500m, loading: 300m, unloading: 200m, gst: 550m),
            Row(2, 8_000m, loading: 200m, gst: 410m));

        Assert.Equal(11_000m + 8_200m, report.TotalBeforeTax);
        Assert.Equal(report.TotalBeforeTax, report.Rows.Sum(r => r.TotalBeforeTax));

        // And tax sits on top of that sum exactly once.
        Assert.Equal(960m, report.TotalGst);
        Assert.Equal(report.TotalBeforeTax + report.TotalGst, report.GrandTotal);
    }

    [Fact]
    public void One_rate_across_the_bill_is_named_in_the_tax_line()
    {
        var report = ReportOf(
            Row(1, 10_000m, gst: 500m),
            Row(2, 8_000m, gst: 400m));

        Assert.Equal(5m, report.GstRate);
        Assert.Equal("GST @ 5%", report.GstLabel);
    }

    [Fact]
    public void Mixed_rates_are_not_labelled_with_a_rate_that_only_some_trips_paid()
    {
        var mixed = ReportOf(
            new PartyTripRow(1, new DateTime(2026, 9, 1), "KA01AA1111", "A", "B", 10m, 1000m, 10_000m,
                "LR1", 0, 0, 0, 500m, 5m),
            new PartyTripRow(2, new DateTime(2026, 9, 2), "KA01AA1111", "A", "B", 10m, 1000m, 10_000m,
                "LR2", 0, 0, 0, 1_800m, 18m));

        Assert.Null(mixed.GstRate);
        Assert.Equal("GST", mixed.GstLabel);
    }

    [Fact]
    public void A_bill_with_no_tax_has_no_tax_line_to_label()
    {
        var report = ReportOf(Row(1, 10_000m, loading: 300m));

        Assert.False(report.HasGst);
        Assert.Equal(report.TotalBeforeTax, report.GrandTotal);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    public void The_bill_generates_whichever_columns_the_period_needs(
        bool wayment, bool loading, bool unloading, bool gst)
    {
        var report = ReportOf(
            Row(1, 10_000m,
                wayment: wayment ? 500m : 0m,
                loading: loading ? 300m : 0m,
                unloading: unloading ? 200m : 0m,
                gst: gst ? 550m : 0m),
            Row(2, 6_000m));

        // Nothing to assert about the bytes; the point is that it renders at
        // all. A conditional column added to the header but not the footer
        // throws here and nowhere else until someone prints a bill.
        var pdf = PartyBillDocument.Build(report, TestCompany());

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void An_empty_period_still_produces_a_bill_rather_than_throwing()
    {
        var pdf = PartyBillDocument.Build(ReportOf(), TestCompany());
        Assert.NotEmpty(pdf);
    }
}
