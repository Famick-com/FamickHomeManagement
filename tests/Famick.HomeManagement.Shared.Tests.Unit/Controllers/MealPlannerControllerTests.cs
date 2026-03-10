using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

public class MealTypesControllerTests
{
    private readonly Mock<IMealTypeService> _mockService;
    private readonly Mock<IValidator<CreateMealTypeRequest>> _mockCreateValidator;
    private readonly Mock<IValidator<UpdateMealTypeRequest>> _mockUpdateValidator;
    private readonly MealTypesController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();

    public MealTypesControllerTests()
    {
        _mockService = new Mock<IMealTypeService>();
        _mockCreateValidator = new Mock<IValidator<CreateMealTypeRequest>>();
        _mockUpdateValidator = new Mock<IValidator<UpdateMealTypeRequest>>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        var logger = new Mock<ILogger<MealTypesController>>();

        _controller = new MealTypesController(
            _mockService.Object,
            _mockCreateValidator.Object,
            _mockUpdateValidator.Object,
            mockTenantProvider.Object,
            logger.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task List_ReturnsOk()
    {
        _mockService.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealTypeDto> { new() { Id = Guid.NewGuid(), Name = "Breakfast" } });

        var result = await _controller.List(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(s => s.SeedDefaultsForTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_EmptyResult_SeedsDefaultsAndRetries()
    {
        var callCount = 0;
        _mockService.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new List<MealTypeDto>()
                    : new List<MealTypeDto> { new() { Id = Guid.NewGuid(), Name = "Breakfast" } };
            });

        var result = await _controller.List(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(s => s.SeedDefaultsForTenantAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.Verify(s => s.ListAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        var request = new CreateMealTypeRequest { Name = "Brunch", SortOrder = 5 };
        _mockCreateValidator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealTypeDto { Id = Guid.NewGuid(), Name = "Brunch", SortOrder = 5 });

        var result = await _controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsBadRequest()
    {
        var request = new CreateMealTypeRequest { Name = "" };
        var failures = new List<ValidationFailure> { new("Name", "Name is required") };
        _mockCreateValidator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsBadRequest()
    {
        var request = new CreateMealTypeRequest { Name = "Breakfast", SortOrder = 0 };
        _mockCreateValidator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Duplicate name"));

        var result = await _controller.Create(request, CancellationToken.None);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateMealTypeRequest { Name = "Test", SortOrder = 0 };
        _mockUpdateValidator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockService.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_Success_ReturnsNoContent()
    {
        _mockService.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        _mockService.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

public class MealsControllerTests
{
    private readonly Mock<IMealService> _mockService;
    private readonly MealsController _controller;

    public MealsControllerTests()
    {
        _mockService = new Mock<IMealService>();
        var mockCreateValidator = new Mock<IValidator<CreateMealRequest>>();
        var mockUpdateValidator = new Mock<IValidator<UpdateMealRequest>>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        var logger = new Mock<ILogger<MealsController>>();

        _controller = new MealsController(
            _mockService.Object,
            mockCreateValidator.Object,
            mockUpdateValidator.Object,
            mockTenantProvider.Object,
            logger.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetById_ExistingMeal_ReturnsOk()
    {
        var mealId = Guid.NewGuid();
        _mockService.Setup(s => s.GetByIdAsync(mealId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealDto { Id = mealId, Name = "Test" });

        var result = await _controller.GetById(mealId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        _mockService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealDto?)null);

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_ReferencedByPlan_ReturnsBadRequest()
    {
        _mockService.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Referenced by meal plan"));

        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetNutrition_NotFound_Returns404()
    {
        _mockService.Setup(s => s.GetNutritionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.GetNutrition(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

public class MealPlansControllerTests
{
    private readonly Mock<IMealPlanService> _mockService;
    private readonly Mock<IValidator<CreateMealPlanEntryRequest>> _mockCreateEntryValidator;
    private readonly MealPlansController _controller;
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public MealPlansControllerTests()
    {
        _mockService = new Mock<IMealPlanService>();
        _mockCreateEntryValidator = new Mock<IValidator<CreateMealPlanEntryRequest>>();
        var mockUpdateEntryValidator = new Mock<IValidator<UpdateMealPlanEntryRequest>>();
        var mockGenerateValidator = new Mock<IValidator<GenerateShoppingListRequest>>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        var logger = new Mock<ILogger<MealPlansController>>();

        var mockCreateBatchCookItemValidator = new Mock<IValidator<CreateBatchCookItemRequest>>();
        var mockLinkBatchCookItemValidator = new Mock<IValidator<LinkBatchCookItemRequest>>();

        _controller = new MealPlansController(
            _mockService.Object,
            _mockCreateEntryValidator.Object,
            mockUpdateEntryValidator.Object,
            mockGenerateValidator.Object,
            mockCreateBatchCookItemValidator.Object,
            mockLinkBatchCookItemValidator.Object,
            mockTenantProvider.Object,
            logger.Object);

        // Set up authenticated user
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        _mockService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlanDto?)null);

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        _mockService.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddEntry_ConcurrencyConflict_Returns409()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };
        _mockCreateEntryValidator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockService.Setup(s => s.AddEntryAsync(It.IsAny<Guid>(), request, It.IsAny<uint>(), _userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MealPlanConcurrencyException(Guid.NewGuid()));

        var result = await _controller.AddEntry(Guid.NewGuid(), request, 1, CancellationToken.None);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task AddEntry_NoAuthenticatedUser_ReturnsUnauthorized()
    {
        // Override with unauthenticated context
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };
        _mockCreateEntryValidator.Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var result = await _controller.AddEntry(Guid.NewGuid(), request, 1, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetTodaysMeals_ReturnsOk()
    {
        _mockService.Setup(s => s.GetTodaysMealsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodaysMealsDto
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                MealGroups = new List<TodaysMealGroupDto>()
            });

        var result = await _controller.GetTodaysMeals(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}

public class MealPlannerControllerTests
{
    private readonly Mock<IMealPlannerOnboardingService> _mockService;
    private readonly Mock<IMealTypeService> _mockMealTypeService;
    private readonly MealPlannerController _controller;
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private readonly Guid _tenantId = Guid.NewGuid();

    public MealPlannerControllerTests()
    {
        _mockService = new Mock<IMealPlannerOnboardingService>();
        _mockMealTypeService = new Mock<IMealTypeService>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        var logger = new Mock<ILogger<MealPlannerController>>();

        _controller = new MealPlannerController(
            _mockService.Object,
            _mockMealTypeService.Object,
            mockTenantProvider.Object,
            logger.Object);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetOnboarding_Authenticated_ReturnsOk()
    {
        _mockService.Setup(s => s.GetOnboardingStateAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingStateDto
            {
                HasCompletedOnboarding = false,
                CollapsedMealTypeIds = new List<Guid>()
            });

        var result = await _controller.GetOnboarding(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOnboarding_NoUser_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await _controller.GetOnboarding(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SaveOnboarding_WithMealTypes_CallsCreateFromOnboarding()
    {
        var selections = new List<OnboardingMealTypeSelection>
        {
            new() { Name = "Breakfast", Color = "#FFA726" },
            new() { Name = "Dinner", Color = "#42A5F5" }
        };
        var request = new SaveOnboardingRequest
        {
            PlanningStyle = PlanningStyle.WeekAtAGlance,
            MealTypes = selections
        };
        _mockService.Setup(s => s.SaveOnboardingAsync(_userId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SaveOnboarding(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockMealTypeService.Verify(
            s => s.CreateFromOnboardingAsync(_tenantId, selections, It.IsAny<CancellationToken>()), Times.Once);
        _mockMealTypeService.Verify(
            s => s.SeedDefaultsForTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveOnboarding_NoMealTypes_SeedsDefaults()
    {
        var request = new SaveOnboardingRequest { PlanningStyle = PlanningStyle.WeekAtAGlance };
        _mockService.Setup(s => s.SaveOnboardingAsync(_userId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SaveOnboarding(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockMealTypeService.Verify(
            s => s.SeedDefaultsForTenantAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMealTypeService.Verify(
            s => s.CreateFromOnboardingAsync(It.IsAny<Guid>(), It.IsAny<List<OnboardingMealTypeSelection>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DismissTip_Authenticated_ReturnsNoContent()
    {
        _mockService.Setup(s => s.DismissTipAsync(_userId, "batch-cooking", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DismissTip("batch-cooking", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }
}
