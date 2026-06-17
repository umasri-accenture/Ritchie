using Richie.Domain.Notifications;

namespace Richie.Application.Settings;

/// <summary>The current user's settings (PRD §15).</summary>
public sealed record AppSettingsData(
    string Theme,
    int SessionLockMinutes,
    bool IncludeJewelleryInPortfolio,
    string BackupFrequency,
    IReadOnlyCollection<NotificationType> DisabledNotificationTypes,
    DateTime? LastBackupUtc);

/// <summary>Reads and persists per-user settings; defaults are created on first read.</summary>
public interface IAppSettingsService
{
    AppSettingsData Get();
    void Save(AppSettingsData settings);

    /// <summary>True if the given notification type is enabled for the current user.</summary>
    bool IsNotificationEnabled(NotificationType type);

    void SetLastBackup(DateTime utc);

    /// <summary>Reads the saved theme setting before authentication is established.</summary>
    string GetStartupTheme();
}
