using Richie.Application.Assets;
using Richie.Domain.Assets;
using Richie.Domain.Auditing;
using Richie.Infrastructure.Assets;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Tests.Helpers;

namespace Richie.Infrastructure.Tests.Assets;

public sealed class AssetServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new();
    private readonly UserSession _session = new();
    private readonly AssetService _sut;

    public AssetServiceTests()
    {
        _session.SignIn(Guid.NewGuid(), "Tester");
        _sut = new AssetService(_db, new ValuationService(), _session, _clock);
    }

    private static AssetInput MutualFund(string name = "HDFC Flexicap", decimal invested = 1000, decimal current = 1200) =>
        new()
        {
            Type = AssetType.MutualFund,
            Name = name,
            Identifier = "INF179K01XX0",
            InvestmentStartDate = new DateTime(2025, 1, 1),
            InvestedAmount = invested,
            Quantity = 100,
            PurchasePricePerUnit = 10,
            CurrentValue = current,
            InvestmentMode = InvestmentMode.Sip,
        };

    private static AssetInput Equity(decimal invested = 1000, decimal current = 800) =>
        new()
        {
            Type = AssetType.Equity,
            Name = "Acme Corp",
            Identifier = "ACME",
            Exchange = "NSE",
            InvestmentStartDate = new DateTime(2025, 1, 1),
            InvestedAmount = invested,
            CurrentValue = current,
            InvestmentMode = InvestmentMode.LumpSum,
        };

    private static AssetInput Jewellery(decimal current, bool excluded) =>
        new()
        {
            Type = AssetType.GoldJewellery,
            Name = "Necklace",
            InvestmentStartDate = new DateTime(2025, 1, 1),
            InvestedAmount = 500,
            CurrentValue = current,
            Weight = 20,
            Purity = "22K",
            IsExcludedFromPortfolio = excluded,
            InvestmentMode = InvestmentMode.LumpSum,
        };

    [Fact]
    public void Create_AddsAsset_WithComputedProfitLoss_AndAuditLog()
    {
        _sut.Create(MutualFund(invested: 1000, current: 1200));

        AssetSummary summary = Assert.Single(_sut.GetAssets());
        Assert.Equal("Mutual Funds", summary.TypeName);
        Assert.Equal(200m, summary.ProfitLoss);
        Assert.Equal(20m, summary.ProfitLossPercent);

        using var db = _db.Create();
        AuditLog audit = Assert.Single(db.AuditLogs.ToList());
        Assert.Equal(AuditAction.Create, audit.Action);
        Assert.Equal("Assets", audit.Module);
    }

    [Fact]
    public void Update_ChangesFields_AndLogsAudit()
    {
        Guid id = _sut.Create(MutualFund());

        Assert.True(_sut.Update(id, MutualFund(name: "Renamed", invested: 1000, current: 900)));

        AssetSummary summary = Assert.Single(_sut.GetAssets());
        Assert.Equal("Renamed", summary.Name);
        Assert.Equal(-100m, summary.ProfitLoss);

        using var db = _db.Create();
        Assert.Contains(db.AuditLogs.ToList(), a => a.Action == AuditAction.Update);
    }

    [Fact]
    public void Delete_RemovesAsset_AndLogsAudit()
    {
        Guid id = _sut.Create(MutualFund());

        Assert.True(_sut.Delete(id));
        Assert.Empty(_sut.GetAssets());

        using var db = _db.Create();
        Assert.Contains(db.AuditLogs.ToList(), a => a.Action == AuditAction.Delete);
    }

    [Fact]
    public void GetAssets_IsScopedToCurrentUser()
    {
        _sut.Create(MutualFund());

        _session.SignIn(Guid.NewGuid(), "Someone Else");
        Assert.Empty(_sut.GetAssets());
    }

    [Fact]
    public void PortfolioSummary_NamesEveryType_WithCorrectPercentages()
    {
        _sut.Create(MutualFund(invested: 1000, current: 1200)); // 60%
        _sut.Create(Equity(invested: 1000, current: 800));      // 40%

        PortfolioSummary summary = _sut.GetPortfolioSummary();

        Assert.Equal(2000m, summary.TotalCurrentValue);
        Assert.Equal(2, summary.Allocation.Count);
        Assert.All(summary.Allocation, slice => Assert.False(string.IsNullOrWhiteSpace(slice.TypeName)));
        Assert.DoesNotContain(summary.Allocation, slice => slice.TypeName.Contains("Other"));
        Assert.Equal(60m, summary.Allocation.Single(s => s.Type == AssetType.MutualFund).Percent);
        Assert.Equal(40m, summary.Allocation.Single(s => s.Type == AssetType.Equity).Percent);
    }

    [Fact]
    public void PortfolioSummary_ExcludesFlaggedJewellery_ButListStillShowsIt()
    {
        _sut.Create(MutualFund(invested: 1000, current: 1000));
        _sut.Create(Jewellery(current: 5000, excluded: true));

        PortfolioSummary summary = _sut.GetPortfolioSummary();
        Assert.Equal(1000m, summary.TotalCurrentValue);
        Assert.Single(summary.Allocation);
        Assert.Equal(AssetType.MutualFund, summary.Allocation[0].Type);

        // The excluded jewellery still appears in the asset list, flagged.
        Assert.Contains(_sut.GetAssets(), a => a.Type == AssetType.GoldJewellery && a.IsExcludedFromPortfolio);
    }

    [Fact]
    public void SetPortfolioExclusion_TogglesJewelleryInOrOutOfTotals()
    {
        _sut.Create(MutualFund(invested: 1000, current: 1000));
        Guid jewelleryId = _sut.Create(Jewellery(current: 5000, excluded: false));

        Assert.Equal(6000m, _sut.GetPortfolioSummary().TotalCurrentValue);

        Assert.True(_sut.SetPortfolioExclusion(jewelleryId, excluded: true));
        Assert.Equal(1000m, _sut.GetPortfolioSummary().TotalCurrentValue);

        Assert.True(_sut.SetPortfolioExclusion(jewelleryId, excluded: false));
        Assert.Equal(6000m, _sut.GetPortfolioSummary().TotalCurrentValue);
    }

    [Fact]
    public void SetPortfolioExclusion_WorksForNonJewellery()
    {
        Guid mfId = _sut.Create(MutualFund());

        Assert.True(_sut.SetPortfolioExclusion(mfId, excluded: true));
    }

    public void Dispose() => _db.Dispose();
}
