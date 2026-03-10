using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Core.Validators.ProductOnboarding;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

public class ProductOnboardingValidatorTests
{
    private readonly ProductOnboardingCompleteRequestValidator _validator = new();

    #region Valid Requests

    [Fact]
    public void ValidRequest_WithSelectedIds_ShouldPass()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto
            {
                HasBaby = false,
                HasPets = true,
                DietaryPreferences = new List<string> { "Vegan" },
                Allergens = new List<string> { "Milk" }
            }
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequest_SingleSelectedId_ShouldPass()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto()
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region SelectedMasterProductIds Validation

    [Fact]
    public void EmptySelectedMasterProductIds_ShouldPass()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid>(),
            Answers = new ProductOnboardingAnswersDto()
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Answers Validation

    [Fact]
    public void NullAnswers_ShouldFail()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = null!
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Answers)
            .WithErrorMessage("Questionnaire answers are required.");
    }

    #endregion

    #region Answers With Default Values

    [Fact]
    public void DefaultAnswers_ShouldPass()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto()
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnswersWithAllFieldsPopulated_ShouldPass()
    {
        var request = new ProductOnboardingCompleteRequest
        {
            SelectedMasterProductIds = new List<Guid> { Guid.NewGuid() },
            Answers = new ProductOnboardingAnswersDto
            {
                HasBaby = true,
                HasPets = true,
                TrackHouseholdSupplies = true,
                TrackPersonalCare = true,
                TrackPharmacy = true,
                DietaryPreferences = new List<string>
                {
                    "Vegetarian",
                    "GlutenFree"
                },
                Allergens = new List<string>
                {
                    "Milk",
                    "Peanuts",
                    "TreeNuts"
                }
            }
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
