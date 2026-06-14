using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Richie.UI.ViewModels;
using Richie.UI.Views.Assets;
using Richie.UI.Views.Expenses;

namespace Richie.UI.Views.Pages;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        DataContext = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<DashboardViewModel>();
    }

    private DashboardViewModel Vm => (DashboardViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e) => Vm.Load();

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
}
