using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Richie.Application.Assets;
using Richie.UI.Charts;
using SkiaSharp;

namespace Richie.UI.ViewModels;

public partial class AssetDocumentationViewModel : ObservableObject
{
    private readonly IAssetService _assets;
    private readonly IAssetDocumentService _docs;

    private static readonly string[] AssetPaletteColors =
    [
        "#1E3A8A", // Navy Blue
        "#2563EB", // Royal Blue
        "#0F766E", // Teal
        "#7C3AED", // Purple
        "#F59E0B", // Amber
        "#64748B", // Slate
        "#4F46E5", // Indigo
        "#0891B2", // Cyan
        "#4682B4"  // Steel Blue
    ];

    private static SKColor GetSkColor(int index)
    {
        return SKColor.Parse(AssetPaletteColors[index % AssetPaletteColors.Length]);
    }

    private static Brush GetMediaBrush(int index)
    {
        SKColor c = GetSkColor(index);
        var brush = new SolidColorBrush(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
        brush.Freeze();
        return brush;
    }

    /// <summary>One allocation breakdown row with a colour swatch matching the donut slice and lengths for progress.</summary>
    public sealed record AllocationRow(
        Brush Swatch,
        string TypeName,
        string ValueText,
        string PercentText,
        double PercentValue,
        GridLength FilledWidth,
        GridLength EmptyWidth);

    public sealed record UndocumentedAssetRow(
        Guid Id,
        string Name,
        string TypeName);

    [ObservableProperty] private ObservableCollection<AssetSummary> _items = [];
    [ObservableProperty] private ObservableCollection<AllocationRow> _allocation = [];
    [ObservableProperty] private ISeries[] _allocationSeries = [];
    [ObservableProperty] private string _totalCurrentValueText = string.Empty;
    [ObservableProperty] private string _totalInvestedText = string.Empty;
    [ObservableProperty] private string _totalProfitLossText = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasAssets;

    // Documentation status metrics
    [ObservableProperty] private int _totalAssetCount;
    [ObservableProperty] private int _documentedAssetCount;
    [ObservableProperty] private int _undocumentedAssetCount;
    [ObservableProperty] private double _documentCoveragePercent;
    [ObservableProperty] private ObservableCollection<UndocumentedAssetRow> _undocumentedAssets = [];

    // Detailed portfolio values
    [ObservableProperty] private string _totalInvestedValueText = string.Empty;
    [ObservableProperty] private string _totalProfitLossValueText = string.Empty;
    [ObservableProperty] private string _totalProfitLossPercentText = string.Empty;
    [ObservableProperty] private bool _isProfitLossNegative;

    // Diversification score card
    [ObservableProperty] private int _diversificationScore;
    [ObservableProperty] private string _diversificationClassification = string.Empty;
    [ObservableProperty] private GridLength _scoreGridLength = new GridLength(0, GridUnitType.Star);
    [ObservableProperty] private GridLength _remainingScoreGridLength = new GridLength(100, GridUnitType.Star);

    // Insights card
    [ObservableProperty] private string _insightText = string.Empty;

    // Top performing asset card
    [ObservableProperty] private string _topPerformingAssetName = string.Empty;
    [ObservableProperty] private string _topPerformingAssetReturnText = string.Empty;

    // Allocation summary metrics
    [ObservableProperty] private string _largestAsset = string.Empty;
    [ObservableProperty] private string _portfolioConcentration = string.Empty;
    [ObservableProperty] private int _numberOfCategories;
    [ObservableProperty] private string _mostDiversifiedCategory = string.Empty;

    public AssetDocumentationViewModel(IAssetService assets, IAssetDocumentService docs)
    {
        _assets = assets;
        _docs = docs;
        Refresh();
    }

    public void Refresh()
    {
        var assetsList = _assets.GetAssets();
        Items = new ObservableCollection<AssetSummary>(assetsList);
        IsEmpty = Items.Count == 0;
        HasAssets = !IsEmpty;

        TotalAssetCount = assetsList.Count;
        var undocumentedList = new List<UndocumentedAssetRow>();
        int documentedCount = 0;

        foreach (var asset in assetsList)
        {
            var docsCount = _docs.GetForAsset(asset.Id).Count;
            if (docsCount > 0)
            {
                documentedCount++;
            }
            else
            {
                undocumentedList.Add(new UndocumentedAssetRow(asset.Id, asset.Name, asset.TypeName));
            }
        }

        DocumentedAssetCount = documentedCount;
        UndocumentedAssetCount = TotalAssetCount - documentedCount;
        DocumentCoveragePercent = TotalAssetCount > 0 ? Math.Round((double)documentedCount / TotalAssetCount * 100.0, 1) : 0.0;
        UndocumentedAssets = new ObservableCollection<UndocumentedAssetRow>(undocumentedList);

        PortfolioSummary summary = _assets.GetPortfolioSummary();

        // 1. Calculate the allocation rows with custom palette and grid lengths
        Allocation = new ObservableCollection<AllocationRow>(summary.Allocation.Select((s, i) =>
        {
            double percentVal = (double)s.Percent;
            // Prevent zero or negative values for grid units
            double filled = Math.Max(0.01, percentVal);
            double empty = Math.Max(0.01, 100.0 - percentVal);
            return new AllocationRow(
                GetMediaBrush(i),
                s.TypeName,
                Money(s.Value),
                string.Format(CultureInfo.CurrentCulture, "({0:0.0}%)", s.Percent),
                percentVal,
                new GridLength(filled, GridUnitType.Star),
                new GridLength(empty, GridUnitType.Star));
        }));

        // 2. Set allocation chart series with custom palette
        AllocationSeries = summary.Allocation
            .Select((s, i) => (ISeries)new PieSeries<double>
            {
                Values = [(double)s.Value],
                Name = s.TypeName,
                InnerRadius = 55,
                Fill = new SolidColorPaint(GetSkColor(i))
            })
            .ToArray();

        // 3. Base texts
        TotalCurrentValueText = Money(summary.TotalCurrentValue);
        TotalInvestedText = Money(summary.TotalInvested);
        TotalProfitLossText = $"{Money(summary.TotalProfitLoss)} ({summary.TotalProfitLossPercent:+0.0;-0.0;0.0}%)";

        // 4. Expose clean values for summary cards
        TotalInvestedValueText = Money(summary.TotalInvested);
        TotalProfitLossValueText = Money(summary.TotalProfitLoss);
        TotalProfitLossPercentText = string.Format(CultureInfo.CurrentCulture, "{0:+0.0;-0.0;0.0}%", summary.TotalProfitLossPercent);
        IsProfitLossNegative = summary.TotalProfitLoss < 0;

        // 5. Calculate diversification score (HHI-based)
        double hhi = 0;
        if (summary.Allocation.Count > 0)
        {
            hhi = summary.Allocation.Sum(a =>
            {
                double p = (double)a.Percent / 100.0;
                return p * p;
            });
        }
        else
        {
            hhi = 1.0;
        }
        double divScoreDouble = (1.0 - hhi) * 100.0;
        DiversificationScore = (int)Math.Round(Math.Max(0.0, Math.Min(100.0, divScoreDouble)));
        ScoreGridLength = new GridLength(Math.Max(0.01, DiversificationScore), GridUnitType.Star);
        RemainingScoreGridLength = new GridLength(Math.Max(0.01, 100.0 - DiversificationScore), GridUnitType.Star);

        if (summary.Allocation.Count == 0)
        {
            DiversificationClassification = "Poor";
        }
        else if (DiversificationScore < 30)
        {
            DiversificationClassification = "Poor";
        }
        else if (DiversificationScore < 60)
        {
            DiversificationClassification = "Moderate";
        }
        else if (DiversificationScore < 80)
        {
            DiversificationClassification = "Good";
        }
        else
        {
            DiversificationClassification = "Excellent";
        }

        // 6. Generate allocation insights
        if (summary.Allocation.Count == 0)
        {
            InsightText = "No asset allocations available yet. Add assets to analyze distribution.";
        }
        else
        {
            var largestSlice = summary.Allocation.OrderByDescending(a => a.Percent).First();
            if (largestSlice.Percent > 60)
            {
                InsightText = $"{largestSlice.TypeName} forms {largestSlice.Percent:0.0}% of portfolio. Portfolio is highly concentrated. Consider diversification across financial assets.";
            }
            else if (largestSlice.Percent > 40)
            {
                InsightText = $"{largestSlice.TypeName} forms {largestSlice.Percent:0.0}% of portfolio. Portfolio is moderately concentrated. Look into balancing with other asset classes.";
            }
            else
            {
                InsightText = $"Portfolio is well-diversified. {largestSlice.TypeName} is the largest holding at {largestSlice.Percent:0.0}%. Keep maintaining this balance.";
            }
        }

        // 7. Calculate top performing asset
        var topAsset = Items.OrderByDescending(a => a.ProfitLossPercent).FirstOrDefault();
        if (topAsset != null && Items.Count > 0)
        {
            TopPerformingAssetName = topAsset.Name;
            TopPerformingAssetReturnText = string.Format(CultureInfo.CurrentCulture, "{0:+0.0;-0.0;0.0}%", topAsset.ProfitLossPercent);
        }
        else
        {
            TopPerformingAssetName = "None";
            TopPerformingAssetReturnText = "0.0%";
        }

        // 8. Allocation summary details below chart
        if (summary.Allocation.Count > 0)
        {
            var largestSlice = summary.Allocation.OrderByDescending(a => a.Percent).First();
            LargestAsset = $"{largestSlice.TypeName} ({largestSlice.Percent:0.0}%)";
            PortfolioConcentration = $"{largestSlice.Percent:0.0}%";
        }
        else
        {
            LargestAsset = "N/A";
            PortfolioConcentration = "0.0%";
        }
        NumberOfCategories = summary.Allocation.Count;

        var diversifiedGroup = Items.GroupBy(i => i.TypeName)
                                    .OrderByDescending(g => g.Count())
                                    .FirstOrDefault();
        MostDiversifiedCategory = diversifiedGroup != null ? $"{diversifiedGroup.Key} ({diversifiedGroup.Count()} assets)" : "N/A";
    }

    public void Delete(Guid id)
    {
        _assets.Delete(id);
        Refresh();
    }

    public void ToggleExclusion(Guid id, bool excluded)
    {
        _assets.SetPortfolioExclusion(id, excluded);
        Refresh();
    }

    private static string Money(decimal value) => Richie.Application.Common.CurrencyFormatter.Format(value);
}
