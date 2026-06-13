namespace Richie.Domain.Assets;

/// <summary>Whether the asset was a one-off purchase or a recurring SIP (PRD §6.3).</summary>
public enum InvestmentMode
{
    LumpSum = 0,
    Sip = 1
}
