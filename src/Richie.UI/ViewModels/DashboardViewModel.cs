using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Richie.Application.Assets;
using Richie.Application.Dashboard;
using Richie.Application.Expenses;

namespace Richie.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboard;
    private readonly IAssetService _assets;
    private readonly IExpenseAnalyticsService _analytics;

    public sealed record UpcomingSipRow(string AssetName, string AmountText, string DueText, string FrequencyText);
    public sealed record ActivityRow(string DateText, string Module, string Action, string Description);

    [ObservableProperty] private string _totalAssetsText = "—";
    [ObservableProperty] private string _totalInvestedText = "—";
    [ObservableProperty] private string _totalExpensesText = "—";
    [ObservableProperty] private string _profitLossText = "—";
    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private string _healthRating = string.Empty;
    [ObservableProperty] private Brush _healthBrush = Brushes.Gray;
    [ObservableProperty] private bool _healthIsInterim;

    [ObservableProperty] private ISeries[] _allocationSeries = [];
    [ObservableProperty] private bool _hasAssets;
    [ObservableProperty] private ISeries[] _monthlySeries = [];
    [ObservableProperty] private Axis[] _monthlyAxes = [];

    [ObservableProperty] private ObservableCollection<UpcomingSipRow> _upcomingSips = [];
    [ObservableProperty] private bool _hasUpcomingSips;
    [ObservableProperty] private bool _noUpcomingSips;
    [ObservableProperty] private ObservableCollection<string> _insights = [];
    [ObservableProperty] private ObservableCollection<ActivityRow> _recentActivity = [];
    [ObservableProperty] private bool _hasActivity;
    [ObservableProperty] private bool _noActivity;

    public string HealthScaleLegend => "0–100 · 80–100 Excellent · 60–79 Good · below 60 Needs attention.";

    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F));

    public DashboardViewModel(IDashboardService dashboard, IAssetService assets, IExpenseAnalyticsService analytics)
    {
        _dashboard = dashboard;
        _assets = assets;
        _analytics = analytics;
    }

    public void Load()
    {
        DashboardSummary s = _dashboard.GetSummary();

        TotalAssetsText = Money(s.TotalAssets);
        TotalInvestedText = Money(s.TotalInvested);
        TotalExpensesText = Money(s.TotalExpensesThisMonth);
        ProfitLossText = $"{Money(s.ProfitLoss)} ({s.ProfitLossPercent:+0.0;-0.0;0.0}%)";
        HealthScore = s.HealthScore;
        HealthRating = s.HealthRating;
        HealthBrush = s.HealthScore >= 80 ? Green : s.HealthScore >= 60 ? Amber : Red;
        HealthIsInterim = s.HealthIsInterim;

        UpcomingSips = new ObservableCollection<UpcomingSipRow>(s.UpcomingSips.Select(u =>
            new UpcomingSipRow(u.AssetName, Money(u.Amount), u.DueDate.ToString("d", CultureInfo.CurrentCulture), u.Frequency.ToString())));
        HasUpcomingSips = UpcomingSips.Count > 0;
        NoUpcomingSips = !HasUpcomingSips;

        Insights = new ObservableCollection<string>(s.Insights);

        RecentActivity = new ObservableCollection<ActivityRow>(s.RecentActivity.Select(a =>
            new ActivityRow(a.TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), a.Module, a.Action, a.Description)));
        HasActivity = RecentActivity.Count > 0;
        NoActivity = !HasActivity;

        BuildCharts();
    }

    private void BuildCharts()
    {
        IReadOnlyList<AllocationSlice> allocation = _assets.GetPortfolioSummary().Allocation;
        HasAssets = allocation.Count > 0;
        AllocationSeries = allocation
            .Select(a => (ISeries)new PieSeries<double> { Values = [(double)a.Value], Name = a.TypeName, InnerRadius = 55 })
            .ToArray();

        IReadOnlyList<PeriodDatum> months = _analytics.GetMonthlyTotals(6);
        MonthlySeries = [new ColumnSeries<double> { Values = months.Select(m => (double)m.Amount).ToArray(), Name = "Spend" }];
        MonthlyAxes = [new Axis { Labels = months.Select(m => m.Label).ToArray(), LabelsRotation = 30 }];
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);
}
