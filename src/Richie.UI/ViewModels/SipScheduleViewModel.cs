using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Richie.Application.Assets;
using Richie.Domain.Assets;

namespace Richie.UI.ViewModels;

public partial class SipScheduleViewModel : ObservableObject
{
    private readonly ISipService _sip;
    private Guid _assetId;

    public sealed record FrequencyOption(SipFrequency Value, string Text);

    public IReadOnlyList<FrequencyOption> Frequencies { get; } =
    [
        new(SipFrequency.Monthly, "Monthly"),
        new(SipFrequency.Quarterly, "Quarterly"),
    ];

    public IReadOnlyList<int> Days { get; } = Enumerable.Range(1, 28).ToList();

    [ObservableProperty] private string _assetName = string.Empty;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private int _dayOfMonth = 1;
    [ObservableProperty] private SipFrequency _frequency = SipFrequency.Monthly;
    [ObservableProperty] private DateTime? _startDate = DateTime.Today;
    [ObservableProperty] private ObservableCollection<string> _upcoming = [];
    [ObservableProperty] private ObservableCollection<string> _history = [];
    [ObservableProperty] private string? _error;

    public event Action<bool>? CloseRequested;

    public SipScheduleViewModel(ISipService sip) => _sip = sip;

    public void Initialize(Guid assetId, string assetName)
    {
        _assetId = assetId;
        AssetName = assetName;

        SipScheduleDto? s = _sip.GetSchedule(assetId);
        if (s is not null)
        {
            IsEnabled = s.IsEnabled;
            AmountText = s.Amount.ToString(CultureInfo.CurrentCulture);
            DayOfMonth = s.DayOfMonth;
            Frequency = s.Frequency;
            StartDate = s.StartDate;
        }

        RefreshLists();
    }

    private void RefreshLists()
    {
        Upcoming = new ObservableCollection<string>(
            _sip.GetUpcomingInstallments(_assetId, 5).Select(d => d.ToString("D", CultureInfo.CurrentCulture)));
        History = new ObservableCollection<string>(
            _sip.GetHistory(_assetId).Select(c =>
                $"{c.DateUtc.ToString("d", CultureInfo.CurrentCulture)} — {Richie.Application.Common.CurrencyFormatter.Format(c.Amount)}"));
    }

    [RelayCommand]
    private void Save()
    {
        Error = null;

        decimal amount = 0;
        if (IsEnabled && (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) || amount <= 0))
        {
            Error = "Enter a valid SIP amount.";
            return;
        }

        _sip.SaveSchedule(_assetId, new SipScheduleInput(
            IsEnabled, amount, DayOfMonth, Frequency, StartDate ?? DateTime.Today));
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
