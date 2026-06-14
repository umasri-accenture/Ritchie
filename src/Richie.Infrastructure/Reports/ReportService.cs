using System.Globalization;
using Richie.Application.Abstractions;
using Richie.Application.Assets;
using Richie.Application.Expenses;
using Richie.Application.Income;
using Richie.Application.Insurance;
using Richie.Application.Reports;
using Richie.Application.Vault;

namespace Richie.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private const string Masked = "••••••••";

    private readonly IAssetService _assets;
    private readonly IValuationService _valuation;
    private readonly IGoalService _goals;
    private readonly IExpenseService _expenses;
    private readonly IExpenseAnalyticsService _expenseAnalytics;
    private readonly IIncomeService _income;
    private readonly IVaultService _vault;
    private readonly IInsuranceService _insurance;
    private readonly IClock _clock;

    public ReportService(
        IAssetService assets, IValuationService valuation, IGoalService goals,
        IExpenseService expenses, IExpenseAnalyticsService expenseAnalytics, IIncomeService income,
        IVaultService vault, IInsuranceService insurance, IClock clock)
    {
        _assets = assets;
        _valuation = valuation;
        _goals = goals;
        _expenses = expenses;
        _expenseAnalytics = expenseAnalytics;
        _income = income;
        _vault = vault;
        _insurance = insurance;
        _clock = clock;
    }

    public ReportContent Build(ReportRequest request)
    {
        var sections = new List<ReportSection>();
        switch (request.Type)
        {
            case ReportType.Assets:
                sections.AddRange(AssetSections());
                break;
            case ReportType.Expenses:
                sections.AddRange(ExpenseSections(request));
                break;
            case ReportType.Vault:
                sections.Add(VaultSection(request.IncludeUnmaskedPasswords));
                break;
            case ReportType.Insurance:
                sections.AddRange(InsuranceSections());
                break;
            case ReportType.FullPortfolio:
                sections.AddRange(AssetSections());
                sections.AddRange(ExpenseSections(request));
                sections.AddRange(InsuranceSections());
                sections.Add(VaultSection(request.IncludeUnmaskedPasswords));
                break;
        }

        string period = request is { From: { } f, To: { } t }
            ? $"{f:d} – {t:d}"
            : "All data";
        return new ReportContent(Title(request.Type), _clock.UtcNow, period, sections);
    }

    private IEnumerable<ReportSection> AssetSections()
    {
        PortfolioSummary p = _assets.GetPortfolioSummary();
        IReadOnlyList<AssetSummary> assets = _assets.GetAssets();

        yield return new ReportSection("Portfolio summary",
        [
            $"Total invested: {Money(p.TotalInvested)}",
            $"Total current value: {Money(p.TotalCurrentValue)}",
            $"Profit / Loss: {Money(p.TotalProfitLoss)} ({p.TotalProfitLossPercent:+0.0;-0.0;0.0}%)",
        ],
        new ReportTable(
            ["Name", "Type", "Invested", "Current", "P&L"],
            assets.Select(a => (IReadOnlyList<string>)
            [
                a.Name, a.TypeName, Money(a.InvestedAmount), Money(a.CurrentValue), Money(a.ProfitLoss)
            ]).ToList()));

        yield return new ReportSection("Allocation by type", [],
            new ReportTable(
                ["Type", "Value", "Share"],
                p.Allocation.Select(s => (IReadOnlyList<string>)
                    [s.TypeName, Money(s.Value), $"{s.Percent:0.#}%"]).ToList()));

        IReadOnlyList<GoalProgress> goals = _goals.GetGoals();
        if (goals.Count > 0)
        {
            yield return new ReportSection("Goal progress", [],
                new ReportTable(
                    ["Goal", "Progress", "Current", "Target"],
                    goals.Select(g => (IReadOnlyList<string>)
                    [
                        g.Name, $"{g.PercentComplete:0.#}%", Money(g.CurrentValue), Money(g.TargetAmount)
                    ]).ToList()));
        }
    }

    private IEnumerable<ReportSection> ExpenseSections(ReportRequest request)
    {
        var filter = new ExpenseFilter(From: request.From, To: request.To);
        IReadOnlyList<ExpenseSummary> expenses = _expenses.GetExpenses(filter);

        var byCategory = expenses
            .GroupBy(e => e.CategoryName)
            .Select(g => (Category: g.Key, Amount: g.Sum(e => e.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        yield return new ReportSection("Expenses by category",
            [$"Total: {Money(expenses.Sum(e => e.Amount))} across {expenses.Count} entries"],
            new ReportTable(
                ["Category", "Amount"],
                byCategory.Select(c => (IReadOnlyList<string>)[c.Category, Money(c.Amount)]).ToList()));

        yield return new ReportSection("Monthly trend (last 12 months)", [],
            new ReportTable(
                ["Month", "Spend"],
                _expenseAnalytics.GetMonthlyTotals(12).Select(m => (IReadOnlyList<string>)
                    [m.Label, Money(m.Amount)]).ToList()));

        IReadOnlyList<IncomeSummary> income = _income.GetRecent(500);
        decimal totalIncome = income.Sum(i => i.Amount);
        var bySource = income
            .GroupBy(i => i.Source)
            .Select(g => (Source: g.Key, Amount: g.Sum(i => i.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();
        yield return new ReportSection("Income",
            [$"Total income: {Money(totalIncome)} across {income.Count} entries",
             $"Net (income − expenses): {Money(totalIncome - expenses.Sum(e => e.Amount))}"],
            new ReportTable(
                ["Source", "Amount"],
                bySource.Select(s => (IReadOnlyList<string>)[s.Source, Money(s.Amount)]).ToList()));
    }

    private ReportSection VaultSection(bool unmasked)
    {
        IReadOnlyList<VaultEntrySummary> entries = _vault.GetEntries();
        var rows = entries.Select(e => (IReadOnlyList<string>)
        [
            e.AccountName, e.Category ?? "", e.LoginId ?? "",
            unmasked ? _vault.RevealPassword(e.Id) ?? "" : Masked
        ]).ToList();

        return new ReportSection("Vault accounts",
            unmasked
                ? ["Passwords are shown UNMASKED — handle this export securely."]
                : ["Passwords are masked."],
            new ReportTable(["Account", "Category", "User ID", "Password"], rows));
    }

    private IEnumerable<ReportSection> InsuranceSections()
    {
        IReadOnlyList<InsurancePolicySummary> policies = _insurance.GetPolicies();
        yield return new ReportSection("Insurance policies",
            [$"{policies.Count} policy(ies). Insurance is tracked separately and is not part of asset allocation."],
            new ReportTable(
                ["Type", "Policy", "Provider", "Coverage", "Premium/yr", "Renewal"],
                policies.Select(p => (IReadOnlyList<string>)
                [
                    p.TypeName, p.PolicyName, p.Provider ?? "", Money(p.CoverageAmount),
                    Money(p.AnnualPremium), p.RenewalDate.ToString("d", CultureInfo.CurrentCulture)
                ]).ToList()));
    }

    private static string Title(ReportType type) => type switch
    {
        ReportType.Assets => "Asset Report",
        ReportType.Expenses => "Expense Report",
        ReportType.Vault => "Password Vault Report",
        ReportType.Insurance => "Insurance Report",
        ReportType.FullPortfolio => "Full Portfolio Report",
        _ => "Report"
    };

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);
}
