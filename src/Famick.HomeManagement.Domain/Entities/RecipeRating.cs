namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// One household member's rating of a recipe. Each member has at most one per recipe; the household
/// sees the average of them all.
/// </summary>
public class RecipeRating : BaseTenantEntity
{
    /// <summary>The lowest rating, in half stars: one star.</summary>
    public const short MinHalfStars = 2;

    /// <summary>The highest rating, in half stars: five stars.</summary>
    public const short MaxHalfStars = 10;

    public Guid RecipeId { get; set; }

    /// <summary>
    /// The member who gave it. Null once they have left the household: the rating stays in the
    /// average without their name, as stock history does.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// The rating in half stars, from <see cref="MinHalfStars"/> to <see cref="MaxHalfStars"/>
    /// (9 is four and a half stars), so it is stored without rounding.
    /// </summary>
    public short HalfStars { get; set; }

    // Navigation properties

    public virtual Recipe Recipe { get; set; } = null!;

    public virtual User? User { get; set; }
}
