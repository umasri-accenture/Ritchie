using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Richie.Application.Common;
using Richie.Application.Reports;

namespace Richie.Infrastructure.Reports;

public sealed partial class ReportExporter
{
    public byte[] ToXlsx(ReportContent content)
    {
        using var workbook = new XLWorkbook();

        // First sheet: report metadata.
        IXLWorksheet info = workbook.Worksheets.Add(SheetName("Report", workbook));
        info.Cell(1, 1).Value = content.Title;
        info.Cell(1, 1).Style.Font.Bold = true;
        info.Cell(1, 1).Style.Font.FontSize = 14;
        info.Cell(2, 1).Value = $"Generated {content.GeneratedUtc.ToLocalTime():g}";
        info.Cell(3, 1).Value = content.PeriodLabel;
        info.Columns().AdjustToContents();

        foreach (ReportSection section in content.Sections)
        {
            IXLWorksheet ws = workbook.Worksheets.Add(SheetName(section.Heading, workbook));
            int row = 1;

            ws.Cell(row, 1).Value = section.Heading;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 13;
            row += 2;

            foreach (string line in section.Lines)
                ws.Cell(row++, 1).Value = line;

            if (section.Table is { } table)
            {
                if (section.Lines.Count > 0) row++;

                for (int c = 0; c < table.Columns.Count; c++)
                {
                    IXLCell cell = ws.Cell(row, c + 1);
                    cell.Value = table.Columns[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFEFEF");
                }
                row++;

                for (int dr = 0; dr < table.Rows.Count; dr++)
                {
                    IReadOnlyList<string> dataRow = table.Rows[dr];
                    string? rowLink = table.RowLinks is { } links && dr < links.Count ? links[dr] : null;

                    for (int c = 0; c < dataRow.Count; c++)
                    {
                        IXLCell cell = ws.Cell(row, c + 1);
                        cell.Value = dataRow[c];

                        if (rowLink is { Length: > 0 } && table.LinkColumns?.Contains(c) == true
                            && Uri.TryCreate(rowLink, UriKind.Absolute, out Uri? uri))
                        {
                            cell.SetHyperlink(new XLHyperlink(uri));
                            cell.Style.Font.FontColor = XLColor.FromHtml("#3E86C6");
                            cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                        }
                        else if (table.SignedColumns?.Contains(c) == true)
                        {
                            int sign = SignOf(dataRow[c]);
                            if (sign > 0) cell.Style.Font.FontColor = XLColor.FromHtml(BrandColors.ProfitGreen);
                            else if (sign < 0) cell.Style.Font.FontColor = XLColor.FromHtml(BrandColors.LossRed);
                            if (sign != 0) cell.Style.Font.Bold = true;
                        }
                    }
                    row++;
                }
            }

            ws.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ToCsv(ReportContent content)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Csv(content.Title));
        sb.AppendLine(Csv($"Generated {content.GeneratedUtc.ToLocalTime():g}"));
        sb.AppendLine(Csv(content.PeriodLabel));
        sb.AppendLine();

        foreach (ReportSection section in content.Sections)
        {
            sb.AppendLine(Csv(section.Heading));
            foreach (string line in section.Lines)
                sb.AppendLine(Csv(line));

            if (section.Table is { } table)
            {
                sb.AppendLine(string.Join(",", table.Columns.Select(Csv)));
                foreach (IReadOnlyList<string> row in table.Rows)
                    sb.AppendLine(string.Join(",", row.Select(Csv)));
            }

            sb.AppendLine();
        }

        // UTF-8 BOM so Excel opens non-ASCII (e.g. ₹, ••••) correctly.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>Escapes a CSV field per RFC 4180 (quote when it contains a comma, quote or newline).</summary>
    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>Excel sheet names must be ≤31 chars, exclude <c>[]:*?/\</c> and be unique.</summary>
    private static string SheetName(string heading, XLWorkbook workbook)
    {
        var sb = new StringBuilder();
        foreach (char ch in heading)
            sb.Append("[]:*?/\\".Contains(ch) ? ' ' : ch);
        string baseName = sb.ToString().Trim();
        if (baseName.Length == 0) baseName = "Sheet";
        if (baseName.Length > 31) baseName = baseName[..31];

        string name = baseName;
        int n = 2;
        while (workbook.Worksheets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            string suffix = " " + n.ToString(CultureInfo.InvariantCulture);
            name = baseName.Length + suffix.Length > 31 ? baseName[..(31 - suffix.Length)] + suffix : baseName + suffix;
            n++;
        }
        return name;
    }
}
