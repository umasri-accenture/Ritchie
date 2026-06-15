using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Richie.Application.Abstractions;
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
    private readonly IClock _clock;

    public DashboardService(
        IAssetService assets, IExpenseService expenses, IHealthAuditService health, ISipService sip,
        IInsightGenerator insights, IUserSession session, IAppDbContextFactory factory, IClock clock)
    {
        _assets = assets;
        _expenses = expenses;
        _health = health;
        _sip = sip;
        _insights = insights;
        _session = session;
        _factory = factory;
        _clock = clock;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public DashboardSummary GetSummary()
    {
        Guid userId = UserId;

        PortfolioSummary portfolio = _assets.GetPortfolioSummary();
        ExpenseDashboard expenses = _expenses.GetDashboard();
        HealthAuditReport health = _health.GetReport();
        (IReadOnlyList<PeriodDatum> investedHistory, decimal investedGrowth) = ComputeInvestedHistory(userId);

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
            ReadRecentActivity(userId),
            investedHistory,
            investedGrowth);
    }

    // Reconstructs cumulative invested capital over the last 9 months from real data: each asset's
    // non-SIP (lump-sum) portion counted from its start date, plus posted SIP contributions by date.
    // (True portfolio value over time would need periodic value snapshots, which we don't keep.)
    private (IReadOnlyList<PeriodDatum> History, decimal GrowthPercent) ComputeInvestedHistory(Guid userId)
    {
        const int months = 9;
        using RichieDbContext db = _factory.Create();

        var assets = db.Assets.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Id, a.InvestmentStartDate, a.InvestedAmount })
            .ToList();
        if (assets.Count == 0)
            return ([], 0m);

        var assetIds = assets.Select(a => a.Id).ToHashSet();
        var contributions = db.SipContributions.AsNoTracking()
            .Where(c => assetIds.Contains(c.AssetId))
            .Select(c => new { c.AssetId, c.DateUtc, c.Amount })
            .ToList();

        Dictionary<Guid, decimal> sipByAsset = contributions
            .GroupBy(c => c.AssetId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // Each asset's lump-sum (non-SIP) portion, attributed to its start date.
        var baseEvents = assets
            .Select(a => (Date: a.InvestmentStartDate,
                          Amount: a.InvestedAmount - (sipByAsset.TryGetValue(a.Id, out decimal s) ? s : 0m)))
            .ToList();

        DateTime firstOfThisMonth = new(_clock.UtcNow.Year, _clock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var history = new List<PeriodDatum>(months);
        for (int i = months - 1; i >= 0; i--)
        {
            DateTime monthStart = firstOfThisMonth.AddMonths(-i);
            DateTime cutoff = monthStart.AddMonths(1);   // exclusive end of that month
            decimal cumulative =
                baseEvents.Where(e => e.Date < cutoff).Sum(e => e.Amount) +
                contributions.Where(c => c.DateUtc < cutoff).Sum(c => c.Amount);
            history.Add(new PeriodDatum(monthStart.ToString("MMM", CultureInfo.CurrentCulture), cumulative));
        }

        decimal firstVal = history[0].Amount, lastVal = history[^1].Amount;
        decimal growth = firstVal > 0 ? (lastVal - firstVal) / firstVal * 100m : 0m;
        return (history, growth);
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
