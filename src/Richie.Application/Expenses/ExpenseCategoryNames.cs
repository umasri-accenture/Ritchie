using Richie.Domain.Expenses;

namespace Richie.Application.Expenses;

/// <summary>Explicit display names for every expense category (PRD §7.6; no "Others").</summary>
public static class ExpenseCategoryNames
{
    public static string Display(ExpenseCategory category) => category switch
    {
        ExpenseCategory.HousingUtilities => "Housing & Utilities",
        ExpenseCategory.GroceriesFood => "Groceries & Food",
        ExpenseCategory.Transportation => "Transportation",
        ExpenseCategory.Healthcare => "Healthcare",
        ExpenseCategory.Education => "Education",
        ExpenseCategory.EntertainmentLeisure => "Entertainment & Leisure",
        ExpenseCategory.InsuranceInvestments => "Insurance & Investments",
        ExpenseCategory.DiningRestaurants => "Dining & Restaurants",
        ExpenseCategory.PersonalCareClothing => "Personal Care & Clothing",
        ExpenseCategory.Miscellaneous => "Miscellaneous",
        _ => category.ToString()
    };
}
