using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Richie.Application.Expenses;

namespace Richie.UI.ViewModels;

public partial class RecurringExpensesViewModel : ObservableObject
{
    private readonly IExpenseRecurringService _recurring;

    public sealed record RecurringCard(Guid Id, string Header, string Detail);

    [ObservableProperty] private ObservableCollection<RecurringCard> _items = [];
    [ObservableProperty] private bool _isEmpty;

    public RecurringExpensesViewModel(IExpenseRecurringService recurring)
    {
        _recurring = recurring;
        Refresh();
    }

    public void Refresh()
    {
        Items = new ObservableCollection<RecurringCard>(_recurring.GetRules().Select(ToCard));
        IsEmpty = Items.Count == 0;
    }

    public void Delete(Guid id)
    {
        _recurring.DeleteRule(id);
        Refresh();
    }

    private static RecurringCard ToCard(RecurringSummary r)
    {
        string header = $"{r.CategoryName} — {Richie.Application.Common.CurrencyFormatter.Format(r.Amount)}";
        string status = r.IsEnabled ? $"next {r.NextRunDateUtc:d}" : "paused";
        string detail = $"{r.Frequency}, {status}";
        return new RecurringCard(r.Id, header, detail);
    }
}
