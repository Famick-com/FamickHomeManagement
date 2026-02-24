using Famick.HomeManagement.Core.DTOs.Equipment;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for EquipmentController focusing on document metadata functionality
/// </summary>
public class EquipmentControllerTests
{
    private readonly Mock<IEquipmentService> _mockEquipmentService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly EquipmentController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public EquipmentControllerTests()
    {
        _mockEquipmentService = new Mock<IEquipmentService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var mockFileStorage = new Mock<IFileStorageService>();
        var mockTokenService = new Mock<IFileAccessTokenService>();
        var logger = new Mock<ILogger<EquipmentController>>();

        _controller = new EquipmentController(
            _mockEquipmentService.Object,
            mockFileStorage.Object,
            mockTokenService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region Document Metadata Tests

    [Fact]
    public async Task UpdateDocument_WithDisplayName_ReturnsUpdatedDocument()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var request = new UpdateEquipmentDocumentRequest
        {
            DisplayName = "Updated Document Name",
            TagId = null,
            SortOrder = 0
        };

        var updatedDocument = new EquipmentDocumentDto
        {
            Id = documentId,
            DisplayName = "Updated Document Name",
            OriginalFileName = "original.pdf",
            ContentType = "application/pdf"
        };

        _mockEquipmentService
            .Setup(s => s.UpdateDocumentAsync(documentId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDocument);

        // Act
        var result = await _controller.UpdateDocument(documentId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDocument = okResult.Value.Should().BeAssignableTo<EquipmentDocumentDto>().Subject;
        returnedDocument.DisplayName.Should().Be("Updated Document Name");
    }

    [Fact]
    public async Task UpdateDocument_WithTagId_ReturnsUpdatedDocument()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var request = new UpdateEquipmentDocumentRequest
        {
            DisplayName = null,
            TagId = tagId,
            SortOrder = 0
        };

        var updatedDocument = new EquipmentDocumentDto
        {
            Id = documentId,
            DisplayName = null,
            TagId = tagId,
            TagName = "Manual",
            OriginalFileName = "manual.pdf",
            ContentType = "application/pdf"
        };

        _mockEquipmentService
            .Setup(s => s.UpdateDocumentAsync(documentId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDocument);

        // Act
        var result = await _controller.UpdateDocument(documentId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDocument = okResult.Value.Should().BeAssignableTo<EquipmentDocumentDto>().Subject;
        returnedDocument.TagId.Should().Be(tagId);
        returnedDocument.TagName.Should().Be("Manual");
    }

    [Fact]
    public async Task UpdateDocument_ChangesDisplayNameAndTag_ReturnsUpdatedDocument()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var request = new UpdateEquipmentDocumentRequest
        {
            DisplayName = "Warranty Certificate",
            TagId = tagId,
            SortOrder = 1
        };

        var updatedDocument = new EquipmentDocumentDto
        {
            Id = documentId,
            DisplayName = "Warranty Certificate",
            TagId = tagId,
            TagName = "Warranty",
            SortOrder = 1,
            OriginalFileName = "warranty.pdf",
            ContentType = "application/pdf"
        };

        _mockEquipmentService
            .Setup(s => s.UpdateDocumentAsync(documentId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDocument);

        // Act
        var result = await _controller.UpdateDocument(documentId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDocument = okResult.Value.Should().BeAssignableTo<EquipmentDocumentDto>().Subject;
        returnedDocument.DisplayName.Should().Be("Warranty Certificate");
        returnedDocument.TagId.Should().Be(tagId);
        returnedDocument.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task UpdateDocument_DocumentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var request = new UpdateEquipmentDocumentRequest
        {
            DisplayName = "Some Name",
            TagId = null,
            SortOrder = 0
        };

        _mockEquipmentService
            .Setup(s => s.UpdateDocumentAsync(documentId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Document with ID {documentId} not found"));

        // Act
        var act = async () => await _controller.UpdateDocument(documentId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateDocument_ServiceCalledWithCorrectParameters()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var request = new UpdateEquipmentDocumentRequest
        {
            DisplayName = "Test Name",
            TagId = tagId,
            SortOrder = 5
        };

        _mockEquipmentService
            .Setup(s => s.UpdateDocumentAsync(documentId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EquipmentDocumentDto { Id = documentId });

        // Act
        await _controller.UpdateDocument(documentId, request, CancellationToken.None);

        // Assert
        _mockEquipmentService.Verify(
            s => s.UpdateDocumentAsync(
                documentId,
                It.Is<UpdateEquipmentDocumentRequest>(r =>
                    r.DisplayName == "Test Name" &&
                    r.TagId == tagId &&
                    r.SortOrder == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
