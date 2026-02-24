using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Validators.Contacts;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

public class ContactGroupValidatorTests
{
    private readonly CreateContactGroupRequestValidator _createValidator = new();
    private readonly UpdateContactGroupRequestValidator _updateValidator = new();

    #region CreateContactGroupRequest Validation

    [Fact]
    public void Create_ValidHouseholdGroup_ShouldPass()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Smith Family"
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_ValidBusinessGroup_ShouldPass()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Acme Corp",
            Website = "https://acme.com",
            BusinessCategory = "Manufacturing"
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_EmptyGroupName_ShouldFail()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = ""
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.GroupName);
    }

    [Fact]
    public void Create_NullGroupName_ShouldFail()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = null!
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.GroupName);
    }

    [Fact]
    public void Create_GroupNameTooLong_ShouldFail()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = new string('A', 201)
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.GroupName);
    }

    [Fact]
    public void Create_GroupNameMaxLength_ShouldPass()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = new string('A', 200)
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.GroupName);
    }

    [Fact]
    public void Create_NotesTooLong_ShouldFail()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Test",
            Notes = new string('N', 5001)
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Create_WebsiteTooLong_ShouldFail()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Test",
            Website = "https://" + new string('a', 500) + ".com"
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Website);
    }

    [Fact]
    public void Create_BusinessCategoryTooLong_ShouldFail()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Test",
            BusinessCategory = new string('C', 101)
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.BusinessCategory);
    }

    #endregion

    #region UpdateContactGroupRequest Validation

    [Fact]
    public void Update_ValidRequest_ShouldPass()
    {
        var request = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Updated Corp",
            Website = "https://updated.com",
            BusinessCategory = "Tech",
            IsActive = true
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_EmptyGroupName_ShouldFail()
    {
        var request = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "",
            IsActive = true
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.GroupName);
    }

    [Fact]
    public void Update_GroupNameTooLong_ShouldFail()
    {
        var request = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = new string('A', 201),
            IsActive = true
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.GroupName);
    }

    #endregion
}
