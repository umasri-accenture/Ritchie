using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Richie.Application.Reports;
using Richie.Application.Vault;
using Richie.UI.Services;
using Richie.UI.ViewModels;
using Richie.UI.Views.Vault;

namespace Richie.UI.Views.Pages;

public partial class ReportsPage : Page
{
    public ReportsPage()
    {
        InitializeComponent();
        DataContext = ((App)System.Windows.Application.Current).Services.GetRequiredService<ReportsViewModel>();
    }

    private ReportsViewModel Vm => (ReportsViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e) => Vm.GeneratePreview();

    private void OnGeneratePreview(object sender, RoutedEventArgs e) => Vm.GeneratePreview();

    private void OnExportPdf(object sender, RoutedEventArgs e) => Export("pdf");

    private void OnExportPptx(object sender, RoutedEventArgs e) => Export("pptx");

    private void OnExportXlsx(object sender, RoutedEventArgs e) => Export("xlsx");

    private void OnExportCsv(object sender, RoutedEventArgs e) => Export("csv");

    private void Export(string format)
    {
        var services = ((App)System.Windows.Application.Current).Services;
        var gate = services.GetRequiredService<IVaultGate>();

        bool unmask = Vm.IncludeUnmaskedPasswords && Vm.IsVaultReport;
        bool weUnlocked = false;

        if (unmask)
        {
            if (!gate.IsConfigured())
            {
                MessageBox.Show("Set up the Password Vault before exporting unmasked passwords.",
                    "Unmasked export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show(
                    "This export will contain UNMASKED passwords in plain text — anyone who opens the file can read them. Continue?",
                    "Confirm unmasked export", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            bool wasUnlocked = gate.IsUnlocked;
            var reauth = services.GetRequiredService<VaultReauthWindow>();
            reauth.Owner = Window.GetWindow(this);
            reauth.Configure("Enter your master password to include unmasked passwords.", unlockOnConfirm: true);
            if (reauth.ShowDialog() != true)
                return;
            weUnlocked = !wasUnlocked;   // unlocked just for this export → re-lock afterwards
        }

        try
        {
            ReportContent content = Vm.BuildForExport(unmask);
            var exporter = services.GetRequiredService<IReportExporter>();
            byte[] bytes = format switch
            {
                "pdf" => exporter.ToPdf(content),
                "pptx" => exporter.ToPptx(content),
                "xlsx" => exporter.ToXlsx(content),
                "csv" => exporter.ToCsv(content),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };

            var dialog = new SaveFileDialog
            {
                FileName = Vm.SuggestedFileName(format),
                Filter = format switch
                {
                    "pdf" => "PDF document|*.pdf",
                    "pptx" => "PowerPoint|*.pptx",
                    "xlsx" => "Excel workbook|*.xlsx",
                    "csv" => "CSV file|*.csv",
                    _ => "All files|*.*"
                }
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                File.WriteAllBytes(dialog.FileName, bytes);
                services.GetRequiredService<ToastService>().Success($"{format.ToUpperInvariant()} exported.");
            }
        }
        finally
        {
            if (weUnlocked)
                gate.Lock();
        }
    }
}
