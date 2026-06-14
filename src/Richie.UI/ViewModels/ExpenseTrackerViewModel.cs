using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Richie.Application.Expenses;
using Richie.Domain.Expenses;

namespace Richie.UI.ViewModels;

public partial class ExpenseTrackerViewModel : ObservableObject
{
    private readonly IExpenseService _expenses;

    public sealed record CategoryFilterOption(ExpenseCategory? Value, string Text);

    public IReadOnlyList<CategoryFilterOption> CategoryFilters { get; } =
        new List<CategoryFilterOption> { new(null, "All categories") }
            .Concat(Enum.GetValues<ExpenseCategory>().Select(c => new CategoryFilterOption(c, ExpenseCategoryNames.Display(c))))
            .ToList();

    [ObservableProperty] private string _currentMonthText = string.Empty;
    [ObservableProperty] private string _monthOverMonthText = string.Empty;
    [ObservableProperty] private string _topCategoryText = string.Empty;
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

    public ExpenseTrackerViewModel(IExpenseService expenses)
    {
        _expenses = expenses;
        Refresh();
    }

    public void Refresh()
    {
        ExpenseDashboard dash = _expenses.GetDashboard();
        CurrentMonthText = Money(dash.CurrentMonthTotal);
        MonthOverMonthText = dash.LastMonthTotal > 0
            ? $"{(dash.MonthOverMonthPercent >= 0 ? "+" : "")}{dash.MonthOverMonthPercent:0.#}% vs last month"
            : "No spending last month to compare";
        TopCategoryText = dash.TopCategoryName ?? "—";
        Breakdown = new ObservableCollection<CategorySpend>(dash.CurrentMonthBreakdown);
        Insights = new ObservableCollection<string>(dash.Insights);
        ApplyFilter();
    }

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

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);
}
