using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class PassThroughAddressCanonicalizerTests
{
    private readonly PassThroughAddressCanonicalizer _canonicalizer = new();

    [Fact]
    public async Task CanonicalizeAsync_TrimsButOtherwiseReturnsUnchanged()
    {
        var input = new AddressComponentsInput("  123 Main St  ", "Springfield ", " IL", "62701", "USA");

        var result = await _canonicalizer.CanonicalizeAsync(input);

        result.Line1.Should().Be("123 Main St");
        result.City.Should().Be("Springfield");
        result.State.Should().Be("IL");
        result.PostalCode.Should().Be("62701");
        result.Country.Should().Be("USA");
    }

    [Fact]
    public async Task CanonicalizeAsync_NullInputs_ReturnNull()
    {
        var input = new AddressComponentsInput(null, null, null, null, null);

        var result = await _canonicalizer.CanonicalizeAsync(input);

        result.Line1.Should().BeNull();
        result.City.Should().BeNull();
        result.State.Should().BeNull();
        result.PostalCode.Should().BeNull();
        result.Country.Should().BeNull();
    }

    [Fact]
    public void ProviderName_IsPassThrough()
    {
        _canonicalizer.ProviderName.Should().Be("PassThrough");
    }
}
