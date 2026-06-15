using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Richie.Application.Assets;

namespace Richie.UI.ViewModels;

public partial class GoalsViewModel : ObservableObject
{
    private readonly IGoalService _goals;

    public sealed record GoalCard(
        Guid Id, string Name, string PriorityText, string TargetText, string CurrentText,
        string GapText, double Percent, string PercentText, string ProjectedText);

    [ObservableProperty] private ObservableCollection<GoalCard> _items = [];
    [ObservableProperty] private bool _isEmpty;

    public GoalsViewModel(IGoalService goals)
    {
        _goals = goals;
        Refresh();
    }

    public void Refresh()
    {
        Items = new ObservableCollection<GoalCard>(_goals.GetGoals().Select(ToCard));
        IsEmpty = Items.Count == 0;
    }

    public void Delete(Guid id)
    {
        _goals.DeleteGoal(id);
        Refresh();
    }

    private static GoalCard ToCard(GoalProgress g)
    {
        string projected =
            g.PercentComplete >= 100 ? "Goal reached."
            : g.ProjectedCompletionUtc is { } date ? $"Projected completion: {date:MMM yyyy} (at current SIP rate)."
            : "No active SIP linked — add one to project completion.";

        return new GoalCard(
            g.Id,
            g.Name,
            $"{g.Priority} priority",
            $"Target: {Money(g.TargetAmount)}",
            $"Current: {Money(g.CurrentValue)}",
            $"Gap: {Money(g.Gap)}",
            (double)g.PercentComplete,
            $"{g.PercentComplete:0.#}% of target (current value ÷ target)",
            projected);
    }

    private static string Money(decimal value) => Richie.Application.Common.CurrencyFormatter.Format(value);
}
