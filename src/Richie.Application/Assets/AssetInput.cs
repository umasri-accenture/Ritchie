using Richie.Domain.Assets;

namespace Richie.Application.Assets;

/// <summary>Editable asset fields supplied by the Add/Edit form. The service maps these
/// onto a new or existing <see cref="Asset"/>.</summary>
public sealed class AssetInput
{
    public AssetType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Identifier { get; init; }
    public DateTime InvestmentStartDate { get; init; }
    public decimal InvestedAmount { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? PurchasePricePerUnit { get; init; }
    public decimal CurrentValue { get; init; }
    public DateTime? ValuationDate { get; init; }
    public InvestmentMode InvestmentMode { get; init; }
    public string? Notes { get; init; }
    public bool IsExcludedFromPortfolio { get; init; }

    // Type-specific
    public string? Exchange { get; init; }
    public decimal? IssuePrice { get; init; }
    public DateTime? MaturityDate { get; init; }
    public string? PlatformName { get; init; }
    public string? PropertyAddress { get; init; }
    public decimal? AreaSquareFeet { get; init; }
    public decimal? Weight { get; init; }
    public string? Purity { get; init; }
    public string? AppraiserName { get; init; }
    public string? PolicyNumber { get; init; }
    public decimal? GuaranteedReturnPercent { get; init; }
}
