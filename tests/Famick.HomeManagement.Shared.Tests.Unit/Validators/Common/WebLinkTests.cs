using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Equipment;
using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Platform;
using Famick.HomeManagement.Core.Validators.Common;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators.Common;

/// <summary>
/// Links people type in are opened by the web and mobile apps, so only http and https links are
/// stored, and only those are rendered as links.
/// </summary>
public class WebLinkTests : IDisposable
{
    private const string ScriptLink = "javascript:alert(1)";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public WebLinkTests()
    {
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _tenantProvider.Setup(t => t.UserId).Returns(_userId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _tenantProvider.Object);
    }

    public void Dispose() => _context.Dispose();

    [Theory]
    [InlineData("https://example.com/rules.pdf")]
    [InlineData("http://example.com")]
    [InlineData("  https://example.com/padded  ")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void Web_urls_are_links(string value)
    {
        WebLink.IsWebUrl(value).Should().BeTrue();
        WebLink.SafeHref(value).Should().Be(value.Trim());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("com.famick.homemanagement://oauth/callback?code=x")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("example.com/no-scheme")]
    [InlineData("/relative/path")]
    [InlineData("https://")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_not_a_link(string? value)
    {
        WebLink.IsWebUrl(value).Should().BeFalse();
        WebLink.SafeHref(value).Should().BeNull();
    }

    [Fact]
    public void An_empty_link_is_allowed_on_save() =>
        FluentActions.Invoking(() => WebLink.Ensure(null, "Url")).Should().NotThrow();

    [Fact]
    public async Task A_social_media_profile_link_must_be_a_web_url()
    {
        var contact = SeedContact();
        var service = Contacts();

        await Rejects(() => service.AddSocialMediaAsync(contact.Id,
            new AddSocialMediaRequest { Service = SocialMediaService.Facebook, Username = "x", ProfileUrl = ScriptLink }));

        var added = await service.AddSocialMediaAsync(contact.Id,
            new AddSocialMediaRequest { Service = SocialMediaService.Facebook, Username = "x", ProfileUrl = "https://facebook.com/x" });
        await Rejects(() => service.UpdateSocialMediaAsync(added.Id,
            new AddSocialMediaRequest { Service = SocialMediaService.Facebook, Username = "x", ProfileUrl = ScriptLink }));
    }

    [Fact]
    public async Task Device_sync_drops_a_profile_link_that_is_not_a_web_url_and_keeps_the_rest()
    {
        // A phone's address book can hold app links; failing the whole sync over one would be worse.
        var contact = SeedContact();

        await Contacts().ApplyDeviceSyncUpdateAsync(contact.Id, new DeviceSyncUpdateRequest
        {
            FirstName = "Synced",
            SocialMedia =
            {
                new DeviceSyncSocialEntry { Service = (int)SocialMediaService.Facebook, Username = "a", ProfileUrl = ScriptLink },
                new DeviceSyncSocialEntry { Service = (int)SocialMediaService.Instagram, Username = "b", ProfileUrl = "https://instagram.com/b" },
            },
        });

        var stored = await _context.ContactSocialMedia.Where(s => s.ContactId == contact.Id).ToListAsync();
        stored.Should().HaveCount(2);
        stored.Single(s => s.Username == "a").ProfileUrl.Should().BeNull();
        stored.Single(s => s.Username == "b").ProfileUrl.Should().Be("https://instagram.com/b");
    }

    [Fact]
    public async Task Property_links_and_the_hoa_rules_link_must_be_web_urls()
    {
        var homes = new HomeService(_context, NullLogger<HomeService>.Instance);

        await Rejects(() => homes.SetupHomeAsync(new HomeSetupRequest { HoaRulesLink = ScriptLink }));
        await homes.SetupHomeAsync(new HomeSetupRequest());

        await Rejects(() => homes.UpdateHomeAsync(new UpdateHomeRequest { HoaRulesLink = ScriptLink }));
        await Rejects(() => homes.AddPropertyLinkAsync(new CreatePropertyLinkRequest { Label = "Assessor", Url = ScriptLink }));

        var link = await homes.AddPropertyLinkAsync(new CreatePropertyLinkRequest { Label = "Assessor", Url = "https://county.example/parcel" });
        await Rejects(() => homes.UpdatePropertyLinkAsync(link.Id, new UpdatePropertyLinkRequest { Label = "Assessor", Url = ScriptLink }));
    }

    [Fact]
    public async Task The_setup_wizard_hoa_rules_link_must_be_a_web_url()
    {
        var wizard = new WizardService(_context, _tenantProvider.Object, Mock.Of<IContactService>(),
            Mock.Of<IUserManagementService>(), Mock.Of<IMealTypeService>(), Mock.Of<IFileStorageService>(),
            Mock.Of<IAddressHasher>(), Mock.Of<IServerConfigService>(), Mock.Of<IPlatformInfo>(),
            NullLogger<WizardService>.Instance);

        await Rejects(() => wizard.SaveHomeStatisticsAsync(new HomeStatisticsDto { HoaRulesLink = ScriptLink }));
    }

    [Fact]
    public async Task An_equipment_manufacturer_link_must_be_a_web_url()
    {
        var equipment = new EquipmentService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<EquipmentService>.Instance);

        await Rejects(() => equipment.CreateAsync(new CreateEquipmentRequest { Name = "Furnace", ManufacturerLink = ScriptLink }));
        await Rejects(() => equipment.UpdateAsync(Guid.NewGuid(), new UpdateEquipmentRequest { Name = "Furnace", ManufacturerLink = ScriptLink }));
    }

    [Fact]
    public async Task A_recipe_step_video_link_must_be_a_web_url()
    {
        var recipes = new RecipeService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<RecipeService>.Instance);

        await Rejects(() => recipes.AddStepAsync(Guid.NewGuid(), new CreateRecipeStepRequest { VideoUrl = ScriptLink }));
        await Rejects(() => recipes.UpdateStepAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateRecipeStepRequest { VideoUrl = ScriptLink }));
    }

    private static async Task Rejects(Func<Task> save) =>
        (await save.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle();

    private ContactService Contacts() => new(_context, _tenantProvider.Object, Mock.Of<IFileStorageService>(),
        Mock.Of<IFileUrlService>(), new AddressHasher(new PassThroughAddressCanonicalizer()),
        NullLogger<ContactService>.Instance);

    private Contact SeedContact()
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "Contact",
            CreatedByUserId = _userId,
            IsActive = true,
        };
        _context.Contacts.Add(contact);
        _context.SaveChanges();
        return contact;
    }
}
