using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Richie.Application.Common;
using Richie.Application.Expenses;
using Richie.Application.Income;
using Richie.Domain.Expenses;
using Richie.UI.Charts;
using SkiaSharp;

namespace Richie.UI.ViewModels;

public partial class ExpenseTrackerViewModel : ObservableObject
{
    private readonly IExpenseService _expenses;
    private readonly IIncomeService _income;
    private readonly IExpenseAnalyticsService _analytics;

    public Brush SpendBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BrandColors.Danger)!);
    public Brush IncomeBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BrandColors.Success)!);

    public sealed record CategoryFilterOption(ExpenseCategory? Value, string Text);

    public IReadOnlyList<CategoryFilterOption> CategoryFilters { get; } =
        new List<CategoryFilterOption> { new(null, "All categories") }
            .Concat(Enum.GetValues<ExpenseCategory>().Select(c => new CategoryFilterOption(c, ExpenseCategoryNames.Display(c))))
            .ToList();

    [ObservableProperty] private string _currentMonthText = string.Empty;
    [ObservableProperty] private string _incomeThisMonthText = string.Empty;
    [ObservableProperty] private string _monthOverMonthText = string.Empty;
    [ObservableProperty] private string _topCategoryText = string.Empty;
    [ObservableProperty] private ISeries[] _incomeExpenseSeries = [];
    [ObservableProperty] private Axis[] _incomeExpenseAxes = [];
    [ObservableProperty] private ObservableCollection<CategorySpend> _breakdown = [];
    [ObservableProperty] private ObservableCollection<string> _insights = [];
    [ObservableProperty] private ObservableCollection<ExpenseSummary> _items = [];

    // Filters
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private CategoryFilterOption? _selectedCategory;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _minAmountText = string.Empty;
    [ObservableProperty] private string _maxAmountText = string.Empty;

    public ExpenseTrackerViewModel(IExpenseService expenses, IIncomeService income, IExpenseAnalyticsService analytics)
    {
        _expenses = expenses;
        _income = income;
        _analytics = analytics;
        Refresh();
    }

    public void Refresh()
    {
        ExpenseDashboard dash = _expenses.GetDashboard();
        CurrentMonthText = Money(dash.CurrentMonthTotal);
        IncomeThisMonthText = Money(_income.GetMonthlyTotal());
        MonthOverMonthText = dash.LastMonthTotal > 0
            ? $"{(dash.MonthOverMonthPercent >= 0 ? "+" : "")}{dash.MonthOverMonthPercent:0.#}% vs last month"
            : "No spending last month to compare";
        TopCategoryText = dash.TopCategoryName ?? "—";
        Breakdown = new ObservableCollection<CategorySpend>(dash.CurrentMonthBreakdown);
        Insights = new ObservableCollection<string>(dash.Insights);
        BuildIncomeExpenseChart();
        ApplyFilter();
    }

    private void BuildIncomeExpenseChart()
    {
        IReadOnlyList<PeriodDatum> income = _income.GetMonthlyTotals(6);
        IReadOnlyList<PeriodDatum> expense = _analytics.GetMonthlyTotals(6);

        IncomeExpenseSeries =
        [
            TrendLine("Income", income, BrandPalette.Success),
            TrendLine("Expense", expense, BrandPalette.Danger)
        ];
        IncomeExpenseAxes = [new Axis { Labels = income.Select(d => d.Label).ToArray(), LabelsRotation = 0 }];
    }

    private static LineSeries<double> TrendLine(string name, IReadOnlyList<PeriodDatum> data, SKColor color) => new()
    {
        Name = name,
        Values = data.Select(d => (double)d.Amount).ToArray(),
        Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
        GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 2 },
        GeometryFill = new SolidColorPaint(color),
        GeometrySize = 8,
        Fill = null,                 // trend line, not a filled area
        LineSmoothness = 0.6         // smooth trend
    };

    public void ApplyFilter()
    {
        var filter = new ExpenseFilter(
            From: FromDate,
            To: ToDate,
            Category: SelectedCategory?.Value,
            MinAmount: ParseNullable(MinAmountText),
            MaxAmount: ParseNullable(MaxAmountText),
            Search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);

        Items = new ObservableCollection<ExpenseSummary>(_expenses.GetExpenses(filter));
    }

    public void ClearFilter()
    {
        SearchText = string.Empty;
        SelectedCategory = CategoryFilters[0];
        FromDate = null;
        ToDate = null;
        MinAmountText = string.Empty;
        MaxAmountText = string.Empty;
        ApplyFilter();
    }

    public void Delete(Guid id)
    {
        _expenses.Delete(id);
        Refresh();
    }

    private static decimal? ParseNullable(string text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal v) ? v : null;

    private static string Money(decimal value) => Richie.Application.Common.CurrencyFormatter.Format(value);
}
