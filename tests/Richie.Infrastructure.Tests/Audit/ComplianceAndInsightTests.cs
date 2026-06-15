using Richie.Application.Assets;
using Richie.Application.Audit;
using Richie.Application.Authentication;
using Richie.Application.Expenses;
using Richie.Application.Insurance;
using Richie.Domain.Assets;
using Richie.Domain.Authentication;
using Richie.Domain.Expenses;
using Richie.Domain.Insurance;
using Richie.Domain.Audit;
using Richie.Infrastructure.Assets;
using Richie.Infrastructure.Audit;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Expenses;
using Richie.Infrastructure.Insurance;
using Richie.Infrastructure.Security;
using Richie.Infrastructure.Tests.Helpers;

namespace Richie.Infrastructure.Tests.Audit;

public sealed class ComplianceAndInsightTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly AssetService _assets;
    private readonly InsuranceService _insurance;
    private readonly ExpenseService _expenses;
    private readonly ComplianceService _compliance;
    private readonly InsightGenerator _insights;

    public ComplianceAndInsightTests()
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
        _insurance = new InsuranceService(_db, _session, _clock);
        _expenses = new ExpenseService(_db, _session, _clock);
        var goals = new GoalService(_db, _session, _clock);
        var audit = new HealthAuditService(_assets, goals, _insurance,
            new PlaceholderScoringEngine(), new AgeBandBenchmarkProvider(), _session, _db);
        _compliance = new ComplianceService(audit, _session, _db, _clock);
        _insights = new InsightGenerator(audit, _expenses);
    }

    private void AddAsset(AssetType type, decimal value, DateTime? maturity = null, decimal? guaranteed = null) =>
        _assets.Create(new AssetInput
        {
            Type = type,
            Name = type.ToString(),
            InvestmentStartDate = _clock.UtcNow.AddYears(-1),
            InvestedAmount = value,
            CurrentValue = value,
            InvestmentMode = InvestmentMode.LumpSum,
            MaturityDate = maturity,
            GuaranteedReturnPercent = guaranteed
        });

    [Fact]
    public void Compliance_NoAssets_IsNonCompliant_WithRedAllocation()
    {
        ComplianceReport report = _compliance.GetReport();

        Assert.False(report.IsCompliant);
        Assert.Equal(ComplianceStatus.Red, report.Areas.Single(a => a.Name == "Asset allocation").Status);
        Assert.Equal(ComplianceStatus.Green, report.Areas.Single(a => a.Name == "Guaranteed investments").Status);
        Assert.Empty(report.Gips);
    }

    [Fact]
    public void Compliance_ProtectionCoverage_ReflectsPolicies()
    {
        Assert.Equal(ComplianceStatus.Red,
            _compliance.GetReport().Areas.Single(a => a.Name == "Protection coverage").Status);

        _insurance.Create(new InsurancePolicyInput(InsuranceType.Health, "H", null, null, 1, 1,
            _clock.UtcNow, _clock.UtcNow.AddYears(1), null, null));
        _insurance.Create(new InsurancePolicyInput(InsuranceType.TermLife, "T", null, null, 1, 1,
            _clock.UtcNow, _clock.UtcNow.AddYears(1), null, null));

        Assert.Equal(ComplianceStatus.Green,
            _compliance.GetReport().Areas.Single(a => a.Name == "Protection coverage").Status);
    }

    [Fact]
    public void Compliance_GipNearMaturity_IsAmber_AndTracked()
    {
        AddAsset(AssetType.GuaranteedInvestmentPlan, 1000m, maturity: _clock.UtcNow.AddDays(30), guaranteed: 7.5m);

        ComplianceReport report = _compliance.GetReport();
        ComplianceArea gip = report.Areas.Single(a => a.Name == "Guaranteed investments");
        Assert.Equal(ComplianceStatus.Amber, gip.Status);

        GipStatusRow row = Assert.Single(report.Gips);
        Assert.Equal(7.5m, row.GuaranteedReturnPercent);
        Assert.Contains("Matures in", row.Status);
    }

    [Fact]
    public void Insights_CombinePortfolioAndSpending()
    {
        AddAsset(AssetType.Equity, 1000m);   // 100% equity → benchmark suggestions
        _expenses.Create(new ExpenseInput(_clock.UtcNow, 500m, ExpenseCategory.DiningRestaurants, "Me", "Lunch", null));

        IReadOnlyList<string> insights = _insights.Generate();

        Assert.NotEmpty(insights);
        Assert.True(insights.Count <= 8);
        Assert.Contains(insights, s => s.Contains("Equity"));        // from the audit
        Assert.Contains(insights, s => s.Contains("this month"));    // from expenses
    }

    [Fact]
    public void GenerateDetailed_TagsInsightsWithTheirSourceTopic()
    {
        AddAsset(AssetType.Equity, 1000m);   // 100% equity → portfolio/benchmark suggestions
        _expenses.Create(new ExpenseInput(_clock.UtcNow, 500m, ExpenseCategory.DiningRestaurants, "Me", "Lunch", null));

        IReadOnlyList<Insight> insights = _insights.GenerateDetailed();

        Assert.Contains(insights, i => i.Topic == InsightTopic.Portfolio && i.Text.Contains("Equity"));
        Assert.Contains(insights, i => i.Topic == InsightTopic.Spending && i.Text.Contains("this month"));
        // The text-only overload stays consistent with the detailed one.
        Assert.Equal(_insights.Generate(), insights.Select(i => i.Text).ToList());
    }

    public void Dispose() => _db.Dispose();
}
