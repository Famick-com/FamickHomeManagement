using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class FileUrlServiceTests
{
    private readonly Mock<IFileStorageService> _mockFileStorage;
    private readonly Mock<IFileAccessTokenService> _mockTokenService;
    private readonly FileUrlService _sut;

    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _imageId = Guid.NewGuid();
    private const string FakeToken = "fake-signed-token";
    private const string FakeLocalUrl = "https://app.example.com/api/v1/products/123/images/456/download?token=fake-signed-token";

    public FileUrlServiceTests()
    {
        _mockFileStorage = new Mock<IFileStorageService>();
        _mockTokenService = new Mock<IFileAccessTokenService>();
        _mockTokenService
            .Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(FakeToken);

        _sut = new FileUrlService(_mockFileStorage.Object, _mockTokenService.Object);
    }

    #region GetProductImageUrl

    [Fact]
    public void GetProductImageUrl_ReturnsExternalThumbnailUrl_WhenPresent()
    {
        var result = _sut.GetProductImageUrl(
            _productId, _imageId, _tenantId,
            "https://images.example.com/thumb.jpg", "https://images.example.com/full.jpg", "local.jpg");

        result.Should().Be("https://images.example.com/thumb.jpg");
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetProductImageUrl_ReturnsExternalUrl_WhenNoThumbnail()
    {
        var result = _sut.GetProductImageUrl(
            _productId, _imageId, _tenantId,
            null, "https://images.example.com/full.jpg", "local.jpg");

        result.Should().Be("https://images.example.com/full.jpg");
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetProductImageUrl_ReturnsTokenSignedUrl_WhenOnlyFileNamePresent()
    {
        _mockFileStorage
            .Setup(f => f.GetProductImageUrl(_productId, _imageId, FakeToken))
            .Returns(FakeLocalUrl);

        var result = _sut.GetProductImageUrl(
            _productId, _imageId, _tenantId,
            null, null, "image.jpg");

        result.Should().Be(FakeLocalUrl);
        _mockTokenService.Verify(t => t.GenerateToken("product-image", _imageId, _tenantId, It.IsAny<int>()), Times.Once);
        _mockFileStorage.Verify(f => f.GetProductImageUrl(_productId, _imageId, FakeToken), Times.Once);
    }

    [Fact]
    public void GetProductImageUrl_ReturnsNull_WhenAllFieldsNull()
    {
        var result = _sut.GetProductImageUrl(_productId, _imageId, _tenantId, null, null, null);

        result.Should().BeNull();
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetProductImageUrl_ReturnsNull_WhenFileNameIsEmpty()
    {
        var result = _sut.GetProductImageUrl(_productId, _imageId, _tenantId, null, null, "");

        result.Should().BeNull();
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region GetRecipeImageUrl

    [Fact]
    public void GetRecipeImageUrl_ReturnsExternalUrl_WhenPresent()
    {
        var result = _sut.GetRecipeImageUrl(
            _productId, _imageId, _tenantId,
            "https://images.example.com/recipe.jpg", "local.jpg");

        result.Should().Be("https://images.example.com/recipe.jpg");
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetRecipeImageUrl_ReturnsTokenSignedUrl_WhenOnlyFileNamePresent()
    {
        var recipeId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var expectedUrl = "https://app.example.com/api/v1/recipes/123/images/456/download?token=fake";
        _mockFileStorage
            .Setup(f => f.GetRecipeImageUrl(recipeId, imageId, FakeToken))
            .Returns(expectedUrl);

        var result = _sut.GetRecipeImageUrl(recipeId, imageId, _tenantId, null, "recipe-image.jpg");

        result.Should().Be(expectedUrl);
        _mockTokenService.Verify(t => t.GenerateToken("recipe-image", imageId, _tenantId, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetRecipeImageUrl_ReturnsNull_WhenAllFieldsNull()
    {
        var result = _sut.GetRecipeImageUrl(_productId, _imageId, _tenantId, null, null);
        result.Should().BeNull();
    }

    #endregion

    #region GetRecipeStepImageUrl

    [Fact]
    public void GetRecipeStepImageUrl_ReturnsExternalUrl_WhenPresent()
    {
        var result = _sut.GetRecipeStepImageUrl(
            _productId, _imageId, _tenantId,
            "https://images.example.com/step.jpg", "local.jpg");

        result.Should().Be("https://images.example.com/step.jpg");
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetRecipeStepImageUrl_ReturnsTokenSignedUrl_WhenOnlyFileNamePresent()
    {
        var recipeId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var expectedUrl = "https://app.example.com/api/v1/recipes/123/steps/456/image/download?token=fake";
        _mockFileStorage
            .Setup(f => f.GetRecipeStepImageUrl(recipeId, stepId, FakeToken))
            .Returns(expectedUrl);

        var result = _sut.GetRecipeStepImageUrl(recipeId, stepId, _tenantId, null, "step-image.jpg");

        result.Should().Be(expectedUrl);
        _mockTokenService.Verify(t => t.GenerateToken("recipe-step-image", stepId, _tenantId, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetRecipeStepImageUrl_ReturnsNull_WhenAllFieldsNull()
    {
        var result = _sut.GetRecipeStepImageUrl(_productId, _imageId, _tenantId, null, null);
        result.Should().BeNull();
    }

    #endregion

    #region GetContactProfileImageUrl

    [Fact]
    public void GetContactProfileImageUrl_ReturnsTokenSignedUrl_WhenFileNamePresent()
    {
        var contactId = Guid.NewGuid();
        var expectedUrl = "https://app.example.com/api/v1/contacts/123/profile-image?token=fake";
        _mockFileStorage
            .Setup(f => f.GetContactProfileImageUrl(contactId, FakeToken))
            .Returns(expectedUrl);

        var result = _sut.GetContactProfileImageUrl(contactId, _tenantId, "profile.jpg");

        result.Should().Be(expectedUrl);
        _mockTokenService.Verify(t => t.GenerateToken("contact-profile-image", contactId, _tenantId, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetContactProfileImageUrl_ReturnsNull_WhenFileNameNull()
    {
        var result = _sut.GetContactProfileImageUrl(Guid.NewGuid(), _tenantId, null);

        result.Should().BeNull();
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetContactProfileImageUrl_ReturnsNull_WhenFileNameEmpty()
    {
        var result = _sut.GetContactProfileImageUrl(Guid.NewGuid(), _tenantId, "");

        result.Should().BeNull();
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region GetEquipmentDocumentUrl

    [Fact]
    public void GetEquipmentDocumentUrl_ReturnsTokenSignedUrl_WhenFileNamePresent()
    {
        var documentId = Guid.NewGuid();
        var expectedUrl = "https://app.example.com/api/v1/equipment/documents/123/download?token=fake";
        _mockFileStorage
            .Setup(f => f.GetEquipmentDocumentUrl(documentId, FakeToken))
            .Returns(expectedUrl);

        var result = _sut.GetEquipmentDocumentUrl(documentId, _tenantId, "document.pdf");

        result.Should().Be(expectedUrl);
        _mockTokenService.Verify(t => t.GenerateToken("equipment-document", documentId, _tenantId, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetEquipmentDocumentUrl_ReturnsNull_WhenFileNameNull()
    {
        var result = _sut.GetEquipmentDocumentUrl(Guid.NewGuid(), _tenantId, null);
        result.Should().BeNull();
    }

    #endregion

    #region GetStorageBinPhotoUrl

    [Fact]
    public void GetStorageBinPhotoUrl_ReturnsTokenSignedUrl_WhenFileNamePresent()
    {
        var photoId = Guid.NewGuid();
        var expectedUrl = "https://app.example.com/api/v1/storage-bins/photos/123/download?token=fake";
        _mockFileStorage
            .Setup(f => f.GetStorageBinPhotoUrl(photoId, FakeToken))
            .Returns(expectedUrl);

        var result = _sut.GetStorageBinPhotoUrl(photoId, _tenantId, "photo.jpg");

        result.Should().Be(expectedUrl);
        _mockTokenService.Verify(t => t.GenerateToken("storage-bin-photo", photoId, _tenantId, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetStorageBinPhotoUrl_ReturnsNull_WhenFileNameNull()
    {
        var result = _sut.GetStorageBinPhotoUrl(Guid.NewGuid(), _tenantId, null);
        result.Should().BeNull();
    }

    #endregion

    #region GetMasterProductImageUrl

    [Fact]
    public void GetMasterProductImageUrl_DelegatesToFileStorage()
    {
        var masterProductId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var expectedUrl = "https://app.example.com/api/v1/master-products/123/images/456/download";
        _mockFileStorage
            .Setup(f => f.GetMasterProductImageUrl(masterProductId, imageId, null))
            .Returns(expectedUrl);

        var result = _sut.GetMasterProductImageUrl(masterProductId, imageId);

        result.Should().Be(expectedUrl);
        _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Resource Type String Verification

    [Theory]
    [InlineData("product-image")]
    public void GetProductImageUrl_UsesCorrectResourceType(string expectedResourceType)
    {
        _mockFileStorage.Setup(f => f.GetProductImageUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>())).Returns("url");

        _sut.GetProductImageUrl(_productId, _imageId, _tenantId, null, null, "file.jpg");

        _mockTokenService.Verify(t => t.GenerateToken(expectedResourceType, _imageId, _tenantId, It.IsAny<int>()));
    }

    [Theory]
    [InlineData("recipe-image")]
    public void GetRecipeImageUrl_UsesCorrectResourceType(string expectedResourceType)
    {
        _mockFileStorage.Setup(f => f.GetRecipeImageUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>())).Returns("url");

        _sut.GetRecipeImageUrl(_productId, _imageId, _tenantId, null, "file.jpg");

        _mockTokenService.Verify(t => t.GenerateToken(expectedResourceType, _imageId, _tenantId, It.IsAny<int>()));
    }

    [Theory]
    [InlineData("recipe-step-image")]
    public void GetRecipeStepImageUrl_UsesCorrectResourceType(string expectedResourceType)
    {
        _mockFileStorage.Setup(f => f.GetRecipeStepImageUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>())).Returns("url");

        _sut.GetRecipeStepImageUrl(_productId, _imageId, _tenantId, null, "file.jpg");

        _mockTokenService.Verify(t => t.GenerateToken(expectedResourceType, _imageId, _tenantId, It.IsAny<int>()));
    }

    [Theory]
    [InlineData("contact-profile-image")]
    public void GetContactProfileImageUrl_UsesCorrectResourceType(string expectedResourceType)
    {
        _mockFileStorage.Setup(f => f.GetContactProfileImageUrl(It.IsAny<Guid>(), It.IsAny<string>())).Returns("url");

        _sut.GetContactProfileImageUrl(_imageId, _tenantId, "file.jpg");

        _mockTokenService.Verify(t => t.GenerateToken(expectedResourceType, _imageId, _tenantId, It.IsAny<int>()));
    }

    [Theory]
    [InlineData("equipment-document")]
    public void GetEquipmentDocumentUrl_UsesCorrectResourceType(string expectedResourceType)
    {
        _mockFileStorage.Setup(f => f.GetEquipmentDocumentUrl(It.IsAny<Guid>(), It.IsAny<string>())).Returns("url");

        _sut.GetEquipmentDocumentUrl(_imageId, _tenantId, "file.pdf");

        _mockTokenService.Verify(t => t.GenerateToken(expectedResourceType, _imageId, _tenantId, It.IsAny<int>()));
    }

    [Theory]
    [InlineData("storage-bin-photo")]
    public void GetStorageBinPhotoUrl_UsesCorrectResourceType(string expectedResourceType)
    {
        _mockFileStorage.Setup(f => f.GetStorageBinPhotoUrl(It.IsAny<Guid>(), It.IsAny<string>())).Returns("url");

        _sut.GetStorageBinPhotoUrl(_imageId, _tenantId, "file.jpg");

        _mockTokenService.Verify(t => t.GenerateToken(expectedResourceType, _imageId, _tenantId, It.IsAny<int>()));
    }

    #endregion
}
