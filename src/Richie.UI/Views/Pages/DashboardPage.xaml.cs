using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Richie.UI.ViewModels;
using Richie.UI.Views.Assets;
using Richie.UI.Views.Expenses;

namespace Richie.UI.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly DispatcherTimer _clock;

    public DashboardPage()
    {
        InitializeComponent();
        DataContext = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<DashboardViewModel>();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => TickClock();
    }

    private DashboardViewModel Vm => (DashboardViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Vm.Load();
        TickClock();
        _clock.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _clock.Stop();

    private void TickClock() => ClockText.Text = DateTime.Now.ToString("h:mm tt", CultureInfo.CurrentCulture);

    private void OnAddAsset(object sender, RoutedEventArgs e)
    {
        var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<AddEditAssetWindow>();
        window.Owner = Window.GetWindow(this);
        window.Editor.Initialize(null);
        if (window.ShowDialog() == true)
            Vm.Load();
    }

    private void OnAddExpense(object sender, RoutedEventArgs e)
    {
        var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<AddEditExpenseWindow>();
        window.Owner = Window.GetWindow(this);
        window.Editor.Initialize(null);
        if (window.ShowDialog() == true)
            Vm.Load();
    }

    // The vault requires re-authentication, so route there rather than opening the add dialog directly.
    private void OnAddPassword(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as MainWindow)?.NavigateTo(typeof(PasswordVaultPage));

    private void OnGenerateReport(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as MainWindow)?.NavigateTo(typeof(ReportsPage));

    // Deep-link an insight to the module that can act on it.
    private void OnInsightAction(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DashboardViewModel.InsightRow row) return;
        Type target = row.Topic switch
        {
            Richie.Application.Audit.InsightTopic.Spending => typeof(ExpenseTrackerPage),
            _ => typeof(FinancialHealthAuditPage)
        };
        (Window.GetWindow(this) as MainWindow)?.NavigateTo(target);
    }
}
