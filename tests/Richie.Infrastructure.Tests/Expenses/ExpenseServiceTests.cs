using Richie.Application.Expenses;
using Richie.Domain.Expenses;
using Richie.Infrastructure.Authentication;
using Richie.Infrastructure.Expenses;
using Richie.Infrastructure.Tests.Helpers;

namespace Richie.Infrastructure.Tests.Expenses;

public sealed class ExpenseServiceTests : IDisposable
{
    private readonly TempSqlCipherDatabase _db = new();
    private readonly FakeClock _clock = new(); // 2026-01-01
    private readonly UserSession _session = new();
    private readonly ExpenseService _sut;

    public ExpenseServiceTests()
    {
        _session.SignIn(Guid.NewGuid(), "Tester");
        _sut = new ExpenseService(_db, _session, _clock);
    }

    private Guid Add(decimal amount, ExpenseCategory category, DateTime date, string? spentFor = null) =>
        _sut.Create(new ExpenseInput(date, amount, category, "Me", spentFor, null));

    [Fact]
    public void Create_And_GetExpenses_ReturnsRow()
    {
        Add(50, ExpenseCategory.DiningRestaurants, new DateTime(2026, 1, 5));

        ExpenseSummary row = Assert.Single(_sut.GetExpenses());
        Assert.Equal(50m, row.Amount);
        Assert.Equal("Dining & Restaurants", row.CategoryName);
    }

    [Fact]
    public void GetExpenses_FiltersByCategoryAndAmountAndSearch()
    {
        Add(50, ExpenseCategory.DiningRestaurants, new DateTime(2026, 1, 5), "Lunch with team");
        Add(200, ExpenseCategory.Transportation, new DateTime(2026, 1, 6), "Flight");

        Assert.Single(_sut.GetExpenses(new ExpenseFilter(Category: ExpenseCategory.Transportation)));
        Assert.Single(_sut.GetExpenses(new ExpenseFilter(MinAmount: 100)));
        Assert.Single(_sut.GetExpenses(new ExpenseFilter(Search: "lunch")));
        Assert.Equal(2, _sut.GetExpenses().Count);
    }

    [Fact]
    public void Update_And_Delete_Work()
    {
        Guid id = Add(50, ExpenseCategory.DiningRestaurants, new DateTime(2026, 1, 5));

        Assert.True(_sut.Update(id, new ExpenseInput(new DateTime(2026, 1, 5), 75, ExpenseCategory.GroceriesFood, "Me", null, null)));
        Assert.Equal(75m, _sut.GetExpenses().Single().Amount);

        Assert.True(_sut.Delete(id));
        Assert.Empty(_sut.GetExpenses());
    }

    [Fact]
    public void Dashboard_ComputesMonthTotal_MoM_AndNamedBreakdown()
    {
        // Last month (Dec 2025): 100. This month (Jan 2026): 300 (Dining 200, Transport 100).
        Add(100, ExpenseCategory.DiningRestaurants, new DateTime(2025, 12, 10));
        Add(200, ExpenseCategory.DiningRestaurants, new DateTime(2026, 1, 5));
        Add(100, ExpenseCategory.Transportation, new DateTime(2026, 1, 6));

        ExpenseDashboard dash = _sut.GetDashboard();

        Assert.Equal(300m, dash.CurrentMonthTotal);
        Assert.Equal(100m, dash.LastMonthTotal);
        Assert.Equal(200m, dash.MonthOverMonthPercent); // +200%
        Assert.Equal("Dining & Restaurants", dash.TopCategoryName);
        Assert.Equal(2, dash.CurrentMonthBreakdown.Count);
        Assert.DoesNotContain(dash.CurrentMonthBreakdown, c => c.CategoryName.Contains("Other"));
        Assert.NotEmpty(dash.Insights);
    }

    public void Dispose() => _db.Dispose();
}
