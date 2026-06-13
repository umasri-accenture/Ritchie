using Richie.Application.Assets;

namespace Richie.Infrastructure.Assets;

public sealed class ValuationService : IValuationService
{
    public decimal ProfitLoss(decimal invested, decimal currentValue) => currentValue - invested;

    public decimal ProfitLossPercent(decimal invested, decimal currentValue) =>
        invested == 0 ? 0 : Math.Round((currentValue - invested) / invested * 100, 2);
}
