namespace Richie.Application.Audit;

/// <summary>Which module an insight came from — lets the UI deep-link to the relevant page.</summary>
public enum InsightTopic { Portfolio, Spending }

/// <summary>One cross-module insight: the phrased conclusion plus its source topic.</summary>
public sealed record Insight(string Text, InsightTopic Topic);

/// <summary>
/// Produces the cross-module "what does this mean for me?" insights (PRD §18.1) by combining
/// spending trends and portfolio/coverage findings. Consumed by the Dashboard (Phase 7) and module
/// home screens. (Vault-health insights are added once the vault is unlocked.)
/// </summary>
public interface IInsightGenerator
{
    /// <summary>Insight text only (back-compat for callers that don't deep-link).</summary>
    IReadOnlyList<string> Generate(int max = 8);

    /// <summary>Insights with their source topic, for actionable (deep-linking) UI.</summary>
    IReadOnlyList<Insight> GenerateDetailed(int max = 8);
}
