using Microsoft.EntityFrameworkCore;
using Richie.Application.Assets;
using Richie.Application.Audit;
using Richie.Application.Authentication;
using Richie.Application.Dashboard;
using Richie.Application.Expenses;
using Richie.Domain.Auditing;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private const int RecentActivityCount = 20;

    private readonly IAssetService _assets;
    private readonly IExpenseService _expenses;
    private readonly IHealthAuditService _health;
    private readonly ISipService _sip;
    private readonly IInsightGenerator _insights;
    private readonly IUserSession _session;
    private readonly IAppDbContextFactory _factory;

    public DashboardService(
        IAssetService assets, IExpenseService expenses, IHealthAuditService health, ISipService sip,
        IInsightGenerator insights, IUserSession session, IAppDbContextFactory factory)
    {
        _assets = assets;
        _expenses = expenses;
        _health = health;
        _sip = sip;
        _insights = insights;
        _session = session;
        _factory = factory;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public DashboardSummary GetSummary()
    {
        Guid userId = UserId;

        PortfolioSummary portfolio = _assets.GetPortfolioSummary();
        ExpenseDashboard expenses = _expenses.GetDashboard();
        HealthAuditReport health = _health.GetReport();

        return new DashboardSummary(
            portfolio.TotalCurrentValue,
            portfolio.TotalInvested,
            expenses.CurrentMonthTotal,
            portfolio.TotalProfitLoss,
            portfolio.TotalProfitLossPercent,
            health.HealthScore,
            health.HealthRating,
            health.ScoresAreInterim,
            _sip.GetUpcomingSips(30),
            _insights.GenerateDetailed(),
            ReadRecentActivity(userId));
    }

    private IReadOnlyList<ActivityItem> ReadRecentActivity(Guid userId)
    {
        using RichieDbContext db = _factory.Create();
        return db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.TimestampUtc)
            .Take(RecentActivityCount)
            .ToList()
            .Select(a => new ActivityItem(a.TimestampUtc, a.Module, ActionVerb(a.Action), a.Description))
            .ToList();
    }

    private static string ActionVerb(AuditAction action) => action switch
    {
        AuditAction.Create => "Added",
        AuditAction.Update => "Updated",
        AuditAction.Delete => "Deleted",
        _ => action.ToString()
    };
}
