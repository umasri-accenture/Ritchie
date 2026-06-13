using System.Windows;
using System.Windows.Controls;

namespace Richie.UI.Views.Pages;

public partial class HelpPage : Page
{
    public HelpPage() => InitializeComponent();

    private void OnReplayTour(object sender, RoutedEventArgs e) =>
        ((App)System.Windows.Application.Current).RequestTour();
}
