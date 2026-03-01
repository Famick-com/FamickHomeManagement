using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class ProductAllergenServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly ProductAllergenService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ProductAllergenServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _context = new HomeManagementDbContext(options, tenantProvider.Object);

        var logger = new Mock<ILogger<ProductAllergenService>>();

        _service = new ProductAllergenService(_context, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAsync_ExistingProduct_ReturnsTags()
    {
        var productId = Guid.NewGuid();
        _context.Products.Add(new Product
        {
            Id = productId,
            TenantId = _tenantId,
            Name = "Milk Chocolate",
            Allergens = new List<ProductAllergen>
            {
                new() { Id = Guid.NewGuid(), ProductId = productId, AllergenType = AllergenType.Milk },
                new() { Id = Guid.NewGuid(), ProductId = productId, AllergenType = AllergenType.Soybeans }
            },
            DietaryConflicts = new List<ProductDietaryConflict>
            {
                new() { Id = Guid.NewGuid(), ProductId = productId, DietaryPreference = DietaryPreference.Vegan }
            }
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetAsync(productId);

        result.Should().NotBeNull();
        result.ProductId.Should().Be(productId);
        result.Allergens.Should().HaveCount(2);
        result.Allergens.Should().Contain(AllergenType.Milk);
        result.Allergens.Should().Contain(AllergenType.Soybeans);
        result.DietaryConflicts.Should().HaveCount(1);
        result.DietaryConflicts.Should().Contain(DietaryPreference.Vegan);
    }

    [Fact]
    public async Task GetAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var act = () => _service.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesTags()
    {
        var productId = Guid.NewGuid();
        _context.Products.Add(new Product
        {
            Id = productId,
            TenantId = _tenantId,
            Name = "Test Product",
            Allergens = new List<ProductAllergen>(),
            DietaryConflicts = new List<ProductDietaryConflict>()
        });
        await _context.SaveChangesAsync();

        var request = new UpdateProductAllergenTagsRequest
        {
            Allergens = new List<AllergenType> { AllergenType.Wheat, AllergenType.Gluten },
            DietaryConflicts = new List<DietaryPreference> { DietaryPreference.GlutenFree }
        };

        var result = await _service.UpdateAsync(productId, request);

        result.Allergens.Should().HaveCount(2);
        result.Allergens.Should().Contain(AllergenType.Wheat);
        result.DietaryConflicts.Should().HaveCount(1);
        result.DietaryConflicts.Should().Contain(DietaryPreference.GlutenFree);
    }

    [Fact]
    public async Task UpdateAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        var request = new UpdateProductAllergenTagsRequest
        {
            Allergens = new List<AllergenType>(),
            DietaryConflicts = new List<DietaryPreference>()
        };

        var act = () => _service.UpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_DuplicateAllergens_DeduplicatesInput()
    {
        var productId = Guid.NewGuid();
        _context.Products.Add(new Product
        {
            Id = productId,
            TenantId = _tenantId,
            Name = "Duplicate Test",
            Allergens = new List<ProductAllergen>(),
            DietaryConflicts = new List<ProductDietaryConflict>()
        });
        await _context.SaveChangesAsync();

        var request = new UpdateProductAllergenTagsRequest
        {
            Allergens = new List<AllergenType> { AllergenType.Milk, AllergenType.Milk, AllergenType.Eggs },
            DietaryConflicts = new List<DietaryPreference>()
        };

        var result = await _service.UpdateAsync(productId, request);

        result.Allergens.Should().HaveCount(2);
    }
}
