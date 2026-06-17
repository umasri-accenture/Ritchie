using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Richie.Application.Authentication;
using Richie.Application.Vault;
using Richie.Infrastructure;
using Richie.Infrastructure.Persistence;
using Richie.UI.Services;
using Richie.UI.ViewModels;
using Richie.UI.Views;
using Richie.UI.Views.Auth;
using Serilog;

namespace Richie.UI;

/// <summary>
/// Composition root and window lifecycle: splash → auth (signup/login) → main shell,
/// with logout and inactivity auto-lock returning to the auth window.
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private MainWindow? _main;

    public App()
    {
        // Keep every window/dialog within the visible screen (content already scrolls). Done as a
        // class handler — NOT a window Style — so the WPF-UI FluentWindow template/backdrop is untouched.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnAnyWindowLoaded));

        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Richie", "logs");
        Directory.CreateDirectory(logDirectory);

        _host = Host.CreateDefaultBuilder()
            .UseSerilog((context, configuration) => configuration
                .MinimumLevel.Information()
                .WriteTo.Debug()
                .WriteTo.File(
                    Path.Combine(logDirectory, "richie-.log"),
                    rollingInterval: RollingInterval.Day))
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure();

                services.AddSingleton<AuthNavigationService>();
                services.AddSingleton<IAuthNavigation>(sp => sp.GetRequiredService<AuthNavigationService>());
                services.AddSingleton<InactivityLockService>();
                services.AddSingleton<TourService>();
                services.AddSingleton<Wpf.Ui.ISnackbarService, Wpf.Ui.SnackbarService>();
                services.AddSingleton<ToastService>();

                services.AddTransient<LoginViewModel>();
                services.AddTransient<SignupViewModel>();
                services.AddTransient<ForgotPasswordViewModel>();
                services.AddTransient<LoginPage>();
                services.AddTransient<SignupPage>();
                services.AddTransient<ForgotPasswordPage>();

                services.AddTransient<AuthWindow>();
                services.AddTransient<MainWindow>();

                services.AddTransient<AssetDocumentationViewModel>();
                services.AddTransient<AddEditAssetViewModel>();
                services.AddTransient<AssetDetailsViewModel>();
                services.AddTransient<SipScheduleViewModel>();
                services.AddTransient<GoalsViewModel>();
                services.AddTransient<AddEditGoalViewModel>();
                services.AddTransient<DocumentsViewModel>();
                services.AddTransient<BulkUploadViewModel>();
                services.AddTransient<ExpenseTrackerViewModel>();
                services.AddTransient<AddEditExpenseViewModel>();
                services.AddTransient<RecurringExpensesViewModel>();
                services.AddTransient<AddEditRecurringViewModel>();
                services.AddTransient<ExpenseAnalyticsViewModel>();
                services.AddTransient<AddIncomeViewModel>();
                services.AddTransient<IncomeViewModel>();
                services.AddTransient<BillsViewModel>();
                services.AddTransient<PasswordVaultViewModel>();
                services.AddTransient<AddEditVaultEntryViewModel>();
                services.AddTransient<VaultReauthViewModel>();
                services.AddTransient<VaultHealthViewModel>();
                services.AddTransient<VaultSecurityViewModel>();
                services.AddTransient<VaultRecoveryViewModel>();
                services.AddTransient<InsuranceViewModel>();
                services.AddTransient<AddEditInsuranceViewModel>();
                services.AddTransient<HealthAuditViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<ProfileViewModel>();
                services.AddTransient<ChangePasswordViewModel>();
                services.AddTransient<Views.Profile.ChangePasswordWindow>();
                services.AddTransient<Views.Profile.BackupWindow>();
                services.AddTransient<Views.Assets.AddEditAssetWindow>();
                services.AddTransient<Views.Assets.AssetDetailsWindow>();
                services.AddTransient<Views.Assets.SipScheduleWindow>();
                services.AddTransient<Views.Assets.GoalsWindow>();
                services.AddTransient<Views.Assets.AddEditGoalWindow>();
                services.AddTransient<Views.Assets.DocumentsWindow>();
                services.AddTransient<Views.Assets.BulkUploadWindow>();
                services.AddTransient<Views.Expenses.AddEditExpenseWindow>();
                services.AddTransient<Views.Expenses.RecurringExpensesWindow>();
                services.AddTransient<Views.Expenses.AddEditRecurringWindow>();
                services.AddTransient<Views.Expenses.ExpenseAnalyticsWindow>();
                services.AddTransient<Views.Expenses.AddIncomeWindow>();
                services.AddTransient<Views.Expenses.IncomeWindow>();
                services.AddTransient<Views.Expenses.BillsWindow>();
                services.AddTransient<Views.Vault.AddEditVaultEntryWindow>();
                services.AddTransient<Views.Vault.VaultReauthWindow>();
                services.AddTransient<Views.Vault.VaultHealthWindow>();
                services.AddTransient<Views.Vault.VaultSecurityWindow>();
                services.AddTransient<Views.Vault.VaultRecoveryWindow>();
                services.AddTransient<Views.Insurance.InsuranceWindow>();
                services.AddTransient<Views.Insurance.AddEditInsuranceWindow>();

                services.AddHostedService<Infrastructure.Assets.SipProcessingService>();
                services.AddHostedService<Infrastructure.Expenses.RecurringExpenseProcessingService>();
                services.AddHostedService<Infrastructure.Insurance.InsuranceRenewalProcessingService>();
            })
            .Build();
    }

    /// <summary>Service provider for views that resolve their view-models / dialogs.</summary>
    public IServiceProvider Services => _host.Services;

    // Cap only modal dialogs (which have an Owner) to the app window's size, so popups can't exceed
    // the app. The main/auth/splash windows have no Owner, so they stay freely resizable/maximizable.
    private static void OnAnyWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window { Owner: { ActualHeight: > 0 } owner } dialog)
        {
            dialog.MaxHeight = owner.ActualHeight;
            dialog.MaxWidth = owner.ActualWidth;
        }
    }

    /// <summary>Requested from the Help page to replay the app tour.</summary>
    public void RequestTour() => _host.Services.GetRequiredService<TourService>().Request();

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled UI exception");
            MessageBox.Show(args.Exception.ToString(), "Richie — startup error");
        };

        // On a brand-new install (no db.key yet) ask the user before touching any data files.
        if (AppPaths.IsFirstRun)
        {
            var welcome = new FirstRunWindow();
            if (welcome.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        // Initialize database and load startup theme preference up-front,
        // so that even the Splash Window opens in the correct theme.
        try
        {
            _host.Services.GetRequiredService<IDatabaseInitializer>().Initialize();
            var settingsService = _host.Services.GetRequiredService<Richie.Application.Settings.IAppSettingsService>();
            var theme = settingsService.GetStartupTheme();
            Richie.UI.ViewModels.SettingsViewModel.ApplyTheme(theme);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize database or load settings theme on startup");
            Richie.UI.ViewModels.SettingsViewModel.ApplyTheme("System");
        }

        var splash = new SplashWindow();
        splash.Show();

        try
        {
            await _host.StartAsync();
            ShowAuth();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show(ex.ToString(), "Richie — startup error");
            splash.Close();
            Shutdown();
            return;
        }

        splash.Close();
    }

    private void ShowAuth()
    {
        var navigation = _host.Services.GetRequiredService<AuthNavigationService>();
        var window = _host.Services.GetRequiredService<AuthWindow>();

        EventHandler<AuthenticatedEventArgs>? onAuthenticated = null;
        onAuthenticated = (_, args) =>
        {
            navigation.Authenticated -= onAuthenticated;
            ShowMain(args.IsFirstLogin);
            window.Close();
        };
        navigation.Authenticated += onAuthenticated;

        window.Closed += (sender, args) =>
        {
            if (_main == null)
            {
                Shutdown();
            }
        };

        bool firstRun = !_host.Services.GetRequiredService<IAuthService>().AnyUserExists();
        window.Show();
        window.Start(firstRun);
    }

    private void ShowMain(bool firstLogin)
    {
        var inactivity = _host.Services.GetRequiredService<InactivityLockService>();

        // Apply the signed-in user's preferences (theme + auto-lock timeout).
        Richie.Application.Settings.AppSettingsData settings =
            _host.Services.GetRequiredService<Richie.Application.Settings.IAppSettingsService>().Get();
        Richie.UI.ViewModels.SettingsViewModel.ApplyTheme(settings.Theme);
        inactivity.Timeout = TimeSpan.FromMinutes(settings.SessionLockMinutes);

        _main = _host.Services.GetRequiredService<MainWindow>();
        _main.LogoutRequested += (_, _) => ReturnToLogin();
        inactivity.Locked += OnLocked;
        inactivity.Start();

        _main.Show();
        ShutdownMode = ShutdownMode.OnLastWindowClose;

        if (firstLogin)
            _host.Services.GetRequiredService<TourService>().Request();
    }

    private void OnLocked(object? sender, EventArgs e) => ReturnToLogin();

    private void ReturnToLogin()
    {
        if (_main is null)
            return;

        var inactivity = _host.Services.GetRequiredService<InactivityLockService>();
        inactivity.Stop();
        inactivity.Locked -= OnLocked;
        _host.Services.GetRequiredService<IVaultGate>().Lock();
        _host.Services.GetRequiredService<IUserSession>().SignOut();

        MainWindow closing = _main;
        _main = null;
        ShowAuth();
        closing.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            _host.StopAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to stop host cleanly on exit");
        }
        finally
        {
            _host.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
