using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Richie.Application.Vault;

namespace Richie.UI.ViewModels;

public partial class VaultHealthViewModel : ObservableObject
{
    private readonly IVaultHealthService _health;

    [ObservableProperty] private int _score;
    [ObservableProperty] private string _rating = string.Empty;
    [ObservableProperty] private Brush _ratingBrush = Brushes.Gray;
    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private ObservableCollection<HealthRow> _items = [];
    [ObservableProperty] private bool _hasIssues;
    [ObservableProperty] private bool _noIssues;

    public sealed record HealthRow(string AccountName, int Score, string IssuesText);

    public string ScaleLegend =>
        "Score 0–100 — green ≥ 80 (Good), amber 50–79 (Needs attention), red < 50 (Critical).";

    public string ScoringExplanation =>
        "Each credential starts at 100 and loses points for issues: weak password −40, " +
        "reused on another account −30, not changed in 90+ days −10 (180+ days −20). " +
        "Your score is the average across all credentials.";

    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F));

    public VaultHealthViewModel(IVaultHealthService health) => _health = health;

    public void Load()
    {
        VaultHealthReport report = _health.GetReport();
        Score = report.Score;
        Rating = report.Rating;
        RatingBrush = report.Score >= 80 ? Green : report.Score >= 50 ? Amber : Red;
        SummaryText = report.Total == 0
            ? "No credentials in your vault yet."
            : $"{report.Total} credential(s) · {report.WeakCount} weak · {report.ReusedCount} reused · {report.AgedCount} aged";
        Items = new ObservableCollection<HealthRow>(
            report.Items.Select(i => new HealthRow(i.AccountName, i.Score, string.Join(", ", i.Issues))));
        HasIssues = report.Items.Count > 0;
        NoIssues = !HasIssues;
    }
}
