using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Richie.Application.Common;
using Richie.Application.Reports;
using SkiaSharp;

namespace Richie.Infrastructure.Reports;

public sealed partial class ReportExporter
{
    private const int ChartWidth = 900;
    private const int ChartHeight = 540;
    private static readonly SKColor LabelColor = new(0x32, 0x32, 0x32);

    // Report-chart palette deliberately excludes green and red so those stay reserved for the
    // profit/loss colouring elsewhere in the report. Explicit so slices/bars are visible without a
    // configured LiveCharts theme.
    private static readonly SKColor[] Palette =
        BrandColors.ReportChartPalette.Select(SKColor.Parse).ToArray();

    /// <summary>Renders a report chart spec to a PNG image using SkiaSharp in-memory charts (no WPF).</summary>
    public static byte[] RenderChartImage(ReportChart chart)
    {
        InMemorySkiaSharpChart view = chart.Kind == ReportChartKind.Pie
            ? BuildPie(chart.Points)
            : BuildColumn(chart.Points);

        using var stream = new MemoryStream();
        view.SaveImage(stream);
        return stream.ToArray();
    }

    private static SKPieChart BuildPie(IReadOnlyList<ReportChartPoint> points)
    {
        var series = new List<ISeries>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            ReportChartPoint pt = points[i];
            series.Add(new PieSeries<double>
            {
                Values = [pt.Value],
                Name = pt.Label,
                Fill = new SolidColorPaint(Palette[i % Palette.Length]),
                DataLabelsPaint = new SolidColorPaint(LabelColor),
                DataLabelsFormatter = _ => pt.Label,
                DataLabelsPosition = PolarLabelsPosition.Outer
            });
        }

        return new SKPieChart
        {
            Width = ChartWidth,
            Height = ChartHeight,
            Background = SKColors.White,
            Series = series
        };
    }

    private static SKCartesianChart BuildColumn(IReadOnlyList<ReportChartPoint> points)
    {
        var column = new ColumnSeries<double>
        {
            Values = points.Select(p => p.Value).ToArray(),
            Name = string.Empty,
            Fill = new SolidColorPaint(Palette[0])
        };

        return new SKCartesianChart
        {
            Width = ChartWidth,
            Height = ChartHeight,
            Background = SKColors.White,
            Series = [column],
            XAxes =
            [
                new Axis
                {
                    Labels = points.Select(p => p.Label).ToArray(),
                    LabelsPaint = new SolidColorPaint(LabelColor),
                    LabelsRotation = 30,
                    TextSize = 12
                }
            ],
            YAxes =
            [
                new Axis { LabelsPaint = new SolidColorPaint(LabelColor), TextSize = 12 }
            ]
        };
    }
}
