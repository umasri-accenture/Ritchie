namespace Richie.Domain.Expenses;

/// <summary>
/// The fixed expense category list (PRD §7.6). No "Others" bucket — every expense is named;
/// <see cref="Miscellaneous"/> is the last-resort only.
/// </summary>
public enum ExpenseCategory
{
    HousingUtilities = 1,
    GroceriesFood = 2,
    Transportation = 3,
    Healthcare = 4,
    Education = 5,
    EntertainmentLeisure = 6,
    InsuranceInvestments = 7,
    DiningRestaurants = 8,
    PersonalCareClothing = 9,
    Miscellaneous = 10
}
