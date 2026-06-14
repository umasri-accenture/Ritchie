using Richie.UI.ViewModels;
using Wpf.Ui.Controls;

namespace Richie.UI.Views.Vault;

public partial class VaultHealthWindow : FluentWindow
{
    private readonly VaultHealthViewModel _vm;

    public VaultHealthWindow(VaultHealthViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => _vm.Load();
    }
}
