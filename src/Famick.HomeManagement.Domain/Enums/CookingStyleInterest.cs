namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Types of grocery shopping categories the household is interested in.
/// Used during product onboarding to filter template suggestions.
/// </summary>
public enum CookingStyleInterest
{
    FreshProduce = 0,
    DairyAndEggs = 1,
    MeatAndSeafood = 2,
    Baking = 3,
    InternationalFoods = 4,
    FrozenFoods = 5,
    BreakfastStaples = 6,
    CannedGoodsAndMealPrep = 7,
    Beverages = 8,
    CondimentsAndPantry = 9
}
