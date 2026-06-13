using Richie.Domain.Assets;

namespace Richie.Application.Assets;

/// <summary>
/// CRUD and portfolio summaries for the signed-in user's assets. Every write is audit-logged.
/// </summary>
public interface IAssetService
{
    IReadOnlyList<AssetSummary> GetAssets();

    /// <summary>The full asset (for the edit form), or null if not found / not the user's.</summary>
    Asset? GetById(Guid id);

    Guid Create(AssetInput input);
    bool Update(Guid id, AssetInput input);
    bool Delete(Guid id);

    /// <summary>Totals + allocation; excluded gold jewellery is omitted (PRD §6.10).</summary>
    PortfolioSummary GetPortfolioSummary();
}
