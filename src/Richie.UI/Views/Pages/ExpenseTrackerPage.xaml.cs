using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Richie.UI.ViewModels;
using Richie.UI.Views.Expenses;

namespace Richie.UI.Views.Pages;

public partial class ExpenseTrackerPage : Page
{
    public ExpenseTrackerPage()
    {
        InitializeComponent();
        DataContext = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<ExpenseTrackerViewModel>();
    }

    private ExpenseTrackerViewModel Vm => (ExpenseTrackerViewModel)DataContext;

    private void OnAddExpense(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnEditExpense(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id })
            OpenEditor(id);
    }

    private void OnDeleteExpense(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        if (MessageBox.Show("Delete this expense?", "Confirm delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            Vm.Delete(id);
    }

    private void OnApplyFilter(object sender, RoutedEventArgs e) => Vm.ApplyFilter();

    private void OnClearFilter(object sender, RoutedEventArgs e) => Vm.ClearFilter();

    private void OpenEditor(Guid? expenseId)
    {
        var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<AddEditExpenseWindow>();
        window.Owner = Window.GetWindow(this);
        window.Editor.Initialize(expenseId);
        if (window.ShowDialog() == true)
            Vm.Refresh();
    }
}
