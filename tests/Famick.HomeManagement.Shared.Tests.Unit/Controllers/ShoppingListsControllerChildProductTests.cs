using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

public class ShoppingListsControllerChildProductTests
{
    private readonly Mock<IShoppingListService> _shoppingListServiceMock;
    private readonly Mock<IStoreIntegrationService> _storeIntegrationServiceMock;
    private readonly Mock<ITenantProvider> _tenantProviderMock;
    private readonly ShoppingListsController _controller;

    public ShoppingListsControllerChildProductTests()
    {
        _shoppingListServiceMock = new Mock<IShoppingListService>();
        _storeIntegrationServiceMock = new Mock<IStoreIntegrationService>();
        _tenantProviderMock = new Mock<ITenantProvider>();

        var logger = new Mock<ILogger<ShoppingListsController>>();

        // Mock validators
        var createListValidator = new Mock<IValidator<CreateShoppingListRequest>>();
        var updateListValidator = new Mock<IValidator<UpdateShoppingListRequest>>();
        var addItemValidator = new Mock<IValidator<AddShoppingListItemRequest>>();
        var updateItemValidator = new Mock<IValidator<UpdateShoppingListItemRequest>>();
        var quickAddValidator = new Mock<IValidator<AddToShoppingListRequest>>();

        _tenantProviderMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        _controller = new ShoppingListsController(
            _shoppingListServiceMock.Object,
            _storeIntegrationServiceMock.Object,
            createListValidator.Object,
            updateListValidator.Object,
            addItemValidator.Object,
            updateItemValidator.Object,
            quickAddValidator.Object,
            _tenantProviderMock.Object,
            logger.Object);

        // Set up HttpContext for controller
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region GetChildProducts Tests

    [Fact]
    public async Task GetChildProducts_ReturnsChildList()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var children = new List<ShoppingListItemChildDto>
        {
            new() { ProductId = Guid.NewGuid(), ProductName = "Whole Milk" },
            new() { ProductId = Guid.NewGuid(), ProductName = "2% Milk" }
        };

        _shoppingListServiceMock
            .Setup(s => s.GetChildProductsForItemAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(children);

        var result = await _controller.GetChildProducts(listId, itemId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetChildProducts_Returns404WhenItemNotFound()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        _shoppingListServiceMock
            .Setup(s => s.GetChildProductsForItemAsync(itemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(nameof(ShoppingListItemDto), itemId));

        var result = await _controller.GetChildProducts(listId, itemId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CheckOffChild Tests

    [Fact]
    public async Task CheckOffChild_ReturnsUpdatedItem()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var request = new CheckOffChildRequest { ChildProductId = childId, Quantity = 1 };

        var updatedItem = new ShoppingListItemDto
        {
            Id = itemId,
            ChildPurchasedQuantity = 1
        };

        _shoppingListServiceMock
            .Setup(s => s.CheckOffChildAsync(itemId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedItem);

        var result = await _controller.CheckOffChild(listId, itemId, request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckOffChild_Returns400WhenValidationFails()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new CheckOffChildRequest { ChildProductId = Guid.NewGuid(), Quantity = 1 };

        _shoppingListServiceMock
            .Setup(s => s.CheckOffChildAsync(itemId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Product is not a child of parent"));

        var result = await _controller.CheckOffChild(listId, itemId, request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UncheckChild Tests

    [Fact]
    public async Task UncheckChild_ReturnsUpdatedItem()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var updatedItem = new ShoppingListItemDto
        {
            Id = itemId,
            ChildPurchasedQuantity = 0
        };

        _shoppingListServiceMock
            .Setup(s => s.UncheckChildAsync(itemId, childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedItem);

        var result = await _controller.UncheckChild(listId, itemId, childId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region SendChildToCart Tests

    [Fact]
    public async Task SendChildToCart_ReturnsResult()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var request = new SendChildToCartRequest { ChildProductId = childId, Quantity = 1 };

        var cartResult = new SendToCartResult
        {
            Success = true,
            ItemsSent = 1
        };

        _shoppingListServiceMock
            .Setup(s => s.SendChildToCartAsync(listId, itemId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartResult);

        var result = await _controller.SendChildToCart(listId, itemId, request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SendChildToCart_Returns404WhenListNotFound()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new SendChildToCartRequest { ChildProductId = Guid.NewGuid(), Quantity = 1 };

        _shoppingListServiceMock
            .Setup(s => s.SendChildToCartAsync(listId, itemId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(nameof(ShoppingListItemDto), listId));

        var result = await _controller.SendChildToCart(listId, itemId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region AddChild Tests

    [Fact]
    public async Task AddChild_ReturnsUpdatedItem()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new AddChildToParentRequest
        {
            ProductName = "Organic Milk",
            Quantity = 1
        };

        var updatedItem = new ShoppingListItemDto
        {
            Id = itemId,
            ChildPurchasedQuantity = 1
        };

        _shoppingListServiceMock
            .Setup(s => s.AddChildToParentAsync(itemId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedItem);

        var result = await _controller.AddChild(listId, itemId, request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region SearchStoreForChildren Tests

    [Fact]
    public async Task SearchStoreForChildren_ReturnsResults()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var query = "milk";

        var results = new List<StoreProductSearchResult>
        {
            new() { ExternalProductId = "EXT001", ProductName = "Whole Milk" },
            new() { ExternalProductId = "EXT002", ProductName = "2% Milk" }
        };

        _shoppingListServiceMock
            .Setup(s => s.SearchStoreForChildAsync(itemId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _controller.SearchStoreForChildren(listId, itemId, query, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SearchStoreForChildren_Returns400WhenQueryEmpty()
    {
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var result = await _controller.SearchStoreForChildren(listId, itemId, "", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
