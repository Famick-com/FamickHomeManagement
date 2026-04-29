using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class AddressServiceAutocompleteTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private readonly HomeManagementDbContext _db;
    private readonly Mock<ITenantProvider> _tenant = new();
    private readonly Mock<IAddressAutocompleteProvider> _provider = new();
    private readonly AddressSuggestionCache _cache;
    private readonly TestableAddressService _service;

    public AddressServiceAutocompleteTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"addr-svc-{Guid.NewGuid()}")
            .Options;
        _db = new HomeManagementDbContext(options);

        _tenant.Setup(t => t.TenantId).Returns(TenantId);
        _provider.SetupGet(p => p.ProviderName).Returns("TestProvider");

        _cache = new AddressSuggestionCache(new MemoryCache(Options.Create(new MemoryCacheOptions())));

        _service = new TestableAddressService(
            _db, _tenant.Object, _provider.Object, _cache,
            NullLogger<AddressService>.Instance);
    }

    private static class TestHashHelper
    {
        public static string? Compute(params string?[] parts)
        {
            var combined = string.Join("|",
                parts.Where(p => !string.IsNullOrWhiteSpace(p))
                     .Select(p => p!.Trim().ToLowerInvariant()));
            if (string.IsNullOrEmpty(combined)) return null;
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    /// <summary>
    /// InMemory EF provider cannot evaluate <c>EF.Functions.ILike</c>. This
    /// override replaces the tenant-visibility + LIKE filter with a simple
    /// case-insensitive contains so the autocomplete-merge behaviour above it
    /// can be verified without standing up PostgreSQL.
    /// </summary>
    private sealed class TestableAddressService : AddressService
    {
        private readonly HomeManagementDbContext _db;

        public TestableAddressService(
            HomeManagementDbContext db,
            ITenantProvider tenantProvider,
            IAddressAutocompleteProvider provider,
            IAddressSuggestionCache cache,
            Microsoft.Extensions.Logging.ILogger<AddressService> logger)
            : base(db, tenantProvider, provider, cache, logger)
        {
            _db = db;
        }

        public override Task<List<AddressDto>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return Task.FromResult(new List<AddressDto>());

            var q = query.Trim();
            var results = _db.Addresses.ToList()
                .Where(a =>
                    Contains(a.AddressLine1, q) ||
                    Contains(a.City, q) ||
                    Contains(a.StateProvince, q) ||
                    Contains(a.FormattedAddress, q))
                .Take(Math.Clamp(limit, 1, 25))
                .Select(Famick.HomeManagement.Core.Mapping.TenantMapper.ToAddressDto)
                .ToList();
            return Task.FromResult(results);
        }

        private static bool Contains(string? haystack, string needle) =>
            !string.IsNullOrEmpty(haystack)
            && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _db.Dispose();

    private Address SeedLocalAddress(string line1, string? city = "Springfield", string? state = "IL", string? postal = "62701")
    {
        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = line1,
            City = city,
            StateProvince = state,
            PostalCode = postal,
            Country = "USA",
            FormattedAddress = $"{line1}, {city}, {state} {postal}",
            NormalizedHash = TestHashHelper.Compute(line1, city, state, postal, "USA")
        };
        _db.Addresses.Add(address);

        _db.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = TenantId, FirstName = "Seed" });
        var contactId = _db.Contacts.Local.Last().Id;
        _db.ContactAddresses.Add(new ContactAddress
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ContactId = contactId,
            AddressId = address.Id,
            Tag = AddressTag.Home
        });
        _db.SaveChanges();
        return address;
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_WhenQueryTooShort()
    {
        var result = await _service.AutocompleteAsync("a");

        result.Should().BeEmpty();
        _provider.Verify(p => p.AutocompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AutocompleteAsync_MergesLocalAndProvider_WithLocalFirst()
    {
        SeedLocalAddress("123 Main St");

        _provider.Setup(p => p.AutocompleteAsync("main", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalAddressSuggestion>
            {
                new() { Line1 = "456 Main St", City = "Chicago", State = "IL", PostalCode = "60601", Country = "USA" }
            });

        var result = await _service.AutocompleteAsync("main", 10);

        result.Should().HaveCount(2);
        result[0].Source.Should().Be("Local");
        result[0].AddressId.Should().NotBeNull();
        result[1].Source.Should().Be("TestProvider");
        result[1].AddressId.Should().BeNull();
        result[1].SuggestionId.Should().NotBe(Guid.Empty);

        _cache.TryGet(result[1].SuggestionId).Should().NotBeNull();
    }

    [Fact]
    public async Task AutocompleteAsync_DedupesProviderAgainstLocalByNormalizedHash()
    {
        SeedLocalAddress("123 Main St");

        _provider.Setup(p => p.AutocompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalAddressSuggestion>
            {
                new() { Line1 = "123 Main St", City = "Springfield", State = "IL", PostalCode = "62701", Country = "USA" },
                new() { Line1 = "999 Elm St", City = "Springfield", State = "IL", PostalCode = "62701", Country = "USA" }
            });

        var result = await _service.AutocompleteAsync("main", 10);

        result.Should().HaveCount(2);
        result.Count(r => r.Source == "TestProvider").Should().Be(1);
        result.Single(r => r.Source == "TestProvider").AddressLine1.Should().Be("999 Elm St");
    }

    [Fact]
    public async Task AutocompleteAsync_RunsLocalAndProviderInParallel()
    {
        SeedLocalAddress("123 Main St");

        _provider.Setup(p => p.AutocompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, int _, CancellationToken ct) =>
            {
                await Task.Delay(200, ct);
                return new List<ExternalAddressSuggestion>();
            });

        var start = DateTime.UtcNow;
        await _service.AutocompleteAsync("main", 10);
        var elapsed = DateTime.UtcNow - start;

        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(380));
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsLocalOnly_WhenProviderThrows()
    {
        SeedLocalAddress("123 Main St");
        _provider.Setup(p => p.AutocompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var result = await _service.AutocompleteAsync("main", 10);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be("Local");
    }

    [Fact]
    public async Task ResolveSuggestionAsync_ReturnsLocalDto_WhenSuggestionIsLocal()
    {
        var existing = SeedLocalAddress("777 Oak Ave");

        var result = await _service.ResolveSuggestionAsync(
            new ResolveAddressSuggestionRequest { SuggestionId = existing.Id });

        result.Should().NotBeNull();
        result!.Id.Should().Be(existing.Id);
        result.AddressLine1.Should().Be("777 Oak Ave");
    }

    [Fact]
    public async Task ResolveSuggestionAsync_OverridesLine2_WhenProvided()
    {
        var existing = SeedLocalAddress("777 Oak Ave");

        var result = await _service.ResolveSuggestionAsync(new ResolveAddressSuggestionRequest
        {
            SuggestionId = existing.Id,
            AddressLine2 = "Suite 12"
        });

        result!.AddressLine2.Should().Be("Suite 12");
        (await _db.Addresses.FindAsync(existing.Id))!.AddressLine2.Should().Be("Suite 12");
    }

    [Fact]
    public async Task ResolveSuggestionAsync_ReturnsNull_WhenSuggestionMissing()
    {
        var result = await _service.ResolveSuggestionAsync(
            new ResolveAddressSuggestionRequest { SuggestionId = Guid.NewGuid() });

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveSuggestionAsync_CreatesNewAddress_FromCachedAndStandardizes()
    {
        var suggestion = new ExternalAddressSuggestion
        {
            Line1 = "10 Downing",
            City = "London",
            State = "KY",
            PostalCode = "40741",
            Country = "USA"
        };
        var id = _cache.Store(suggestion);

        _provider.Setup(p => p.StandardizeAsync(It.IsAny<ExternalStandardizeInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalStandardizedAddress
            {
                Line1 = "10 DOWNING ST",
                City = "LONDON",
                State = "KY",
                PostalCode = "40741-1234",
                Country = "USA",
                CountryCode = "US"
            });

        var result = await _service.ResolveSuggestionAsync(new ResolveAddressSuggestionRequest
        {
            SuggestionId = id,
            AddressLine2 = "Apt 2"
        });

        result.Should().NotBeNull();
        result!.AddressLine1.Should().Be("10 DOWNING ST");
        result.AddressLine2.Should().Be("Apt 2");
        result.PostalCode.Should().Be("40741-1234");
        result.Country.Should().Be("USA");
        _db.Addresses.Should().ContainSingle(a => a.Id == result.Id);
    }

    [Fact]
    public async Task ResolveSuggestionAsync_ReusesExisting_WhenHashMatches()
    {
        var existing = SeedLocalAddress("500 Elm St", state: "TX", postal: "75001", city: "Addison");
        var suggestion = new ExternalAddressSuggestion
        {
            Line1 = "500 Elm St",
            City = "Addison",
            State = "TX",
            PostalCode = "75001",
            Country = "USA"
        };
        var id = _cache.Store(suggestion);

        _provider.Setup(p => p.StandardizeAsync(It.IsAny<ExternalStandardizeInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalStandardizedAddress?)null);

        var result = await _service.ResolveSuggestionAsync(new ResolveAddressSuggestionRequest { SuggestionId = id });

        result!.Id.Should().Be(existing.Id);
        _db.Addresses.Count().Should().Be(1);
    }

    [Fact]
    public async Task ResolveSuggestionAsync_ReusesByProviderPlaceId()
    {
        var existing = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = "Rooftop address",
            City = "Somewhere",
            Country = "USA",
            GeoapifyPlaceId = "geoapify-place-xyz",
            NormalizedHash = TestHashHelper.Compute("Rooftop address", "Somewhere", null, null, "USA")
        };
        _db.Addresses.Add(existing);
        _db.SaveChanges();

        var id = _cache.Store(new ExternalAddressSuggestion
        {
            Line1 = "Different Line",
            City = "DifferentCity",
            Country = "USA",
            ProviderPlaceId = "geoapify-place-xyz"
        });

        _provider.Setup(p => p.StandardizeAsync(It.IsAny<ExternalStandardizeInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalStandardizedAddress
            {
                Line1 = "Different Line",
                City = "DifferentCity",
                Country = "USA",
                ProviderPlaceId = "geoapify-place-xyz"
            });

        var result = await _service.ResolveSuggestionAsync(new ResolveAddressSuggestionRequest { SuggestionId = id });

        result!.Id.Should().Be(existing.Id);
        _db.Addresses.Count().Should().Be(1);
    }

    [Fact]
    public async Task StandardizeAndCreateAsync_UsesProviderOutput()
    {
        _provider.Setup(p => p.StandardizeAsync(It.IsAny<ExternalStandardizeInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalStandardizedAddress
            {
                Line1 = "1 INFINITE LOOP",
                City = "CUPERTINO",
                State = "CA",
                PostalCode = "95014",
                Country = "USA",
                CountryCode = "US"
            });

        var result = await _service.StandardizeAndCreateAsync(new StandardizeAddressRequest
        {
            AddressLine1 = "1 infinite loop",
            City = "cupertino",
            StateProvince = "ca",
            PostalCode = "95014",
            Country = "US"
        });

        result.AddressLine1.Should().Be("1 INFINITE LOOP");
        result.City.Should().Be("CUPERTINO");
        result.PostalCode.Should().Be("95014");
        _db.Addresses.Should().ContainSingle(a => a.Id == result.Id);
    }

    [Fact]
    public async Task StandardizeAndCreateAsync_FallsBack_WhenProviderReturnsNull()
    {
        _provider.Setup(p => p.StandardizeAsync(It.IsAny<ExternalStandardizeInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalStandardizedAddress?)null);

        var result = await _service.StandardizeAndCreateAsync(new StandardizeAddressRequest
        {
            AddressLine1 = "999 Nowhere Rd",
            City = "Ghost Town",
            StateProvince = "NV",
            PostalCode = "00000",
            Country = "USA"
        });

        result.AddressLine1.Should().Be("999 Nowhere Rd");
        result.FormattedAddress.Should().Contain("999 Nowhere Rd");
    }

    [Fact]
    public async Task StandardizeAndCreateAsync_DedupesAgainstExistingByHash()
    {
        var existing = SeedLocalAddress("42 Galaxy Way", city: "Portland", state: "OR", postal: "97201");
        _provider.Setup(p => p.StandardizeAsync(It.IsAny<ExternalStandardizeInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalStandardizedAddress?)null);

        var result = await _service.StandardizeAndCreateAsync(new StandardizeAddressRequest
        {
            AddressLine1 = "42 Galaxy Way",
            City = "Portland",
            StateProvince = "OR",
            PostalCode = "97201",
            Country = "USA"
        });

        result.Id.Should().Be(existing.Id);
    }
}
