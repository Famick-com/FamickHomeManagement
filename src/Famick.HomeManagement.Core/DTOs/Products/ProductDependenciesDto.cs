namespace Famick.HomeManagement.Core.DTOs.Products;

public class ProductDependenciesDto
{
    public int StockEntryCount { get; set; }
    public int ShoppingListItemCount { get; set; }
    public int RecipeCount { get; set; }
    public List<string> ShoppingListNames { get; set; } = new();
    public bool CanForceDelete => RecipeCount == 0;
}
