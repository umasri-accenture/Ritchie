using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Richie.Application.Authentication;
using Richie.Application.Notifications;
using Richie.UI.Services;
using Richie.UI.Views.Pages;
using Wpf.Ui.Controls;

namespace Richie.UI;

/// <summary>
/// The application shell: Fluent (Mica) window with a collapsible sidebar, a topbar
/// (notifications + profile popovers), and the first-run tour overlay.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly InactivityLockService _inactivity;
    private readonly TourService _tour;
    private readonly INotificationService _notifications;

    // Placeholder tour copy — final wording is a team open item (PRD §22).
    private static readonly (string Title, string Body)[] TourSteps =
    [
        ("Welcome to Richie", "A quick tour to show you around. Everything you enter stays encrypted on this device — nothing leaves your machine."),
        ("Navigate with the sidebar", "Use the left navigation to move between modules: Assets, Expenses, the Password Vault, the Financial Health Audit, Reports and Export."),
        ("Insights, not just numbers", "Each screen surfaces conclusions about your finances — what the numbers mean for you — rather than raw tables."),
        ("Your profile & settings", "The top-right icons open Notifications and your Profile, where Settings, Help and Log out live."),
    ];

    private int _tourIndex;

    /// <summary>Raised when the user logs out; App returns to the auth window.</summary>
    public event EventHandler? LogoutRequested;

    public MainWindow(IUserSession session, InactivityLockService inactivity, TourService tour,
        INotificationService notifications, Wpf.Ui.ISnackbarService snackbar)
    {
        InitializeComponent();
        _inactivity = inactivity;
        _tour = tour;
        _notifications = notifications;
        snackbar.SetSnackbarPresenter(RootSnackbar);

        ProfileName.Text = session.FullName ?? "Signed in";

        _tour.StartRequested += StartTour;
        Closed += (_, _) => _tour.StartRequested -= StartTour;

        PreviewMouseMove += (_, _) => _inactivity.Notify();
        PreviewMouseDown += (_, _) => _inactivity.Notify();
        PreviewKeyDown += (_, _) => _inactivity.Notify();

        Loaded += (_, _) =>
        {
            // Apply the user's saved theme preference when main window loads
            var app = (App)System.Windows.Application.Current;
            var settingsService = app.Services.GetRequiredService<Richie.Application.Settings.IAppSettingsService>();
            string savedTheme = settingsService.Get().Theme;
            ViewModels.SettingsViewModel.ApplyTheme(savedTheme);
            
            RootNavigation.Navigate(typeof(DashboardPage));
            UpdateBadge();
            PopulateStatusBar();

            PositionPaneGrip();
            // Keep the resize grip aligned (and hidden) as the pane is collapsed/expanded via the toggle.
            System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(NavigationView.IsPaneOpenProperty, typeof(NavigationView))
                .AddValueChanged(RootNavigation, (_, _) => PositionPaneGrip());
        };
    }

    private const double MinPaneWidth = 150;
    private const double MaxPaneWidth = 420;

    private void OnPaneResize(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        RootNavigation.OpenPaneLength =
            Math.Clamp(RootNavigation.OpenPaneLength + e.HorizontalChange, MinPaneWidth, MaxPaneWidth);
        PositionPaneGrip();
    }

    // Park the grip on the pane's right edge; only meaningful while the pane is open.
    private void PositionPaneGrip()
    {
        PaneResizeGrip.Margin = new Thickness(RootNavigation.OpenPaneLength - (PaneResizeGrip.Width / 2), 0, 0, 0);
        PaneResizeGrip.Visibility = RootNavigation.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    // The bottom status bar is informational (encryption · storage · last backup · auto-lock).
    private void PopulateStatusBar()
    {
        try
        {
            var services = ((App)System.Windows.Application.Current).Services;
            Richie.Application.Settings.AppSettingsData settings =
                services.GetRequiredService<Richie.Application.Settings.IAppSettingsService>().Get();
            long bytes = services.GetRequiredService<Richie.Application.Profile.IProfileService>().Get().StorageBytes;

            StatusStorage.Text = $"Storage: {FormatBytes(bytes)}";
            StatusBackup.Text = settings.LastBackupUtc is { } backup
                ? $"Last backup: {backup.ToLocalTime():d}"
                : "Last backup: never";
            StatusLock.Text = $"Auto-lock: {settings.SessionLockMinutes} min";
        }
        catch
        {
            // Status bar is non-essential — never let it block the shell.
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    private void UpdateBadge()
    {
        int unread = _notifications.GetUnreadCount();
        BadgeText.Text = unread > 99 ? "99+" : unread.ToString();
        BadgeBorder.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshNotifications()
    {
        IReadOnlyList<NotificationDto> recent = _notifications.GetRecent(20);
        NotificationsList.ItemsSource = recent;
        NotificationsEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateBadge();
    }

    /// <summary>Navigate the shell to a page (used by Dashboard quick actions).</summary>
    public void NavigateTo(Type pageType) => RootNavigation.Navigate(pageType);

    private void OnNotificationsClick(object sender, RoutedEventArgs e)
    {
        RefreshNotifications();
        NotificationsPopup.IsOpen = true;
    }

    private void OnMarkAllRead(object sender, RoutedEventArgs e)
    {
        _notifications.MarkAllRead();
        RefreshNotifications();
    }

    private void OnDismissNotification(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id })
        {
            _notifications.Dismiss(id);
            RefreshNotifications();
        }
    }

    private void OnProfileClick(object sender, RoutedEventArgs e) => ProfilePopup.IsOpen = true;

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        RootNavigation.Navigate(typeof(SettingsPage));
    }

    private void OnProfileNavClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        RootNavigation.Navigate(typeof(ProfilePage));
    }

    private void OnBackupRestoreClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        var window = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<Views.Profile.BackupWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void OnHelpClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        RootNavigation.Navigate(typeof(HelpPage));
    }

    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StartTour()
    {
        _tourIndex = 0;
        ShowTourStep();
        TourOverlay.Visibility = Visibility.Visible;
    }

    private void ShowTourStep()
    {
        (string title, string body) = TourSteps[_tourIndex];
        TourTitle.Text = title;
        TourBody.Text = body;
        TourNext.Content = _tourIndex == TourSteps.Length - 1 ? "Done" : "Next";
    }

    private void OnTourNext(object sender, RoutedEventArgs e)
    {
        if (_tourIndex < TourSteps.Length - 1)
        {
            _tourIndex++;
            ShowTourStep();
        }
        else
        {
            TourOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void OnTourSkip(object sender, RoutedEventArgs e) => TourOverlay.Visibility = Visibility.Collapsed;
}
