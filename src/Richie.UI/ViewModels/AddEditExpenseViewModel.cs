using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richie.Application.Expenses;
using Richie.Domain.Expenses;

namespace Richie.UI.ViewModels;

public partial class AddEditExpenseViewModel : ObservableObject
{
    private readonly IExpenseService _expenses;
    private Guid? _editId;

    public sealed record CategoryOption(ExpenseCategory Value, string Text);

    public IReadOnlyList<CategoryOption> Categories { get; } =
        Enum.GetValues<ExpenseCategory>().Select(c => new CategoryOption(c, ExpenseCategoryNames.Display(c))).ToList();

    [ObservableProperty] private string _title = "Add expense";
    [ObservableProperty] private DateTime? _date = DateTime.Today;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private ExpenseCategory _category = ExpenseCategory.GroceriesFood;
    [ObservableProperty] private string? _spentBy;
    [ObservableProperty] private string? _spentFor;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _error;

    public event Action<bool>? CloseRequested;

    public AddEditExpenseViewModel(IExpenseService expenses) => _expenses = expenses;

    public void Initialize(Guid? id)
    {
        _editId = id;
        if (id is null)
            return;

        ExpenseInput? e = _expenses.GetById(id.Value);
        if (e is null)
            return;

        Title = "Edit expense";
        Date = e.Date;
        AmountText = e.Amount.ToString(CultureInfo.CurrentCulture);
        Category = e.Category;
        SpentBy = e.SpentBy;
        SpentFor = e.SpentFor;
        Notes = e.Notes;
    }

    [RelayCommand]
    private void Save()
    {
        Error = null;
        if (Date is null)
        {
            Error = "Date is required.";
            return;
        }
        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) || amount <= 0)
        {
            Error = "Enter a valid amount.";
            return;
        }

        var input = new ExpenseInput(Date.Value, amount, Category, SpentBy, SpentFor, Notes);
        if (_editId is null)
            _expenses.Create(input);
        else
            _expenses.Update(_editId.Value, input);

        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
