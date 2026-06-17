using Microsoft.EntityFrameworkCore;
using Richie.Application.Abstractions;
using Richie.Application.Assets;
using Richie.Application.Authentication;
using Richie.Domain.Assets;
using Richie.Domain.Auditing;
using Richie.Infrastructure.Auditing;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Assets;

public sealed class AssetService : IAssetService
{
    private const string Module = "Assets";

    private readonly IAppDbContextFactory _factory;
    private readonly IValuationService _valuation;
    private readonly IUserSession _session;
    private readonly IClock _clock;

    public AssetService(IAppDbContextFactory factory, IValuationService valuation, IUserSession session, IClock clock)
    {
        _factory = factory;
        _valuation = valuation;
        _session = session;
        _clock = clock;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public IReadOnlyList<AssetSummary> GetAssets()
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        return db.Assets.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Name)
            .AsEnumerable()
            .Select(ToSummary)
            .ToList();
    }

    public Asset? GetById(Guid id)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        return db.Assets.AsNoTracking().FirstOrDefault(a => a.Id == id && a.UserId == userId);
    }

    public Guid Create(AssetInput input)
    {
        Guid userId = UserId;
        DateTime now = _clock.UtcNow;

        var asset = new Asset { Id = Guid.NewGuid(), UserId = userId, CreatedUtc = now, UpdatedUtc = now };
        Apply(asset, input);

        using RichieDbContext db = _factory.Create();
        db.Assets.Add(asset);
        AuditWriter.Add(db, userId, now, Module, AuditAction.Create, nameof(Asset), asset.Id, $"Added asset '{asset.Name}'.");
        db.SaveChanges();
        return asset.Id;
    }

    public bool Update(Guid id, AssetInput input)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        Asset? asset = db.Assets.FirstOrDefault(a => a.Id == id && a.UserId == userId);
        if (asset is null)
            return false;

        Apply(asset, input);
        asset.UpdatedUtc = _clock.UtcNow;
        AuditWriter.Add(db, userId, asset.UpdatedUtc, Module, AuditAction.Update, nameof(Asset), asset.Id, $"Updated asset '{asset.Name}'.");
        db.SaveChanges();
        return true;
    }

    public bool Delete(Guid id)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        Asset? asset = db.Assets.FirstOrDefault(a => a.Id == id && a.UserId == userId);
        if (asset is null)
            return false;

        db.Assets.Remove(asset);
        AuditWriter.Add(db, userId, _clock.UtcNow, Module, AuditAction.Delete, nameof(Asset), asset.Id, $"Deleted asset '{asset.Name}'.");
        db.SaveChanges();
        return true;
    }

public bool SetPortfolioExclusion(Guid id, bool excluded)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        Asset? asset = db.Assets.FirstOrDefault(a => a.Id == id && a.UserId == userId);
        if (asset is null)
            return false;

        asset.IsExcludedFromPortfolio = excluded;
        asset.UpdatedUtc = _clock.UtcNow;
        string verb = excluded ? "Excluded" : "Included";
        AuditWriter.Add(db, userId, asset.UpdatedUtc, Module, AuditAction.Update, nameof(Asset), asset.Id,
            $"{verb} '{asset.Name}' {(excluded ? "from" : "in")} portfolio valuation.");
        db.SaveChanges();
        return true;
    }

    public int SetAllJewelleryExclusion(bool excluded)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        List<Asset> jewellery = db.Assets
            .Where(a => a.UserId == userId && a.Type == AssetType.GoldJewellery && a.IsExcludedFromPortfolio != excluded)
            .ToList();
        if (jewellery.Count == 0)
            return 0;

        DateTime now = _clock.UtcNow;
        foreach (Asset asset in jewellery)
        {
            asset.IsExcludedFromPortfolio = excluded;
            asset.UpdatedUtc = now;
        }
        AuditWriter.Add(db, userId, now, Module, AuditAction.Update, nameof(Asset), userId,
            $"{(excluded ? "Excluded" : "Included")} all gold jewellery {(excluded ? "from" : "in")} portfolio valuation.");
        db.SaveChanges();
        return jewellery.Count;
    }

    public PortfolioSummary GetPortfolioSummary()
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        List<Asset> included = db.Assets.AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsExcludedFromPortfolio)
            .ToList();

        decimal totalInvested = included.Sum(a => a.InvestedAmount);
        decimal totalCurrent = included.Sum(a => a.CurrentValue);

        List<AllocationSlice> allocation = included
            .GroupBy(a => a.Type)
            .Select(g =>
            {
                decimal value = g.Sum(a => a.CurrentValue);
                decimal percent = totalCurrent == 0 ? 0 : Math.Round(value / totalCurrent * 100, 2);
                return new AllocationSlice(g.Key, AssetTypeNames.Display(g.Key), value, percent);
            })
            .OrderByDescending(s => s.Value)
            .ToList();

        return new PortfolioSummary(
            totalInvested,
            totalCurrent,
            _valuation.ProfitLoss(totalInvested, totalCurrent),
            _valuation.ProfitLossPercent(totalInvested, totalCurrent),
            allocation);
    }

    private AssetSummary ToSummary(Asset a) => new(
        a.Id, a.Type, AssetTypeNames.Display(a.Type), a.Name, a.Identifier,
        a.InvestedAmount, a.CurrentValue,
        _valuation.ProfitLoss(a.InvestedAmount, a.CurrentValue),
        _valuation.ProfitLossPercent(a.InvestedAmount, a.CurrentValue),
        a.InvestmentMode, a.IsExcludedFromPortfolio);

    private static void Apply(Asset a, AssetInput i)
    {
        a.Type = i.Type;
        a.Name = i.Name.Trim();
        a.Identifier = i.Identifier?.Trim();
        a.InvestmentStartDate = i.InvestmentStartDate;
        a.InvestedAmount = i.InvestedAmount;
        a.Quantity = i.Quantity;
        a.PurchasePricePerUnit = i.PurchasePricePerUnit;
        a.CurrentValue = i.CurrentValue;
        a.ValuationDate = i.ValuationDate;
        a.InvestmentMode = i.InvestmentMode;
        a.Notes = i.Notes;
        a.IsExcludedFromPortfolio = i.IsExcludedFromPortfolio;
        a.Exchange = i.Exchange;
        a.IssuePrice = i.IssuePrice;
        a.MaturityDate = i.MaturityDate;
        a.PlatformName = i.PlatformName;
        a.PropertyAddress = i.PropertyAddress;
        a.AreaSquareFeet = i.AreaSquareFeet;
        a.Weight = i.Weight;
        a.Purity = i.Purity;
        a.AppraiserName = i.AppraiserName;
        a.PolicyNumber = i.PolicyNumber;
        a.GuaranteedReturnPercent = i.GuaranteedReturnPercent;
    }
}
