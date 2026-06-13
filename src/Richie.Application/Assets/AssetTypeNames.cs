using Richie.Domain.Assets;

namespace Richie.Application.Assets;

/// <summary>Explicit display names for every asset type (no "Others", PRD §5.3/§21).</summary>
public static class AssetTypeNames
{
    public static string Display(AssetType type) => type switch
    {
        AssetType.MutualFund => "Mutual Funds",
        AssetType.Equity => "Equity / Stocks",
        AssetType.SovereignGoldBond => "Sovereign Gold Bond",
        AssetType.RealEstate => "Real Estate",
        AssetType.DigitalGold => "Digital Gold",
        AssetType.GoldJewellery => "Gold Jewellery",
        AssetType.GuaranteedInvestmentPlan => "Guaranteed Investment Plan",
        _ => type.ToString()
    };
}
