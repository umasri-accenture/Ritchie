using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Richie.Application.Audit;
using Richie.Domain.Audit;
using Richie.UI.Charts;
using SkiaSharp;

namespace Richie.UI.ViewModels;

public partial class HealthAuditViewModel : ObservableObject
{
    private readonly IHealthAuditService _audit;
    private readonly IComplianceService _complianceService;
    private readonly IInsightGenerator _insights;

    public sealed record BenchmarkDisplay(
        string ClassName, string ActualText, string RecommendedText, string StatusText, Brush StatusBrush);

    public sealed record ComplianceDisplay(string Name, string StatusText, Brush StatusBrush, string Detail);

    [ObservableProperty] private bool _hasAssets;
    [ObservableProperty] private bool _noAssets;
    [ObservableProperty] private bool _scoresAreInterim;

    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private string _healthRating = string.Empty;
    [ObservableProperty] private Brush _healthBrush = Brushes.Gray;
    [ObservableProperty] private ObservableCollection<ScoreFactor> _healthFactors = [];

    // Radar of the health-score factors (each 0–100), a visual complement to the factor list.
    [ObservableProperty] private ISeries[] _healthRadarSeries = [];
    [ObservableProperty] private PolarAxis[] _healthRadarAngleAxes = [];
    [ObservableProperty] private PolarAxis[] _healthRadarRadiusAxes = [];
    [ObservableProperty] private Margin? _healthRadarDrawMargin;

    // Benchmark comparison (kept non-null to avoid LiveCharts startup crashes).
    [ObservableProperty] private ISeries[] _benchmarkComparisonSeries = [];
    [ObservableProperty] private Axis[] _benchmarkComparisonAxes = [];

    [ObservableProperty] private int _riskScore;
    [ObservableProperty] private string _riskBand = string.Empty;
    [ObservableProperty] private string _riskInterpretation = string.Empty;
    [ObservableProperty] private Brush _riskBrush = Brushes.Gray;

    [ObservableProperty] private string _ageBandName = string.Empty;
    [ObservableProperty] private ObservableCollection<BenchmarkDisplay> _benchmark = [];
    [ObservableProperty] private string _diversificationText = string.Empty;
    [ObservableProperty] private ObservableCollection<GoalProgressRow> _goals = [];
    [ObservableProperty] private bool _hasGoals;
    [ObservableProperty] private bool _noGoals;
    [ObservableProperty] private ObservableCollection<string> _coverageGaps = [];
    [ObservableProperty] private bool _hasCoverageGaps;
    [ObservableProperty] private bool _coverageOk;
    [ObservableProperty] private ObservableCollection<string> _suggestions = [];

    [ObservableProperty] private string _complianceOverall = string.Empty;
    [ObservableProperty] private Brush _complianceBrush = Brushes.Gray;
    [ObservableProperty] private ObservableCollection<ComplianceDisplay> _compliance = [];
    [ObservableProperty] private ObservableCollection<GipStatusRow> _gips = [];
    [ObservableProperty] private bool _hasGips;
    [ObservableProperty] private bool _noGips;

    public string HealthScaleLegend => "Scale: 80–100 Excellent · 60–79 Good · below 60 Needs attention.";
    public string RiskScaleLegend => "Scale: ≤20 Low · ≤40 Moderate · ≤60 Moderately High · ≤80 High · >80 Very High.";
    public string InterimNotice =>
        "Interim scoring — the Risk Score, Health Score and age-group benchmarks are placeholder formulas pending team finalization (PRD §22).";

    // Visual palette (no red/green allowed on this page).
    private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)); // #EA580C
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));  // #F59E0B
    private static readonly Brush Teal = new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E));    // #0F766E


    public HealthAuditViewModel(IHealthAuditService audit, IComplianceService compliance, IInsightGenerator insights)
    {
        _audit = audit;
        _complianceService = compliance;
        _insights = insights;
    }

    public void Load()
    {
        HealthAuditReport r = _audit.GetReport();

        HasAssets = r.HasAssets;
        NoAssets = !HasAssets;
        ScoresAreInterim = r.ScoresAreInterim;

        HealthScore = r.HealthScore;
        HealthRating = r.HealthRating;
        // Portfolio Health Score: 43/100 (low) should render as Orange, and “Good” as Teal.
        HealthBrush = r.HealthScore >= 60 ? Teal : Orange;

        HealthFactors = new ObservableCollection<ScoreFactor>(r.HealthFactors);
        BuildRadar(r.HealthFactors);

        RiskScore = r.RiskScore;
        RiskBand = r.RiskBand;
        RiskInterpretation = r.RiskInterpretation;
        // Risk Score: high-risk should render as Orange; positives as Teal.
        RiskBrush = r.RiskScore <= 40 ? Teal : r.RiskScore <= 60 ? Amber : Orange;


        AgeBandName = r.AgeBandName;
        Benchmark = new ObservableCollection<BenchmarkDisplay>(r.Benchmark.Select(ToDisplay));
        BuildBenchmarkChart(r.Benchmark);
        DiversificationText = $"{r.DistinctClassCount} of 4 broad asset classes represented" +
            (r.MissingClasses.Count > 0 ? $" — missing: {string.Join(", ", r.MissingClasses)}." : ".");

        Goals = new ObservableCollection<GoalProgressRow>(r.Goals);
        HasGoals = r.Goals.Count > 0;
        NoGoals = !HasGoals;
        CoverageGaps = new ObservableCollection<string>(r.CoverageGaps);
        HasCoverageGaps = r.CoverageGaps.Count > 0;
        CoverageOk = !HasCoverageGaps;

        // Cross-module insights (portfolio + spending) supersede the audit-only suggestions list.
        Suggestions = new ObservableCollection<string>(_insights.Generate());

        ComplianceReport c = _complianceService.GetReport();
        ComplianceOverall = c.OverallStatus;
        ComplianceBrush = c.IsCompliant ? Teal
            : c.Areas.Any(a => a.Status == ComplianceStatus.Red) ? Orange : Amber;

        Compliance = new ObservableCollection<ComplianceDisplay>(c.Areas.Select(ToComplianceDisplay));
        Gips = new ObservableCollection<GipStatusRow>(c.Gips);
        HasGips = c.Gips.Count > 0;
        NoGips = !HasGips;
    }

    private void BuildRadar(IReadOnlyList<ScoreFactor> factors)
    {
        HealthRadarSeries =
        [
            new PolarLineSeries<double>
            {
                Values = factors.Select(f => (double)f.Points).ToArray(),
                Name = "Your score",
                IsClosed = true,
                Fill = new SolidColorPaint(BrandPalette.Primary.WithAlpha(60)),
                Stroke = new SolidColorPaint(BrandPalette.Primary) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(BrandPalette.Primary),
                GeometryStroke = new SolidColorPaint(BrandPalette.Primary),
                GeometrySize = 8
            }
        ];
        HealthRadarAngleAxes = [
            new PolarAxis
            {
                Labels = factors.Select(f => f.Name switch
                {
                    "Benchmark alignment" => "Benchmark",
                    "Goal progress" => "Goals",
                    _ => f.Name
                }).ToArray(),
                TextSize = 10
            }
        ];
        HealthRadarRadiusAxes = [
            new PolarAxis
            {
                MinLimit = 0,
                MaxLimit = 100,
                TextSize = 8
            }
        ];
        HealthRadarDrawMargin = new Margin(15);
    }

    private void BuildBenchmarkChart(IReadOnlyList<BenchmarkRow> benchmark)
    {
        BenchmarkComparisonSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Recommended %",
                Values = benchmark.Select(b => (double)b.RecommendedPercent).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#94A3B8"))
            },
            new ColumnSeries<double>
            {
                Name = "Mine %",
                Values = benchmark.Select(b => (double)b.ActualPercent).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB"))
            }
        ];

        BenchmarkComparisonAxes =
        [
            new Axis
            {
                Labels = benchmark.Select(b => b.ClassName).ToArray(),
                LabelsRotation = 0
            }
        ];
    }

    private ComplianceDisplay ToComplianceDisplay(ComplianceArea area)
    {
        (string text, Brush brush) = area.Status switch
        {
            ComplianceStatus.Green => ("Good", Teal),
            ComplianceStatus.Amber => ("Needs attention", Amber),
            _ => ("Critical", Orange)

        };
        return new ComplianceDisplay(area.Name, text, brush, area.Detail);
    }

    private BenchmarkDisplay ToDisplay(BenchmarkRow row)
    {
        (string text, Brush brush) = row.Status switch
        {
            BenchmarkStatus.OnTarget => ("On target", Teal),
            // Task requirement: “Over” must be orange (#EA580C), not amber.
            BenchmarkStatus.Over => ("Over", Orange),
            _ => ("Under", Amber)

        };
        return new BenchmarkDisplay(row.ClassName, $"{row.ActualPercent:0.#}%", $"{row.RecommendedPercent:0}%", text, brush);
    }
}
