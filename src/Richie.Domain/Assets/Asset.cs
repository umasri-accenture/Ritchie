namespace Richie.Domain.Assets;

/// <summary>
/// A single asset holding. One flat entity covers all <see cref="AssetType"/>s: the common
/// fields apply to every type and the nullable type-specific fields are populated only for
/// the relevant type (PRD §6.1/§6.3). <see cref="CurrentValue"/> is stored (manually entered
/// or computed in the form); valuation/P&amp;L is derived from it.
/// </summary>
public class Asset
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Common fields (all types).
    public AssetType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Identifier { get; set; }          // ISIN or stock ticker
    public DateTime InvestmentStartDate { get; set; }
    public decimal InvestedAmount { get; set; }
    public decimal? Quantity { get; set; }           // units / shares / grams
    public decimal? PurchasePricePerUnit { get; set; }
    public decimal CurrentValue { get; set; }
    public DateTime? ValuationDate { get; set; }
    public InvestmentMode InvestmentMode { get; set; }
    public string? Notes { get; set; }

    /// <summary>Gold jewellery may be excluded from portfolio totals/allocation (PRD §6.10).</summary>
    public bool IsExcludedFromPortfolio { get; set; }

    // Type-specific fields (nullable; populated per type).
    public string? Exchange { get; set; }                 // Equity
    public decimal? IssuePrice { get; set; }              // SGB
    public DateTime? MaturityDate { get; set; }           // SGB, GIP
    public string? PlatformName { get; set; }             // Digital Gold
    public string? PropertyAddress { get; set; }          // Real Estate
    public decimal? AreaSquareFeet { get; set; }          // Real Estate
    public decimal? Weight { get; set; }                  // Gold Jewellery
    public string? Purity { get; set; }                   // Gold Jewellery
    public string? AppraiserName { get; set; }            // Gold Jewellery
    public string? PolicyNumber { get; set; }             // GIP
    public decimal? GuaranteedReturnPercent { get; set; } // GIP

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
