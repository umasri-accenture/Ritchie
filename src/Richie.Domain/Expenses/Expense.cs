namespace Richie.Domain.Expenses;

/// <summary>A single expense entry (PRD §7.2).</summary>
public class Expense
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public ExpenseCategory Category { get; set; }
    public string? SpentBy { get; set; }
    public string? SpentFor { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
