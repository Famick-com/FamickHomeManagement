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

/// <summary>
/// The profile page shows the URL an upload returns without reloading, so it has to be
/// the same signed, versioned URL the contact DTOs carry, built from the new file.
/// </summary>
public class ContactProfileImageUploadTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private readonly HomeManagementDbContext _context;
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IFileUrlService> _fileUrls = new();
    private readonly ContactService _service;

    public ContactProfileImageUploadTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        tenantProvider.Setup(t => t.UserId).Returns(_userId);

        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantProvider.Object);

        _service = new ContactService(
            _context,
            tenantProvider.Object,
            _fileStorage.Object,
            _fileUrls.Object,
            new AddressHasher(new PassThroughAddressCanonicalizer()),
            Mock.Of<ILogger<ContactService>>());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task UploadReturnsTheSignedVersionedUrlForTheNewFile()
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Pat",
            CreatedByUserId = _userId,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true,
            ProfileImageFileName = "profile_old.png",
        };
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();

        _fileStorage
            .Setup(f => f.SaveContactProfileImageAsync(contact.Id, It.IsAny<Stream>(), "logo.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("profile_new.png");
        _fileUrls
            .Setup(u => u.GetContactProfileImageUrl(contact.Id, _tenantId, "profile_new.png"))
            .Returns("https://app.example.com/api/v1/contacts/1/profile-image?token=t&v=new");

        var url = await _service.UploadProfileImageAsync(contact.Id, new MemoryStream([1, 2, 3]), "logo.png");

        url.Should().Be("https://app.example.com/api/v1/contacts/1/profile-image?token=t&v=new");
        _fileStorage.Verify(f => f.DeleteContactProfileImageAsync(contact.Id, "profile_old.png", It.IsAny<CancellationToken>()));
    }
}
