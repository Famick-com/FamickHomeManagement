using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

/// <summary>
/// Recipe ratings between the stars the API speaks (1.0 to 5.0 in steps of 0.5) and the half stars
/// <see cref="RecipeRating"/> stores (2 to 10).
/// </summary>
public static class RecipeRatings
{
    public const decimal MinStars = RecipeRating.MinHalfStars / 2m;
    public const decimal MaxStars = RecipeRating.MaxHalfStars / 2m;

    /// <summary>Whether <paramref name="stars"/> is a whole or half star from one to five.</summary>
    public static bool IsValid(decimal stars) =>
        stars >= MinStars && stars <= MaxStars && stars * 2 == decimal.Truncate(stars * 2);

    public static short ToHalfStars(decimal stars) => (short)(stars * 2);

    public static decimal ToStars(short halfStars) => halfStars / 2m;

    /// <summary>The household average in stars, to one decimal place, or null when nobody has rated.</summary>
    public static decimal? Average(IReadOnlyCollection<short> halfStars) =>
        halfStars.Count == 0
            ? null
            : Math.Round(halfStars.Sum(h => (decimal)h) / halfStars.Count / 2m, 1, MidpointRounding.AwayFromZero);
}
