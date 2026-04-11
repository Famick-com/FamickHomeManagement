using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class ProductGroupMappingTests
{
    private readonly IMapper _mapper;

    public ProductGroupMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ProductGroupMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void ProductGroup_To_ProductGroupDto_MapsAllProperties()
    {
        var group = new ProductGroup
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Dairy",
            Description = "Dairy products",
            Products = new List<Product>
            {
                new() { Id = Guid.NewGuid(), Name = "Milk" },
                new() { Id = Guid.NewGuid(), Name = "Cheese" }
            },
            CreatedAt = DateTime.UtcNow
        };

        var dto = _mapper.Map<ProductGroupDto>(group);

        dto.Id.Should().Be(group.Id);
        dto.Name.Should().Be("Dairy");
        dto.Description.Should().Be("Dairy products");
        dto.ProductCount.Should().Be(2);
    }

    [Fact]
    public void ProductGroup_To_ProductGroupDto_HandlesNullProducts()
    {
        var group = new ProductGroup
        {
            Id = Guid.NewGuid(),
            Name = "Empty Group",
            Products = null!
        };

        var dto = _mapper.Map<ProductGroupDto>(group);

        dto.ProductCount.Should().Be(0);
    }

    [Fact]
    public void CreateProductGroupRequest_To_ProductGroup_MapsNameAndDescription()
    {
        var request = new CreateProductGroupRequest
        {
            Name = "Beverages",
            Description = "Drink items"
        };

        var entity = _mapper.Map<ProductGroup>(request);

        entity.Name.Should().Be("Beverages");
        entity.Description.Should().Be("Drink items");
    }

    [Fact]
    public void CreateProductGroupRequest_To_ProductGroup_IgnoresSystemFields()
    {
        var request = new CreateProductGroupRequest { Name = "Test" };

        var entity = _mapper.Map<ProductGroup>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.Products.Should().BeNull();
    }

    [Fact]
    public void UpdateProductGroupRequest_To_ProductGroup_IgnoresSystemFields()
    {
        var request = new UpdateProductGroupRequest
        {
            Name = "Updated Name",
            Description = "Updated desc"
        };

        var entity = _mapper.Map<ProductGroup>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.Name.Should().Be("Updated Name");
    }

    [Fact]
    public void Product_To_ProductSummaryDto_MapsWithNavigationProperties()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Milk",
            Description = "Whole milk",
            ProductGroup = new ProductGroup { Name = "Dairy" },
            ShoppingLocation = new ShoppingLocation { Name = "Kroger" }
        };

        var dto = _mapper.Map<ProductSummaryDto>(product);

        dto.Name.Should().Be("Milk");
        dto.ProductGroupName.Should().Be("Dairy");
        dto.ShoppingLocationName.Should().Be("Kroger");
    }

    [Fact]
    public void Product_To_ProductSummaryDto_HandlesNullNavigationProperties()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Unknown Product",
            ProductGroup = null,
            ShoppingLocation = null
        };

        var dto = _mapper.Map<ProductSummaryDto>(product);

        dto.Name.Should().Be("Unknown Product");
        dto.ProductGroupName.Should().BeNull();
        dto.ShoppingLocationName.Should().BeNull();
    }
}
