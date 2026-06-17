using System.Globalization;
using Richie.Application.Common;
using Richie.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Richie.Infrastructure.Reports;

public sealed partial class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        // QuestPDF is free for this use under the Community licence (offline desktop app).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ToPdf(ReportContent content)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                // Segoe UI / Nirmala UI both carry the ₹ glyph (U+20B9); the bundled default does not.
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI", "Nirmala UI", Fonts.Arial));

                page.Header().Column(col =>
                {
                    col.Item().Text(content.Title).FontSize(20).SemiBold();
                    col.Item().Text($"Generated {content.GeneratedUtc.ToLocalTime():g}  ·  {content.PeriodLabel}")
                        .FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(10);
                    foreach (ReportSection section in content.Sections)
                    {
                        col.Item().PaddingTop(6).Text(section.Heading).FontSize(13).SemiBold();
                        foreach (string line in section.Lines)
                            col.Item().Text(line);
                        if (section.Table is { } table)
                            col.Item().Element(c => RenderTable(c, table));
                        if (section.Chart is { Points.Count: > 0 } chart)
                            col.Item().PaddingTop(6).MaxWidth(380).Image(RenderChartImage(chart));
                    }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void RenderTable(IContainer container, ReportTable table)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                foreach (string _ in table.Columns)
                    cols.RelativeColumn();
            });

            foreach (string column in table.Columns)
                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(column).SemiBold();

            for (int r = 0; r < table.Rows.Count; r++)
            {
                IReadOnlyList<string> row = table.Rows[r];
                string? rowLink = table.RowLinks is { } links && r < links.Count ? links[r] : null;

                for (int c = 0; c < row.Count; c++)
                {
                    IContainer cell = t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                    bool isLink = rowLink is { Length: > 0 } && table.LinkColumns?.Contains(c) == true;
                    if (isLink)
                        cell = cell.Hyperlink(rowLink!);

                    var span = cell.Text(row[c]);
                    if (isLink)
                        span.FontColor("#3E86C6").Underline();          // link styling (brand blue)
                    else if (table.SignedColumns?.Contains(c) == true)
                    {
                        int sign = SignOf(row[c]);
                        if (sign > 0) span.FontColor(BrandColors.ProfitGreen).SemiBold();
                        else if (sign < 0) span.FontColor(BrandColors.LossRed).SemiBold();
                    }
                }
            }
        });
    }

    /// <summary>Sign of a formatted money/percent cell (handles ₹, %, thousands separators, ± sign).</summary>
    private static int SignOf(string cell)
    {
        string s = cell.Replace("₹", "").Replace("%", "").Replace(",", "").Trim();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal v))
            return v > 0 ? 1 : v < 0 ? -1 : 0;
        return 0;
    }
}
