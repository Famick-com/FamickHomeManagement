using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

public class ProductOnboardingControllerTests
{
    private readonly Mock<IProductOnboardingService> _mockService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly ProductOnboardingController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ProductOnboardingControllerTests()
    {
        _mockService = new Mock<IProductOnboardingService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var logger = new Mock<ILogger<ProductOnboardingController>>();

        _controller = new ProductOnboardingController(
            _mockService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region GetState

    [Fact]
    public async Task GetState_ShouldReturnOk()
    {
        _mockService.Setup(s => s.GetStateAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductOnboardingStateDto
            {
                HasCompletedOnboarding = false,
                ProductsCreatedCount = 0
            });

        var result = await _controller.GetState(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetState_CallsServiceWithTenantId()
    {
        _mockService.Setup(s => s.GetStateAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductOnboardingStateDto());

        await _controller.GetState(CancellationToken.None);

        _mockService.Verify(s => s.GetStateAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Preview

    [Fact]
    public async Task Preview_ShouldReturnOk()
    {
        var answers = new ProductOnboardingAnswersDto { HasBaby = true };
        _mockService.Setup(s => s.PreviewAsync(answers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductOnboardingPreviewResponse
            {
                TotalMasterProducts = 100,
                FilteredCount = 50,
                Categories = new List<MasterProductCategoryGroup>()
            });

        var result = await _controller.Preview(answers, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Preview_CallsServiceWithAnswers()
    {
        var answers = new ProductOnboardingAnswersDto
        {
            HasPets = true,
            TrackHouseholdSupplies = true
        };
        _mockService.Setup(s => s.PreviewAsync(answers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductOnboardingPreviewResponse());

        await _controller.Preview(answers, CancellationToken.None);

        _mockService.Verify(s => s.PreviewAsync(answers, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Complete

    [Fact]
    public async Task Complete_ShouldReturnOk()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto()
        };
        _mockService.Setup(s => s.CompleteAsync(_tenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductOnboardingCompleteResponse
            {
                ProductsCreated = 10,
                ProductsSkipped = 2
            });

        var result = await _controller.Complete(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Complete_CallsServiceWithTenantIdAndRequest()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto()
        };
        _mockService.Setup(s => s.CompleteAsync(_tenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductOnboardingCompleteResponse());

        await _controller.Complete(request, CancellationToken.None);

        _mockService.Verify(
            s => s.CompleteAsync(_tenantId, request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Reset

    [Fact]
    public async Task Reset_ShouldReturnNoContent()
    {
        _mockService.Setup(s => s.ResetAsync(_tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Reset(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Reset_CallsServiceWithTenantId()
    {
        _mockService.Setup(s => s.ResetAsync(_tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.Reset(CancellationToken.None);

        _mockService.Verify(s => s.ResetAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
