namespace Richie.Application.Assets;

/// <summary>
/// Derives profit/loss from invested vs current value. (Live market-data valuation is an
/// optional future integration; current value is entered/updated manually for now — PRD §6.3.)
/// </summary>
public interface IValuationService
{
    decimal ProfitLoss(decimal invested, decimal currentValue);
    decimal ProfitLossPercent(decimal invested, decimal currentValue);
}
