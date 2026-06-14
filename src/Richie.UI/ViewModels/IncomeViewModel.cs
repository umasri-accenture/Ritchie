using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Richie.Application.Income;

namespace Richie.UI.ViewModels;

public partial class IncomeViewModel : ObservableObject
{
    private readonly IIncomeService _income;

    [ObservableProperty] private ObservableCollection<IncomeSummary> _items = [];
    [ObservableProperty] private string _monthlyTotalText = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    public IncomeViewModel(IIncomeService income)
    {
        _income = income;
        Refresh();
    }

    public void Refresh()
    {
        Items = new ObservableCollection<IncomeSummary>(_income.GetRecent());
        IsEmpty = Items.Count == 0;
        MonthlyTotalText = _income.GetMonthlyTotal().ToString("N2", CultureInfo.CurrentCulture);
    }

    public void Delete(Guid id)
    {
        _income.Delete(id);
        Refresh();
    }
}
