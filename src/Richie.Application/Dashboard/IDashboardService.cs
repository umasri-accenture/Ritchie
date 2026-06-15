using Richie.Application.Assets;
using Richie.Application.Audit;
using Richie.Application.Expenses;

namespace Richie.Application.Dashboard;

/// <summary>One recent create/update/delete across any module (from the audit log).</summary>
public sealed record ActivityItem(DateTime TimestampUtc, string Module, string Action, string Description);

/// <summary>
/// The Dashboard's aggregated, insight-led view (PRD §5): summary numbers, the Financial Health
/// Score, upcoming SIPs, cross-module insights and a recent-activity feed — assembled from the
/// asset, expense, health-audit and SIP modules.
/// </summary>
public sealed record DashboardSummary(
    decimal TotalAssets,
    decimal TotalInvested,
    decimal TotalExpensesThisMonth,
    decimal ProfitLoss,
    decimal ProfitLossPercent,
    int HealthScore,
    string HealthRating,
    bool HealthIsInterim,
    IReadOnlyList<UpcomingSipDto> UpcomingSips,
    IReadOnlyList<Insight> Insights,
    IReadOnlyList<ActivityItem> RecentActivity,
    IReadOnlyList<PeriodDatum> InvestedHistory,
    decimal InvestedGrowthPercent);

public interface IDashboardService
{
    DashboardSummary GetSummary();
}
