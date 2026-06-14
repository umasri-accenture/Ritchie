using Richie.UI.ViewModels;
using Wpf.Ui.Controls;

namespace Richie.UI.Views.Expenses;

public partial class AddEditExpenseWindow : FluentWindow
{
    public AddEditExpenseViewModel Editor { get; }

    public AddEditExpenseWindow(AddEditExpenseViewModel editor)
    {
        InitializeComponent();
        Editor = editor;
        DataContext = editor;
        editor.CloseRequested += OnCloseRequested;
        Closed += (_, _) => editor.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(bool success)
    {
        DialogResult = success;
        Close();
    }
}
