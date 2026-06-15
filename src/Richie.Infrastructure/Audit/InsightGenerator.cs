using Richie.Application.Audit;
using Richie.Application.Expenses;

namespace Richie.Infrastructure.Audit;

/// <summary>
/// Aggregates insights across modules (PRD §18.1): portfolio/coverage findings from the Financial
/// Health Audit plus spending insights from the Expense Tracker. Kept deliberately simple — it
/// reuses each module's own insight logic rather than re-deriving it.
/// </summary>
public sealed class InsightGenerator : IInsightGenerator
{
    private readonly IHealthAuditService _audit;
    private readonly IExpenseService _expenses;

    public InsightGenerator(IHealthAuditService audit, IExpenseService expenses)
    {
        _audit = audit;
        _expenses = expenses;
    }

    public IReadOnlyList<string> Generate(int max = 8) =>
        GenerateDetailed(max).Select(i => i.Text).ToList();

    public IReadOnlyList<Insight> GenerateDetailed(int max = 8)
    {
        var insights = new List<Insight>();

        // Portfolio + coverage (already phrased as actionable conclusions by the audit).
        insights.AddRange(_audit.GetReport().Suggestions.Select(s => new Insight(s, InsightTopic.Portfolio)));

        // Spending trends.
        insights.AddRange(_expenses.GetDashboard().Insights.Select(s => new Insight(s, InsightTopic.Spending)));

        return insights
            .Where(i => !string.IsNullOrWhiteSpace(i.Text))
            .GroupBy(i => i.Text)
            .Select(g => g.First())
            .Take(max)
            .ToList();
    }
}
