using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Richie.UI.ViewModels;
using Wpf.Ui.Controls;

namespace Richie.UI.Views.Assets;

public partial class GoalsWindow : FluentWindow
{
    private readonly GoalsViewModel _vm;

    public GoalsWindow(GoalsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void OnAddGoal(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnEditGoal(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id })
            OpenEditor(id);
    }

    private void OnDeleteGoal(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        if (System.Windows.MessageBox.Show("Delete this goal?", "Confirm delete",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            == System.Windows.MessageBoxResult.Yes)
            _vm.Delete(id);
    }

    private void OpenEditor(Guid? goalId)
    {
        bool? result;
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<AddEditGoalWindow>();
            window.Owner = this;
            window.Editor.Initialize(goalId);
            result = window.ShowDialog();
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }
        if (result == true)
            _vm.Refresh();
    }
}
