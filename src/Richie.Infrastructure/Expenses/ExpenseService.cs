using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Richie.Application.Abstractions;
using Richie.Application.Authentication;
using Richie.Application.Expenses;
using Richie.Domain.Auditing;
using Richie.Domain.Expenses;
using Richie.Infrastructure.Auditing;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Expenses;

public sealed class ExpenseService : IExpenseService
{
    private const string Module = "Expenses";

    private readonly IAppDbContextFactory _factory;
    private readonly IUserSession _session;
    private readonly IClock _clock;

    public ExpenseService(IAppDbContextFactory factory, IUserSession session, IClock clock)
    {
        _factory = factory;
        _session = session;
        _clock = clock;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public IReadOnlyList<ExpenseSummary> GetExpenses(ExpenseFilter? filter = null)
    {
        Guid userId = UserId;
        filter ??= new ExpenseFilter();

        using RichieDbContext db = _factory.Create();
        IQueryable<Expense> query = db.Expenses.AsNoTracking().Where(e => e.UserId == userId);

        if (filter.From is { } from) query = query.Where(e => e.Date >= from);
        if (filter.To is { } to) query = query.Where(e => e.Date <= to);
        if (filter.Category is { } category) query = query.Where(e => e.Category == category);
        if (filter.MinAmount is { } min) query = query.Where(e => e.Amount >= min);
        if (filter.MaxAmount is { } max) query = query.Where(e => e.Amount <= max);

        List<Expense> list = query.OrderByDescending(e => e.Date).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string term = filter.Search.Trim();
            list = list.Where(e =>
                Contains(e.SpentBy, term) || Contains(e.SpentFor, term) || Contains(e.Notes, term)).ToList();
        }

        return list.Select(ToSummary).ToList();
    }

    public ExpenseInput? GetById(Guid id)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        Expense? e = db.Expenses.AsNoTracking().FirstOrDefault(x => x.Id == id && x.UserId == userId);
        return e is null ? null : new ExpenseInput(e.Date, e.Amount, e.Category, e.SpentBy, e.SpentFor, e.Notes);
    }

    public Guid Create(ExpenseInput input)
    {
        Guid userId = UserId;
        DateTime now = _clock.UtcNow;

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = input.Date,
            Amount = input.Amount,
            Category = input.Category,
            SpentBy = Trim(input.SpentBy),
            SpentFor = Trim(input.SpentFor),
            Notes = Trim(input.Notes),
            CreatedUtc = now,
            UpdatedUtc = now
        };

        using RichieDbContext db = _factory.Create();
        db.Expenses.Add(expense);
        AuditWriter.Add(db, userId, now, Module, AuditAction.Create, nameof(Expense), expense.Id,
            $"Added {ExpenseCategoryNames.Display(expense.Category)} expense.");
        db.SaveChanges();
        return expense.Id;
    }

    public bool Update(Guid id, ExpenseInput input)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        Expense? e = db.Expenses.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (e is null)
            return false;

        e.Date = input.Date;
        e.Amount = input.Amount;
        e.Category = input.Category;
        e.SpentBy = Trim(input.SpentBy);
        e.SpentFor = Trim(input.SpentFor);
        e.Notes = Trim(input.Notes);
        e.UpdatedUtc = _clock.UtcNow;
        AuditWriter.Add(db, userId, e.UpdatedUtc, Module, AuditAction.Update, nameof(Expense), e.Id, "Updated expense.");
        db.SaveChanges();
        return true;
    }

    public bool Delete(Guid id)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        Expense? e = db.Expenses.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (e is null)
            return false;

        db.Expenses.Remove(e);
        AuditWriter.Add(db, userId, _clock.UtcNow, Module, AuditAction.Delete, nameof(Expense), e.Id, "Deleted expense.");
        db.SaveChanges();
        return true;
    }

    public ExpenseDashboard GetDashboard()
    {
        Guid userId = UserId;
        DateTime now = _clock.UtcNow;
        int year = now.Year, month = now.Month;
        int lastYear = month == 1 ? year - 1 : year, lastMonth = month == 1 ? 12 : month - 1;

        using RichieDbContext db = _factory.Create();
        List<Expense> all = db.Expenses.AsNoTracking().Where(e => e.UserId == userId).ToList();

        List<Expense> current = all.Where(e => e.Date.Year == year && e.Date.Month == month).ToList();
        List<Expense> previous = all.Where(e => e.Date.Year == lastYear && e.Date.Month == lastMonth).ToList();

        decimal currentTotal = current.Sum(e => e.Amount);
        decimal lastTotal = previous.Sum(e => e.Amount);
        decimal mom = lastTotal > 0 ? Math.Round((currentTotal - lastTotal) / lastTotal * 100, 1) : 0;

        List<CategorySpend> breakdown = current
            .GroupBy(e => e.Category)
            .Select(g =>
            {
                decimal amount = g.Sum(e => e.Amount);
                decimal percent = currentTotal > 0 ? Math.Round(amount / currentTotal * 100, 1) : 0;
                return new CategorySpend(g.Key, ExpenseCategoryNames.Display(g.Key), amount, percent);
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        List<ExpenseSummary> recent = all.OrderByDescending(e => e.Date).Take(7).Select(ToSummary).ToList();

        return new ExpenseDashboard(currentTotal, lastTotal, mom, breakdown.FirstOrDefault()?.CategoryName,
            breakdown, recent, BuildInsights(currentTotal, lastTotal, mom, breakdown, current, previous));
    }

    private static IReadOnlyList<string> BuildInsights(
        decimal currentTotal, decimal lastTotal, decimal mom,
        List<CategorySpend> breakdown, List<Expense> current, List<Expense> previous)
    {
        var insights = new List<string>();
        if (current.Count == 0)
        {
            insights.Add("No expenses recorded this month yet.");
            return insights;
        }

        insights.Add($"You've spent {Money(currentTotal)} this month.");

        if (lastTotal > 0)
        {
            string direction = mom > 0 ? "up" : mom < 0 ? "down" : "flat vs";
            insights.Add($"That's {direction} {Math.Abs(mom):0.#}% compared to last month ({Money(lastTotal)}).");
        }

        if (breakdown.Count > 0)
            insights.Add($"{breakdown[0].CategoryName} is your biggest category at {breakdown[0].Percent:0.#}% ({Money(breakdown[0].Amount)}).");

        // Biggest category increase vs last month.
        var prevByCat = previous.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        (string Name, decimal Pct) topRise = ("", 0);
        foreach (CategorySpend c in breakdown)
        {
            decimal prev = prevByCat.GetValueOrDefault(c.Category, 0);
            if (prev > 0)
            {
                decimal rise = Math.Round((c.Amount - prev) / prev * 100, 1);
                if (rise > topRise.Pct)
                    topRise = (c.CategoryName, rise);
            }
        }
        if (topRise.Pct > 0)
            insights.Add($"Your {topRise.Name} spending rose {topRise.Pct:0.#}% vs last month.");

        return insights;
    }

    private static ExpenseSummary ToSummary(Expense e) => new(
        e.Id, e.Date, e.Amount, e.Category, ExpenseCategoryNames.Display(e.Category), e.SpentBy, e.SpentFor,
        e.RecurringId is not null);

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Money(decimal value) => Richie.Application.Common.CurrencyFormatter.Format(value);
}
