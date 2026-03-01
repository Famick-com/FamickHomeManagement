using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IProductAllergenService
{
    Task<ProductAllergenTagsDto> GetAsync(Guid productId, CancellationToken ct = default);
    Task<ProductAllergenTagsDto> UpdateAsync(Guid productId, UpdateProductAllergenTagsRequest request, CancellationToken ct = default);
}
