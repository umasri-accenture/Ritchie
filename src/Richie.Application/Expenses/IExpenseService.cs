namespace Richie.Application.Expenses;

/// <summary>Expense CRUD, filtered history, and the current-month insight dashboard (PRD §7).</summary>
public interface IExpenseService
{
    IReadOnlyList<ExpenseSummary> GetExpenses(ExpenseFilter? filter = null);
    ExpenseInput? GetById(Guid id);

    Guid Create(ExpenseInput input);
    bool Update(Guid id, ExpenseInput input);
    bool Delete(Guid id);

    ExpenseDashboard GetDashboard();
}
