using System.Collections.ObjectModel;
using System.Windows.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using Richie.Application.Assets;
using Richie.Application.Common;
using Richie.Application.Settings;
using Richie.Domain.Notifications;
using Richie.UI.Services;
using Wpf.Ui.Appearance;

namespace Richie.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settings;
    private readonly IAssetService _assets;
    private readonly InactivityLockService _lock;

    public partial class NotificationPref : ObservableObject
    {
        public NotificationType Type { get; init; }
        public string Name { get; init; } = string.Empty;
        [ObservableProperty] private bool _isEnabled = true;
    }

    public IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"];
    public IReadOnlyList<string> BackupFrequencies { get; } = ["Manual", "Daily", "Weekly"];
    public IReadOnlyList<int> LockMinutesOptions { get; } = [1, 2, 5, 10, 15, 30, 60];

    [ObservableProperty] private string _selectedTheme = "System";
    [ObservableProperty] private int _sessionLockMinutes = 5;
    [ObservableProperty] private bool _includeJewelleryInPortfolio = true;
    [ObservableProperty] private string _backupFrequency = "Manual";
    [ObservableProperty] private ObservableCollection<NotificationPref> _notificationPrefs = [];

    public string EncryptionStatus =>
        "On — the database is SQLCipher (AES-256) encrypted and vault passwords use AES-256-GCM. Encryption cannot be disabled.";

    public SettingsViewModel(IAppSettingsService settings, IAssetService assets, InactivityLockService @lock)
    {
        _settings = settings;
        _assets = assets;
        _lock = @lock;
        Load();
    }

    public void Load()
    {
        AppSettingsData data = _settings.Get();
        SelectedTheme = data.Theme;
        SessionLockMinutes = data.SessionLockMinutes;
        IncludeJewelleryInPortfolio = data.IncludeJewelleryInPortfolio;
        BackupFrequency = data.BackupFrequency;
        NotificationPrefs = new ObservableCollection<NotificationPref>(
            Enum.GetValues<NotificationType>().Select(t => new NotificationPref
            {
                Type = t,
                Name = Friendly(t),
                IsEnabled = !data.DisabledNotificationTypes.Contains(t)
            }));
    }

    public void Save()
    {
        var disabled = NotificationPrefs.Where(p => !p.IsEnabled).Select(p => p.Type).ToList();
        _settings.Save(new AppSettingsData(
            SelectedTheme, SessionLockMinutes, IncludeJewelleryInPortfolio, BackupFrequency, disabled,
            _settings.Get().LastBackupUtc));

        ApplyTheme(SelectedTheme);
        _lock.Timeout = TimeSpan.FromMinutes(SessionLockMinutes);
        _assets.SetAllJewelleryExclusion(!IncludeJewelleryInPortfolio);
    }

    /// <summary>Detects the system theme preference from Windows registry.</summary>
    public static string GetSystemTheme()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key?.GetValue("AppsUseLightTheme") is int value && value == 0)
                    return "Dark";
            }
        }
        catch
        {
            // If registry access fails, default to Light
        }
        return "Light";
    }

    public static void ApplyTheme(string theme)
    {
        // If "System" is selected, detect actual system preference
        string actualTheme = theme == "System" ? GetSystemTheme() : theme;

        switch (actualTheme)
        {
            case "Dark": ApplicationThemeManager.Apply(ApplicationTheme.Dark); break;
            case "Light": ApplicationThemeManager.Apply(ApplicationTheme.Light); break;
            default: ApplicationThemeManager.ApplySystemTheme(); break;
        }

        // ApplicationThemeManager.Apply resets the accent to the system accent, so re-brand afterwards.
        ApplyBrandAccent();
    }

    /// <summary>Applies a professional brand accent across the app (buttons, nav highlight, focus rings).</summary>
    public static void ApplyBrandAccent()
    {
        ApplicationTheme theme = ApplicationThemeManager.GetAppTheme();
        if (theme is ApplicationTheme.Unknown)
            theme = ApplicationTheme.Light;

        // Use a professional blue/teal accent instead of red for modern UI
        var accent = (Color)ColorConverter.ConvertFromString("#3B82F6")!; // Professional blue
        ApplicationAccentColorManager.Apply(accent, theme);
    }

    private static string Friendly(NotificationType type) => type switch
    {
        NotificationType.SipReminder => "SIP reminders",
        NotificationType.SipPosted => "SIP posted",
        NotificationType.RecurringExpense => "Recurring expenses",
        NotificationType.InsuranceRenewal => "Insurance renewals",
        NotificationType.PortfolioHealthAlert => "Portfolio health alerts",
        NotificationType.ExpenseAlert => "Expense / budget alerts",
        NotificationType.UploadStatus => "Bulk-upload status",
        NotificationType.GipMaturity => "Guaranteed-plan maturity",
        _ => type.ToString()
    };
}
