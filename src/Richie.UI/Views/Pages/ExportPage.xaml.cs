using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Richie.Application.Reports;
using Richie.Application.Vault;
using Richie.UI.Services;
using Richie.UI.Views.Vault;

namespace Richie.UI.Views.Pages;

public partial class ExportPage : Page
{
    public ExportPage() => InitializeComponent();

    private void OnExportPdf(object sender, RoutedEventArgs e) => Export("pdf");

    private void OnExportPptx(object sender, RoutedEventArgs e) => Export("pptx");

    private void OnExportXlsx(object sender, RoutedEventArgs e) => Export("xlsx");

    private void OnExportCsv(object sender, RoutedEventArgs e) => Export("csv");

    private void Export(string format)
    {
        var services = ((App)System.Windows.Application.Current).Services;
        var gate = services.GetRequiredService<IVaultGate>();

        // Ask whether to include decrypted (unmasked) vault passwords. If yes, re-auth with the master
        // password; otherwise export with passwords masked.
        bool unmask = false;
        bool weUnlocked = false;

        if (gate.IsConfigured() &&
            MessageBox.Show(
                "Include your vault passwords DECRYPTED (unmasked) in this export?\n\n" +
                "Choose Yes to enter your master password and export the real passwords in plain text. " +
                "Choose No to export with passwords masked.",
                "Decrypt passwords?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            bool wasUnlocked = gate.IsUnlocked;
            var reauth = services.GetRequiredService<VaultReauthWindow>();
            reauth.Owner = Window.GetWindow(this);
            reauth.Configure("Enter your master password to include unmasked passwords.", unlockOnConfirm: true);
            if (reauth.ShowDialog() != true)
                return;
            unmask = true;
            weUnlocked = !wasUnlocked;   // unlocked just for this export → re-lock afterwards
        }

        try
        {
            ReportContent content = services.GetRequiredService<IReportService>()
                .Build(new ReportRequest(ReportType.FullPortfolio, null, null, IncludeUnmaskedPasswords: unmask));

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
                FileName = $"richie-full-portfolio-{DateTime.Now:yyyyMMdd}.{format}",
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
