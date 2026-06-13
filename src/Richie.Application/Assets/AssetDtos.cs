using Richie.Domain.Assets;

namespace Richie.Application.Assets;

/// <summary>Row shown in the asset list.</summary>
public sealed record AssetSummary(
    Guid Id,
    AssetType Type,
    string TypeName,
    string Name,
    string? Identifier,
    decimal InvestedAmount,
    decimal CurrentValue,
    decimal ProfitLoss,
    decimal ProfitLossPercent,
    InvestmentMode InvestmentMode,
    bool IsExcludedFromPortfolio);

/// <summary>One named slice of the allocation breakdown (never an "Others" bucket).</summary>
public sealed record AllocationSlice(AssetType Type, string TypeName, decimal Value, decimal Percent);

/// <summary>Portfolio totals + allocation, with excluded gold jewellery omitted from totals.</summary>
public sealed record PortfolioSummary(
    decimal TotalInvested,
    decimal TotalCurrentValue,
    decimal TotalProfitLoss,
    decimal TotalProfitLossPercent,
    IReadOnlyList<AllocationSlice> Allocation);
