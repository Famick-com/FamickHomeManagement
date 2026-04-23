namespace Famick.HomeManagement.Mobile;

public static class AppColors
{
    public static Color Household => Resolve("HouseholdColor", "#4CAF50");
    public static Color Business => Resolve("BusinessColor", "#2196F3");

    public static Color ForContactType(int contactType) =>
        contactType == 1 ? Business : Household;

    private static Color Resolve(string key, string fallbackHex) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Color.FromArgb(fallbackHex);
}
