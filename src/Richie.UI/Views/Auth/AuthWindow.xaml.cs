using Microsoft.Extensions.DependencyInjection;
using Richie.UI.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Richie.UI.Views.Auth;

/// <summary>
/// Hosts the auth pages in a frame. Page navigation is driven by
/// <see cref="AuthNavigationService"/> so the page view-models stay decoupled from this window.
/// </summary>
public partial class AuthWindow : FluentWindow
{
    private readonly IServiceProvider _services;
    private readonly AuthNavigationService _navigation;

    public AuthWindow(IServiceProvider services, AuthNavigationService navigation)
    {
        InitializeComponent();
        _services = services;
        _navigation = navigation;
        _navigation.NavigateRequested = Navigate;
    }

    /// <summary>Show signup on first run (no accounts yet), otherwise login.</summary>
    public void Start(bool firstRun)
    {
        if (firstRun)
            _navigation.ShowSignup();
        else
            _navigation.ShowLogin();
    }

    private void Navigate(AuthNavigationService.AuthPage page)
    {
        object view = page switch
        {
            AuthNavigationService.AuthPage.Login => _services.GetRequiredService<LoginPage>(),
            AuthNavigationService.AuthPage.Signup => _services.GetRequiredService<SignupPage>(),
            AuthNavigationService.AuthPage.ForgotPassword => _services.GetRequiredService<ForgotPasswordPage>(),
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };
        RootFrame.Navigate(view);
    }
}
