using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Issue #108: images from a URL come only through the guarded fetcher, under a size
/// cap, and are stored as the type their bytes show, never the type the server claims.
/// </summary>
public class ProductImageFromUrlTests
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    private readonly HomeManagementDbContext _context = new(
        new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

    private readonly Mock<IOutboundHttpFetcher> _fetcher = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Guid _productId = Guid.NewGuid();

    public ProductImageFromUrlTests()
    {
        _context.Products.Add(new Product { Id = _productId, TenantId = Guid.NewGuid(), Name = "Beans" });
        _context.SaveChanges();

        _storage.Setup(s => s.SaveProductImageAsync(_productId, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Stream _, string name, CancellationToken _) => "stored-" + name);
    }

    private ProductsService Service() => new(
        _context, _storage.Object, Mock.Of<IFileUrlService>(), _fetcher.Object,
        Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(),
        Mock.Of<ITenantProvider>(), NullLogger<ProductsService>.Instance);

    private void Serve(byte[] body, string? contentType) =>
        _fetcher.Setup(f => f.GetAsync(It.IsAny<Uri>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri url, long _, CancellationToken _) => new OutboundFetchResult(body, contentType, url));

    [Fact]
    public async Task Stores_the_sniffed_type_whatever_the_server_and_url_claim()
    {
        Serve(PngBytes, "text/html");

        var image = await Service().AddImageFromUrlAsync(_productId, "https://example.com/evil.html");

        image.Should().NotBeNull();
        var stored = await _context.ProductImages.SingleAsync();
        stored.ContentType.Should().Be("image/png");
        _storage.Verify(s => s.SaveProductImageAsync(
            _productId, It.IsAny<Stream>(), "product-image.png", It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "image/svg+xml")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", "image/png")]
    [InlineData("<html><script>alert(1)</script></html>", "image/jpeg")]
    public async Task Refuses_anything_that_is_not_a_raster_image(string body, string claimed)
    {
        Serve(Encoding.UTF8.GetBytes(body), claimed);

        var image = await Service().AddImageFromUrlAsync(_productId, "https://example.com/pic.png");

        image.Should().BeNull();
        (await _context.ProductImages.CountAsync()).Should().Be(0);
        _storage.Verify(s => s.SaveProductImageAsync(
            It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fetches_under_the_size_cap()
    {
        Serve(PngBytes, "image/png");

        await Service().AddImageFromUrlAsync(_productId, "https://example.com/pic.png");

        _fetcher.Verify(f => f.GetAsync(
            new Uri("https://example.com/pic.png"), ProductsService.MaxImageFromUrlBytes, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task A_refused_fetch_adds_nothing()
    {
        _fetcher.Setup(f => f.GetAsync(It.IsAny<Uri>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutboundFetchException(OutboundFetchFailure.AddressNotAllowed));

        var image = await Service().AddImageFromUrlAsync(_productId, "http://169.254.169.254/latest/meta-data/");

        image.Should().BeNull();
        (await _context.ProductImages.CountAsync()).Should().Be(0);
    }
}

public class ImageSignatureTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 }, "image/jpeg", ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png", ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0 }, "image/gif", ".gif")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 1, 0 }, "image/gif", ".gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50, 0x56 }, "image/webp", ".webp")]
    public void Recognises_the_allowed_formats(byte[] data, string contentType, string extension)
    {
        var format = ImageSignature.Detect(data);

        format.Should().NotBeNull();
        format!.ContentType.Should().Be(contentType);
        format.Extension.Should().Be(extension);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("<?xml version=\"1.0\"?><svg/>")]
    [InlineData("<!DOCTYPE html><html></html>")]
    [InlineData("BM6\0\0\0")]                 // BMP: not on the allow-list
    [InlineData("RIFF\u0001\u0002\u0003\u0004WAVE")] // RIFF but not WebP
    [InlineData("")]
    public void Refuses_everything_else(string data)
    {
        ImageSignature.Detect(Encoding.Latin1.GetBytes(data)).Should().BeNull();
    }
}
