namespace Richie.Domain.Assets;

/// <summary>
/// Supported asset types (PRD §6.1). Every type is named explicitly across the app —
/// there is never an "Others" bucket.
/// </summary>
public enum AssetType
{
    MutualFund = 1,
    Equity = 2,
    SovereignGoldBond = 3,
    RealEstate = 4,
    DigitalGold = 5,
    GoldJewellery = 6,
    GuaranteedInvestmentPlan = 7
}
