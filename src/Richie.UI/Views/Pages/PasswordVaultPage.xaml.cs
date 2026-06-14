using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Richie.UI.Services;
using Richie.UI.ViewModels;
using Richie.UI.Views.Vault;

namespace Richie.UI.Views.Pages;

public partial class PasswordVaultPage : Page
{
    public PasswordVaultPage()
    {
        InitializeComponent();
        DataContext = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<PasswordVaultViewModel>();
    }

    private PasswordVaultViewModel Vm => (PasswordVaultViewModel)DataContext;

    // Re-lock on every access to the page (PRD §8.1).
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Vm.ResetToLocked();
        MasterPasswordBox.Focus();
    }

    private void OnMasterPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Vm.Submit();
    }

    private void OnSubmit(object sender, RoutedEventArgs e) => Vm.Submit();

    private void OnLock(object sender, RoutedEventArgs e)
    {
        Vm.ResetToLocked();
        MasterPasswordBox.Focus();
    }

    private void OnAddEntry(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnPasswordHealth(object sender, RoutedEventArgs e)
    {
        var window = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<VaultHealthWindow>();
        window.Owner = Window.GetWindow(this);
        window.ShowDialog();
    }

    private void OnAccountLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { Tag: string url } && !UrlLauncher.TryOpen(url))
            MessageBox.Show("This entry's website link is not a valid http(s) URL.", "Open website",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnReveal(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: VaultEntryRowViewModel row })
            return;

        if (row.IsRevealed)
        {
            row.Hide();
            return;
        }

        if (Reauthenticate("Re-enter your master password to reveal this password.")
            && Vm.RevealPassword(row.Id) is { } plaintext)
            row.Reveal(plaintext);
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: VaultEntryRowViewModel row })
            return;

        // Copying exposes the secret — require re-auth unless it's already revealed on screen.
        if (!row.IsRevealed && !Reauthenticate("Re-enter your master password to copy this password."))
            return;

        if (Vm.RevealPassword(row.Id) is { Length: > 0 } plaintext)
        {
            SecureClipboard.CopyWithAutoClear(plaintext);
            new Wpf.Ui.Controls.Snackbar(VaultSnackbar)
            {
                Title = "Password copied",
                Content = "It will clear from the clipboard in 30 seconds.",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Success,
                Timeout = TimeSpan.FromSeconds(3)
            }.Show();
        }
    }

    private bool Reauthenticate(string prompt)
    {
        var window = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<VaultReauthWindow>();
        window.Owner = Window.GetWindow(this);
        window.Configure(prompt);
        return window.ShowDialog() == true;
    }

    private void OnEditEntry(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id })
            OpenEditor(id);
    }

    private void OnDeleteEntry(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
            return;

        if (MessageBox.Show("Delete this credential?", "Confirm delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            Vm.Delete(id);
    }

    private void OpenEditor(Guid? id)
    {
        var window = ((App)System.Windows.Application.Current).Services
            .GetRequiredService<AddEditVaultEntryWindow>();
        window.Owner = Window.GetWindow(this);
        window.Editor.Initialize(id);
        if (window.ShowDialog() == true)
            Vm.Reload();
    }
}
