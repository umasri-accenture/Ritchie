using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Richie.Application.Assets;
using Richie.Application.Audit;
using Richie.Application.Authentication;
using Richie.Application.Dashboard;
using Richie.Application.Expenses;
using Richie.Application.Income;
using Richie.UI.Charts;
using SkiaSharp;

namespace Richie.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboard;
    private readonly IAssetService _assets;
    private readonly IExpenseAnalyticsService _analytics;
    private readonly IIncomeService _income;
    private readonly IUserSession _session;

    public sealed record UpcomingSipRow(string AssetName, string AmountText, string DueText, string FrequencyText);
    public sealed record ActivityRow(string DateText, string Module, string Action, string Description);
    public sealed record InsightRow(string Text, string ActionLabel, InsightTopic Topic);
    public sealed record AllocationLegendItem(string Label, Brush Swatch);

    [ObservableProperty] private string _totalAssetsText = "—";
    [ObservableProperty] private string _totalInvestedText = "—";
    [ObservableProperty] private string _totalExpensesText = "—";
    [ObservableProperty] private string _profitLossText = "—";
    [ObservableProperty] private Brush _profitLossBrush = Brushes.Gray;
    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private string _healthRating = string.Empty;
    [ObservableProperty] private Brush _healthBrush = Brushes.Gray;
    [ObservableProperty] private bool _healthIsInterim;

    [ObservableProperty] private ISeries[] _allocationSeries = [];
    [ObservableProperty] private ObservableCollection<AllocationLegendItem> _allocationLegend = [];
    [ObservableProperty] private bool _hasAssets;
    [ObservableProperty] private bool _noAssets;

    [ObservableProperty] private ISeries[] _incomeExpenseSeries = [];
    [ObservableProperty] private Axis[] _incomeExpenseAxes = [];
    [ObservableProperty] private ISeries[] _investmentSeries = [];
    [ObservableProperty] private Axis[] _investmentAxes = [];
    [ObservableProperty] private string _investmentGrowthText = string.Empty;
    [ObservableProperty] private ISeries[] _expenseBreakdownSeries = [];
    [ObservableProperty] private Axis[] _expenseBreakdownAxes = [];

    // Hero greeting (top of the dashboard).
    [ObservableProperty] private string _heroDateText = string.Empty;
    [ObservableProperty] private string _greetingText = string.Empty;
    [ObservableProperty] private string _portfolioInsightText = string.Empty;

    [ObservableProperty] private ObservableCollection<UpcomingSipRow> _upcomingSips = [];
    [ObservableProperty] private bool _hasUpcomingSips;
    [ObservableProperty] private bool _noUpcomingSips;
    [ObservableProperty] private ObservableCollection<InsightRow> _insights = [];
    [ObservableProperty] private bool _noInsights;
    [ObservableProperty] private ObservableCollection<ActivityRow> _recentActivity = [];
    [ObservableProperty] private bool _hasActivity;
    [ObservableProperty] private bool _noActivity;

    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F));

    public DashboardViewModel(IDashboardService dashboard, IAssetService assets,
        IExpenseAnalyticsService analytics, IIncomeService income, IUserSession session)
    {
        _dashboard = dashboard;
        _assets = assets;
        _analytics = analytics;
        _income = income;
        _session = session;
    }

    public void Load()
    {
        DashboardSummary s = _dashboard.GetSummary();

        BuildHero(s);

        TotalAssetsText = Money(s.TotalAssets);
        TotalInvestedText = Money(s.TotalInvested);
        TotalExpensesText = Money(s.TotalExpensesThisMonth);
        ProfitLossText = $"{Money(s.ProfitLoss)} ({s.ProfitLossPercent:+0.0;-0.0;0.0}%)";
        ProfitLossBrush = s.ProfitLoss < 0 ? Red : Green;
        HealthScore = s.HealthScore;
        HealthRating = s.HealthRating;
        HealthBrush = s.HealthScore >= 80 ? Green : s.HealthScore >= 60 ? Amber : Red;
        HealthIsInterim = s.HealthIsInterim;

        UpcomingSips = new ObservableCollection<UpcomingSipRow>(s.UpcomingSips.Select(u =>
            new UpcomingSipRow(u.AssetName, Money(u.Amount), u.DueDate.ToString("d", CultureInfo.CurrentCulture), u.Frequency.ToString())));
        HasUpcomingSips = UpcomingSips.Count > 0;
        NoUpcomingSips = !HasUpcomingSips;

        // Keep the dashboard focused — show the top few insights horizontally; the rest live on each module.
        Insights = new ObservableCollection<InsightRow>(
            s.Insights.Take(3).Select(i => new InsightRow(i.Text, ActionLabel(i.Topic), i.Topic)));
        NoInsights = Insights.Count == 0;

        RecentActivity = new ObservableCollection<ActivityRow>(s.RecentActivity.Select(a =>
            new ActivityRow(a.TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), a.Module, a.Action, a.Description)));
        HasActivity = RecentActivity.Count > 0;
        NoActivity = !HasActivity;

        BuildCharts(s);
    }

    private void BuildCharts(DashboardSummary s)
    {
        // Asset allocation — donut with the share shown in the legend (e.g. "Equity 24%").
        IReadOnlyList<AllocationSlice> allocation = _assets.GetPortfolioSummary().Allocation;
        HasAssets = allocation.Count > 0;
        NoAssets = !HasAssets;
        AllocationSeries = allocation
            .Select((a, i) => (ISeries)new PieSeries<double>
                { Values = [(double)a.Value], Name = a.TypeName, InnerRadius = 45, Fill = BrandPalette.Categorical(i) })
            .ToArray();
        // Custom legend (matches slice colours) so it lays out inside the card instead of the
        // built-in legend overflowing it.
        AllocationLegend = new ObservableCollection<AllocationLegendItem>(
            allocation.Select((a, i) => new AllocationLegendItem($"{a.TypeName}  {a.Percent:0.#}%", BrandPalette.MediaBrush(i))));

        // Income vs Expense — filled area trend over the last 9 months.
        IReadOnlyList<PeriodDatum> income = _income.GetMonthlyTotals(9);
        IReadOnlyList<PeriodDatum> expense = _analytics.GetMonthlyTotals(9);
        IncomeExpenseSeries = [Area("Income", income, BrandPalette.Success), Area("Expense", expense, BrandPalette.Danger)];
        IncomeExpenseAxes = [new Axis { Labels = income.Select(d => d.Label).ToArray(), LabelsRotation = 0 }];

        // Investment growth — invested capital over time (line + period-growth badge).
        InvestmentSeries =
        [
            new LineSeries<double>
            {
                Name = "Invested",
                Values = s.InvestedHistory.Select(d => (double)d.Amount).ToArray(),
                Stroke = new SolidColorPaint(BrandPalette.Primary) { StrokeThickness = 2.5f },
                GeometryStroke = new SolidColorPaint(BrandPalette.Primary) { StrokeThickness = 2f },
                GeometryFill = new SolidColorPaint(BrandPalette.Primary),
                GeometrySize = 7,
                Fill = null,
                LineSmoothness = 0.5
            }
        ];
        InvestmentAxes = [new Axis { Labels = s.InvestedHistory.Select(d => d.Label).ToArray() }];
        InvestmentGrowthText = $"{(s.InvestedGrowthPercent >= 0 ? "▲ +" : "▼ ")}{s.InvestedGrowthPercent:0.#}% YoY";

        // Expense breakdown — this month's spend by category (named categories only, no "Others").
        var categories = _analytics.GetCategoryDistribution(AnalyticsPeriod.ThisMonth)
            .Where(c => c.Amount > 0).ToList();
        ExpenseBreakdownSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Spend",
                Values = categories.Select(c => (double)c.Amount).ToArray(),
                Fill = new SolidColorPaint(BrandPalette.Primary)
            }
        ];
        ExpenseBreakdownAxes = [new Axis { Labels = categories.Select(c => c.CategoryName).ToArray(), LabelsRotation = 20 }];
    }

    private static LineSeries<double> Area(string name, IReadOnlyList<PeriodDatum> data, SKColor color) => new()
    {
        Name = name,
        Values = data.Select(d => (double)d.Amount).ToArray(),
        Stroke = new SolidColorPaint(color) { StrokeThickness = 2f },
        GeometryFill = null,
        GeometryStroke = null,
        GeometrySize = 0,
        Fill = new SolidColorPaint(color.WithAlpha(50)),
        LineSmoothness = 0.6
    };

    private void BuildHero(DashboardSummary s)
    {
        DateTime now = DateTime.Now;
        HeroDateText = now.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture).ToUpper(CultureInfo.CurrentCulture);

        string name = string.IsNullOrWhiteSpace(_session.FullName) ? "there" : _session.FullName!.Split(' ')[0];
        string partOfDay = now.Hour < 12 ? "morning" : now.Hour < 17 ? "afternoon" : "evening";
        GreetingText = $"Good {partOfDay}, {name} \U0001F44B";

        string pnl = s.ProfitLossPercent >= 0
            ? $"up {s.ProfitLossPercent:0.0}%"
            : $"down {Math.Abs(s.ProfitLossPercent):0.0}%";
        int sips = s.UpcomingSips.Count;
        int insights = s.Insights.Count;
        PortfolioInsightText =
            $"Portfolio {pnl} overall · {sips} upcoming SIP{(sips == 1 ? "" : "s")} · " +
            $"{insights} insight{(insights == 1 ? "" : "s")} to review.";
    }

    private static string ActionLabel(InsightTopic topic) => topic switch
    {
        InsightTopic.Spending => "Analyse",
        _ => "Review"
    };

    private static string Money(decimal value) => Richie.Application.Common.CurrencyFormatter.Format(value);
}
