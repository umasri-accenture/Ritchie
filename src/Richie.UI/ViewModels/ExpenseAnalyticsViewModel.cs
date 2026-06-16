using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Richie.Application.Expenses;
using Richie.Domain.Expenses;
using Richie.UI.Charts;

namespace Richie.UI.ViewModels;

public partial class ExpenseAnalyticsViewModel : ObservableObject
{
    private readonly IExpenseAnalyticsService _analytics;
    private readonly IExpenseBudgetService _budgets;

    public sealed record PeriodOption(AnalyticsPeriod Value, string Text);

    public sealed class BudgetEditRow
    {
        public ExpenseCategory Category { get; init; }
        public string Name { get; init; } = string.Empty;
        public string LimitText { get; set; } = string.Empty;
        public string ActualText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public Brush StatusBrush { get; init; } = Brushes.Gray;
    }

    public IReadOnlyList<PeriodOption> Periods { get; } =
    [
        new(AnalyticsPeriod.ThisMonth, "This month"),
        new(AnalyticsPeriod.ThisQuarter, "This quarter"),
        new(AnalyticsPeriod.ThisYear, "This year"),
        new(AnalyticsPeriod.AllTime, "All time"),
    ];

    [ObservableProperty] private AnalyticsPeriod _selectedPeriod = AnalyticsPeriod.ThisMonth;
    [ObservableProperty] private ISeries[] _categorySeries = [];
    [ObservableProperty] private bool _hasCategoryData;
    [ObservableProperty] private ISeries[] _monthlySeries = [];
    [ObservableProperty] private Axis[] _monthlyAxes = [];
    [ObservableProperty] private ISeries[] _yearlySeries = [];
    [ObservableProperty] private Axis[] _yearlyAxes = [];
    [ObservableProperty] private bool _hasYearlyData;
    [ObservableProperty] private ObservableCollection<BudgetEditRow> _budgetRows = [];

    public ExpenseAnalyticsViewModel(IExpenseAnalyticsService analytics, IExpenseBudgetService budgets)
    {
        _analytics = analytics;
        _budgets = budgets;
        Refresh();
    }

    public void Refresh()
    {
        BuildCategory();
        BuildMonthly();
        BuildYearly();
        BuildBudgets();
    }

    public void SaveBudgets()
    {
        var limits = new Dictionary<ExpenseCategory, decimal>();
        foreach (BudgetEditRow row in BudgetRows)
            limits[row.Category] =
                decimal.TryParse(row.LimitText, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal v) ? v : 0;

        _budgets.SetBudgets(limits);
        Refresh();
    }

    partial void OnSelectedPeriodChanged(AnalyticsPeriod value) => BuildCategory();

    private void BuildCategory()
    {
        IReadOnlyList<CategoryDatum> data = _analytics.GetCategoryDistribution(SelectedPeriod);
        HasCategoryData = data.Count > 0;
        CategorySeries = data
            .Select((d, i) => (ISeries)new PieSeries<double>
                { Values = [(double)d.Amount], Name = d.CategoryName, InnerRadius = 50, Fill = BrandPalette.Categorical(i) })
            .ToArray();
    }

    private void BuildMonthly()
    {
        IReadOnlyList<PeriodDatum> data = _analytics.GetMonthlyTotals(12);
        MonthlySeries = [new ColumnSeries<double>
            { Values = data.Select(d => (double)d.Amount).ToArray(), Name = "Spend", Fill = BrandPalette.Solid(BrandPalette.Primary) }];
        MonthlyAxes = [new Axis { Labels = data.Select(d => d.Label).ToArray(), LabelsRotation = 0 }];
    }

    private void BuildYearly()
    {
        IReadOnlyList<PeriodDatum> data = _analytics.GetYearlyTotals();
        HasYearlyData = data.Count > 0;
        YearlySeries = [new ColumnSeries<double>
            { Values = data.Select(d => (double)d.Amount).ToArray(), Name = "Spend", Fill = BrandPalette.Solid(BrandPalette.Primary) }];
        YearlyAxes = [new Axis { Labels = data.Select(d => d.Label).ToArray(), LabelsRotation = 0 }];
    }

    private void BuildBudgets()
    {
        BudgetRows = new ObservableCollection<BudgetEditRow>(_budgets.GetAnalysis().Select(r => new BudgetEditRow
        {
            Category = r.Category,
            Name = r.CategoryName,
            LimitText = r.MonthlyLimit > 0 ? r.MonthlyLimit.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty,
            ActualText = Richie.Application.Common.CurrencyFormatter.Format(r.ActualThisMonth),
            StatusText = StatusText(r),
            StatusBrush = StatusBrush(r.Status)
        }));
    }

    private static string StatusText(BudgetRow r) => r.Status switch
    {
        BudgetStatus.Unset => "No budget set",
        BudgetStatus.Good => $"On track ({r.Percent:0.#}%)",
        BudgetStatus.Warning => $"Near limit ({r.Percent:0.#}%)",
        BudgetStatus.Over => $"Over budget ({r.Percent:0.#}%)",
        _ => string.Empty
    };

    private static Brush StatusBrush(BudgetStatus status)
    {
        // Make "good/over" visually neutral (no strong green/red).
        // Warning stays orange; Unset stays gray.
        string hex = status switch
        {
            BudgetStatus.Good => "#2563EB",     // blue
            BudgetStatus.Warning => "#B26A00",  // orange
            BudgetStatus.Over => "#7C3AED",     // violet
            _ => "#888888"
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
