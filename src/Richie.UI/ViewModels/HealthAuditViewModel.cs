using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Richie.Application.Audit;
using Richie.Domain.Audit;
using Richie.UI.Charts;

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
    [ObservableProperty] private bool _scoresAreInterim;

    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private string _healthRating = string.Empty;
    [ObservableProperty] private Brush _healthBrush = Brushes.Gray;
    [ObservableProperty] private ObservableCollection<ScoreFactor> _healthFactors = [];

    // Radar of the health-score factors (each 0–100), a visual complement to the factor list.
    [ObservableProperty] private ISeries[] _healthRadarSeries = [];
    [ObservableProperty] private PolarAxis[] _healthRadarAngleAxes = [];
    [ObservableProperty] private PolarAxis[] _healthRadarRadiusAxes = [];

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

    public string HealthScaleLegend => "Scale: 80–100 Excellent · 60–79 Good · below 60 Needs attention.";
    public string RiskScaleLegend => "Scale: ≤20 Low · ≤40 Moderate · ≤60 Moderately High · ≤80 High · >80 Very High.";
    public string InterimNotice =>
        "Interim scoring — the Risk Score, Health Score and age-group benchmarks are placeholder formulas pending team finalization (PRD §22).";

    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F));

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
        ScoresAreInterim = r.ScoresAreInterim;

        HealthScore = r.HealthScore;
        HealthRating = r.HealthRating;
        HealthBrush = r.HealthScore >= 80 ? Green : r.HealthScore >= 60 ? Amber : Red;
        HealthFactors = new ObservableCollection<ScoreFactor>(r.HealthFactors);
        BuildRadar(r.HealthFactors);

        RiskScore = r.RiskScore;
        RiskBand = r.RiskBand;
        RiskInterpretation = r.RiskInterpretation;
        RiskBrush = r.RiskScore <= 40 ? Green : r.RiskScore <= 60 ? Amber : Red;

        AgeBandName = r.AgeBandName;
        Benchmark = new ObservableCollection<BenchmarkDisplay>(r.Benchmark.Select(ToDisplay));
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
        ComplianceBrush = c.IsCompliant ? Green
            : c.Areas.Any(a => a.Status == ComplianceStatus.Red) ? Red : Amber;
        Compliance = new ObservableCollection<ComplianceDisplay>(c.Areas.Select(ToComplianceDisplay));
        Gips = new ObservableCollection<GipStatusRow>(c.Gips);
        HasGips = c.Gips.Count > 0;
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
        HealthRadarAngleAxes = [new PolarAxis { Labels = factors.Select(f => f.Name).ToArray() }];
        HealthRadarRadiusAxes = [new PolarAxis { MinLimit = 0, MaxLimit = 100 }];
    }

    private ComplianceDisplay ToComplianceDisplay(ComplianceArea area)
    {
        (string text, Brush brush) = area.Status switch
        {
            ComplianceStatus.Green => ("Good", Green),
            ComplianceStatus.Amber => ("Needs attention", Amber),
            _ => ("Critical", Red)
        };
        return new ComplianceDisplay(area.Name, text, brush, area.Detail);
    }

    private BenchmarkDisplay ToDisplay(BenchmarkRow row)
    {
        (string text, Brush brush) = row.Status switch
        {
            BenchmarkStatus.OnTarget => ("On target", Green),
            BenchmarkStatus.Over => ("Over", Amber),
            _ => ("Under", Amber)
        };
        return new BenchmarkDisplay(row.ClassName, $"{row.ActualPercent:0.#}%", $"{row.RecommendedPercent:0}%", text, brush);
    }
}
