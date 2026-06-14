using Richie.Domain.Assets;

namespace Richie.Application.Assets;

/// <summary>Editable SIP configuration for an asset.</summary>
public sealed record SipScheduleInput(
    bool IsEnabled, decimal Amount, int DayOfMonth, SipFrequency Frequency, DateTime StartDate);

public sealed record SipScheduleDto(
    bool IsEnabled, decimal Amount, int DayOfMonth, SipFrequency Frequency,
    DateTime StartDate, DateTime NextRunDateUtc, DateTime? LastRunUtc);

public sealed record SipContributionDto(DateTime DateUtc, decimal Amount);

/// <summary>An enabled SIP instalment falling due soon (across all of the user's assets).</summary>
public sealed record UpcomingSipDto(
    Guid AssetId, string AssetName, decimal Amount, DateTime DueDate, SipFrequency Frequency);
