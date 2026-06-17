using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Richie.Application.Reports;
using Richie.Infrastructure.Reports;

namespace Richie.Infrastructure.Tests.Reports;

public sealed class ReportExporterTests
{
    private static ReportContent Sample() => new(
        "Test Report", new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc), "All data",
        [
            new ReportSection("Summary", ["Total: 1,200.00", "P&L: +200.00"],
                new ReportTable(["Name", "Value"], [["Acme", "1,000"], ["Beta", "200"]])),
            new ReportSection("Notes", ["Just text, no table."])
        ]);

    private static ReportContent SampleWithCharts() => new(
        "Charted Report", new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc), "All data",
        [
            new ReportSection("Allocation", [],
                new ReportTable(["Type", "Value"], [["Equity", "600"], ["Debt", "400"]]),
                new ReportChart(ReportChartKind.Pie,
                    [new ReportChartPoint("Equity", 600), new ReportChartPoint("Debt", 400)])),
            new ReportSection("Monthly trend", [],
                new ReportTable(["Month", "Spend"], [["Apr", "100"], ["May", "250"], ["Jun", "180"]]),
                new ReportChart(ReportChartKind.Column,
                    [new ReportChartPoint("Apr", 100), new ReportChartPoint("May", 250), new ReportChartPoint("Jun", 180)]))
        ]);

    // A report exercising signed (P&L) columns and per-row hyperlink columns.
    private static ReportContent SampleWithSemantics() => new(
        "Semantic Report", new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc), "All data",
        [
            new ReportSection("Portfolio summary", [],
                new ReportTable(
                    ["Invested", "Current", "P&L", "Return %"],
                    [["₹1,000.00", "₹1,200.00", "₹200.00", "+20.0%"], ["₹500.00", "₹400.00", "₹-100.00", "-20.0%"]],
                    SignedColumns: [2, 3])),
            new ReportSection("Vault accounts", [],
                new ReportTable(
                    ["Account", "Password", "Website"],
                    [["GitHub", "••••", "https://github.com"]],
                    LinkColumns: [0, 2], RowLinks: ["https://github.com"]))
        ]);

    [Fact]
    public void ToXlsx_AppliesProfitLossColours_AndHyperlinks()
    {
        byte[] xlsx = new ReportExporter().ToXlsx(SampleWithSemantics());

        using var ms = new MemoryStream(xlsx);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);

        ClosedXML.Excel.IXLWorksheet summary = wb.Worksheet("Portfolio summary");
        int profit = Find(summary, "₹200.00").Style.Font.FontColor.Color.ToArgb();
        int loss = Find(summary, "₹-100.00").Style.Font.FontColor.Color.ToArgb();
        Assert.Equal(ClosedXML.Excel.XLColor.FromHtml("#1FA56C").Color.ToArgb(), profit);   // green
        Assert.Equal(ClosedXML.Excel.XLColor.FromHtml("#CE2E20").Color.ToArgb(), loss);     // red

        ClosedXML.Excel.IXLWorksheet vault = wb.Worksheet("Vault accounts");
        ClosedXML.Excel.IXLCell account = Find(vault, "GitHub");
        Assert.True(account.HasHyperlink);
        Assert.Equal("https://github.com/", account.GetHyperlink().ExternalAddress.ToString());
    }

    [Fact]
    public void ToPdf_WithSignedAndLinkedTable_StillProducesAPdf()
    {
        byte[] pdf = new ReportExporter().ToPdf(SampleWithSemantics());
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static ClosedXML.Excel.IXLCell Find(ClosedXML.Excel.IXLWorksheet ws, string value) =>
        ws.CellsUsed().First(c => c.GetString() == value);

    [Theory]
    [InlineData(ReportChartKind.Pie)]
    [InlineData(ReportChartKind.Column)]
    public void RenderChartImage_ProducesANonTrivialPng(ReportChartKind kind)
    {
        var chart = new ReportChart(kind,
            [new ReportChartPoint("A", 30), new ReportChartPoint("B", 50), new ReportChartPoint("C", 20)]);

        byte[] png = ReportExporter.RenderChartImage(chart);

        // PNG magic header: 0x89 'P' 'N' 'G'.
        Assert.True(png.Length > 2000, $"expected a drawn chart, got {png.Length} bytes");
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }

    [Fact]
    public void ToPdf_WithCharts_ProducesALargerPdf()
    {
        byte[] plain = new ReportExporter().ToPdf(Sample());
        byte[] charted = new ReportExporter().ToPdf(SampleWithCharts());

        Assert.Equal("%PDF", Encoding.ASCII.GetString(charted, 0, 4));
        Assert.True(charted.Length > plain.Length);   // embedded chart images add bytes
    }

    [Fact]
    public void ToXlsx_ProducesAWorkbook_WithOneSheetPerSectionPlusMeta()
    {
        byte[] xlsx = new ReportExporter().ToXlsx(Sample());

        Assert.Equal("PK", Encoding.ASCII.GetString(xlsx, 0, 2));   // zip/OOXML header

        using var ms = new MemoryStream(xlsx);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        // "Report" meta sheet + one per section.
        Assert.Equal(3, wb.Worksheets.Count);
        ClosedXML.Excel.IXLWorksheet summary = wb.Worksheet("Summary");
        Assert.Equal("Summary", summary.Cell(1, 1).GetString());          // heading
        Assert.Contains("Acme", summary.RowsUsed().SelectMany(r => r.CellsUsed()).Select(c => c.GetString()));
    }

    [Fact]
    public void ToCsv_FlattensHeadingsLinesAndTables()
    {
        byte[] csv = new ReportExporter().ToCsv(Sample());
        string text = Encoding.UTF8.GetString(csv);

        Assert.Contains("Test Report", text);
        Assert.Contains("Summary", text);
        Assert.Contains("Name,Value", text);   // table header
        Assert.Contains("Acme", text);
    }

    [Fact]
    public void ToCsv_QuotesFieldsContainingCommas()
    {
        var content = new ReportContent("T", DateTime.UtcNow, "All data",
            [new ReportSection("S", [], new ReportTable(["A", "B"], [["x,y", "z"]]))]);

        string text = Encoding.UTF8.GetString(new ReportExporter().ToCsv(content));

        Assert.Contains("\"x,y\",z", text);
    }

    [Fact]
    public void ToPptx_WithCharts_AddsChartSlidesAndImages()
    {
        byte[] pptx = new ReportExporter().ToPptx(SampleWithCharts());

        using var ms = new MemoryStream(pptx);
        using PresentationDocument doc = PresentationDocument.Open(ms, false);
        IEnumerable<SlidePart> slides = doc.PresentationPart!.SlideParts;

        // Title + 2 sections + 2 chart slides.
        Assert.Equal(5, slides.Count());
        // Two of the slides embed a PNG image.
        Assert.Equal(2, slides.Count(s => s.ImageParts.Any()));
    }

    [Fact]
    public void ToPdf_ProducesAPdf()
    {
        byte[] pdf = new ReportExporter().ToPdf(Sample());

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));   // PDF magic header
    }

    [Fact]
    public void ToPptx_ProducesAValidDeck_WithTitlePlusSectionSlides()
    {
        byte[] pptx = new ReportExporter().ToPptx(Sample());

        Assert.True(pptx.Length > 0);
        Assert.Equal("PK", Encoding.ASCII.GetString(pptx, 0, 2));    // zip/OOXML header

        using var ms = new MemoryStream(pptx);
        using PresentationDocument doc = PresentationDocument.Open(ms, false);
        int slides = doc.PresentationPart!.SlideParts.Count();
        Assert.Equal(3, slides);   // title slide + 2 sections
    }
}
