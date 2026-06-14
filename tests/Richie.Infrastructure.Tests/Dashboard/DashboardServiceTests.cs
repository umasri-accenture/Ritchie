using Richie.Application.Assets;
using Richie.Application.Dashboard;
using Richie.Application.Authentication;
using Richie.Application.Expenses;
using Richie.Domain.Assets;
using Richie.Domain.Authentication;
using Richie.Domain.Expenses;
using Richie.Domain.Audit;
using Richie.Infrastructure.Assets;
using Richie.Infrastructure.Audit;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Dashboard;
using Richie.Infrastructure.Expenses;
using Richie.Infrastructure.Insurance;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Tests.Helpers;

namespace Richie.Infrastructure.Tests.Dashboard;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly AssetService _assets;
    private readonly ExpenseService _expenses;
    private readonly SipService _sip;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        var hasher = new Argon2PasswordHasher();
        var auth = new AuthService(_db, hasher, _clock);
        auth.Signup(new SignupRequest("U", "u", "password1", 30, "C",
        [
            new(SecurityQuestion.MothersMaidenName, "a"),
            new(SecurityQuestion.CityOfBirth, "b"),
            new(SecurityQuestion.FavouriteFood, "c")
        ]));
        _session.SignIn(auth.Login("u", "password1").UserId!.Value, "U");

        _assets = new AssetService(_db, new ValuationService(), _session, _clock);
        _expenses = new ExpenseService(_db, _session, _clock);
        _sip = new SipService(_db, _session, _clock);
        var goals = new GoalService(_db, _session, _clock);
        var insurance = new InsuranceService(_db, _session, _clock);
        var audit = new HealthAuditService(_assets, goals, insurance,
            new PlaceholderScoringEngine(), new AgeBandBenchmarkProvider(), _session, _db);
        var insights = new InsightGenerator(audit, _expenses);
        _sut = new DashboardService(_assets, _expenses, audit, _sip, insights, _session, _db);
    }

    [Fact]
    public void GetSummary_AggregatesTotals_Insights_AndActivity()
    {
        Guid assetId = _assets.Create(new AssetInput
        {
            Type = AssetType.Equity, Name = "Acme", InvestmentStartDate = _clock.UtcNow.AddYears(-1),
            InvestedAmount = 1000m, CurrentValue = 1200m, InvestmentMode = InvestmentMode.LumpSum
        });
        _expenses.Create(new ExpenseInput(_clock.UtcNow, 500m, ExpenseCategory.DiningRestaurants, "Me", "Lunch", null));

        DashboardSummary s = _sut.GetSummary();

        Assert.Equal(1200m, s.TotalAssets);
        Assert.Equal(1000m, s.TotalInvested);
        Assert.Equal(500m, s.TotalExpensesThisMonth);
        Assert.Equal(200m, s.ProfitLoss);
        Assert.InRange(s.HealthScore, 0, 100);
        Assert.True(s.HealthIsInterim);
        Assert.NotEmpty(s.Insights);
        Assert.NotEmpty(s.RecentActivity);                    // asset + expense creates were audited
        Assert.Contains(s.RecentActivity, a => a.Action == "Added");
    }

    [Fact]
    public void GetSummary_ListsSipsDueWithin30Days()
    {
        Guid assetId = _assets.Create(new AssetInput
        {
            Type = AssetType.MutualFund, Name = "SIP Fund", InvestmentStartDate = _clock.UtcNow,
            InvestedAmount = 0m, CurrentValue = 0m, InvestmentMode = InvestmentMode.Sip
        });
        // Clock is 2026-01-01; day-of-month 15 → next run 2026-01-15 (within 30 days).
        _sip.SaveSchedule(assetId, new SipScheduleInput(true, 5000m, 15, SipFrequency.Monthly, _clock.UtcNow));

        DashboardSummary s = _sut.GetSummary();

        UpcomingSipDto sip = Assert.Single(s.UpcomingSips);
        Assert.Equal("SIP Fund", sip.AssetName);
        Assert.Equal(5000m, sip.Amount);
    }

    public void Dispose() => _db.Dispose();
}
