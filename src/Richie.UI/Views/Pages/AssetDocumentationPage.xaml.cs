using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Richie.Application.Assets;
using Richie.UI.ViewModels;
using Richie.UI.Views.Assets;

namespace Richie.UI.Views.Pages;

public partial class AssetDocumentationPage : Page
{
    public AssetDocumentationPage()
    {
        InitializeComponent();
        DataContext = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<AssetDocumentationViewModel>();
    }

    private AssetDocumentationViewModel Vm => (AssetDocumentationViewModel)DataContext;

    private void OnAddAsset(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnOpenGoals(object sender, RoutedEventArgs e)
    {
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<GoalsWindow>();
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }
        Vm.Refresh(); // goal links don't change assets, but current values may have been recalculated
    }

    private void OnBulkUpload(object sender, RoutedEventArgs e)
    {
        var services = ((App)System.Windows.Application.Current).Services;
        BulkUploadWindow? window = null;
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            window = services.GetRequiredService<BulkUploadWindow>();
            window.Owner = Window.GetWindow(this);
            window.Upload.Initialize(services.GetRequiredService<IAssetImportService>(), "Bulk upload assets");
            window.ShowDialog();
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }
        if (window != null && window.Upload.ImportedAny)
            Vm.Refresh();
    }

    private void OnViewAsset(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        bool editRequested;
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<AssetDetailsWindow>();
            window.Owner = Window.GetWindow(this);
            window.Details.Initialize(id);
            window.ShowDialog();
            editRequested = window.Details.EditRequested;
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }

        Vm.Refresh(); // exclusion may have changed
        if (editRequested)
            OpenEditor(id);
    }

    private void OnEditAsset(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id })
            OpenEditor(id);
    }

    private void OnSipAsset(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        string assetName = Vm.Items.FirstOrDefault(a => a.Id == id)?.Name ?? "Asset";
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<SipScheduleWindow>();
            window.Owner = Window.GetWindow(this);
            window.Schedule.Initialize(id, assetName);
            window.ShowDialog();
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }
        Vm.Refresh();
    }

    private void OnDocsAsset(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        string assetName = Vm.Items.FirstOrDefault(a => a.Id == id)?.Name ?? "Asset";
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<DocumentsWindow>();
            window.Owner = Window.GetWindow(this);
            window.Documents.Initialize(id, assetName);
            window.ShowDialog();
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }
        Vm.Refresh();
    }

    private void OnDeleteAsset(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        MessageBoxResult confirm = MessageBox.Show(
            "Delete this asset? This cannot be undone.", "Confirm delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
            Vm.Delete(id);
    }

    private void OnExclClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is Guid id)
            Vm.ToggleExclusion(id, cb.IsChecked ?? false);
    }

    private void OpenEditor(Guid? assetId)
    {
        bool? result;
        SettingsViewModel.ApplyAccent("#2563EB");
        try
        {
            var window = ((App)System.Windows.Application.Current).Services.GetRequiredService<AddEditAssetWindow>();
            window.Owner = Window.GetWindow(this);
            window.Editor.Initialize(assetId);
            result = window.ShowDialog();
        }
        finally
        {
            SettingsViewModel.ApplyBrandAccent();
        }

        if (result == true)
            Vm.Refresh();
    }
}
