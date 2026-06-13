using System.Windows;
using Richie.Application.Authentication;
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

    public MainWindow(IUserSession session, InactivityLockService inactivity, TourService tour)
    {
        InitializeComponent();
        _inactivity = inactivity;
        _tour = tour;

        ProfileName.Text = session.FullName ?? "Signed in";

        _tour.StartRequested += StartTour;
        Closed += (_, _) => _tour.StartRequested -= StartTour;

        PreviewMouseMove += (_, _) => _inactivity.Notify();
        PreviewMouseDown += (_, _) => _inactivity.Notify();
        PreviewKeyDown += (_, _) => _inactivity.Notify();

        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }

    private void OnNotificationsClick(object sender, RoutedEventArgs e) => NotificationsPopup.IsOpen = true;

    private void OnProfileClick(object sender, RoutedEventArgs e) => ProfilePopup.IsOpen = true;

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        RootNavigation.Navigate(typeof(SettingsPage));
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
