using Richie.Domain.Expenses;

namespace Richie.Application.Expenses;

public sealed record ExpenseInput(
    DateTime Date, decimal Amount, ExpenseCategory Category, string? SpentBy, string? SpentFor, string? Notes);

public sealed record ExpenseSummary(
    Guid Id, DateTime Date, decimal Amount, ExpenseCategory Category, string CategoryName,
    string? SpentBy, string? SpentFor);

/// <summary>Filters for the expense history (all optional).</summary>
public sealed record ExpenseFilter(
    DateTime? From = null,
    DateTime? To = null,
    ExpenseCategory? Category = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? Search = null);

public sealed record CategorySpend(ExpenseCategory Category, string CategoryName, decimal Amount, decimal Percent);

/// <summary>Expense home insights for the current month (PRD §7.1) — conclusions, not raw data.</summary>
public sealed record ExpenseDashboard(
    decimal CurrentMonthTotal,
    decimal LastMonthTotal,
    decimal MonthOverMonthPercent,
    string? TopCategoryName,
    IReadOnlyList<CategorySpend> CurrentMonthBreakdown,
    IReadOnlyList<ExpenseSummary> Recent,
    IReadOnlyList<string> Insights);
