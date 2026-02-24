namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeShareDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
