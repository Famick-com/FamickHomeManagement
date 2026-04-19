using Famick.HomeManagement.Shared.PhoneFormatting;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.PhoneFormatting;

public class PhoneNumberFormatterTests
{
    [Fact]
    public void Parse_NullOrEmpty_ReturnsDefaultWithEmptyLocal()
    {
        var result = PhoneNumberFormatter.Parse(null);
        result.Country.Should().Be(CountryPhoneFormats.UnitedStates);
        result.LocalNumber.Should().BeEmpty();

        result = PhoneNumberFormatter.Parse("  ");
        result.Country.Should().Be(CountryPhoneFormats.UnitedStates);
        result.LocalNumber.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoPrefix_DefaultsToUs()
    {
        var result = PhoneNumberFormatter.Parse("(555) 555-1212");
        result.Country.Should().Be(CountryPhoneFormats.UnitedStates);
        result.LocalNumber.Should().Be("(555) 555-1212");
    }

    [Fact]
    public void Parse_UsPrefix_SplitsCodeAndLocal()
    {
        var result = PhoneNumberFormatter.Parse("+1 (555) 555-1212");
        result.Country.Should().Be(CountryPhoneFormats.UnitedStates);
        result.LocalNumber.Should().Be("(555) 555-1212");
    }

    [Fact]
    public void Parse_UkPrefix_PicksUnitedKingdom()
    {
        var result = PhoneNumberFormatter.Parse("+44 20 7946 0958");
        result.Country.Should().Be(CountryPhoneFormats.UnitedKingdom);
        result.LocalNumber.Should().Be("20 7946 0958");
    }

    [Fact]
    public void Parse_PrefixWithoutSpace_StillSplits()
    {
        var result = PhoneNumberFormatter.Parse("+447911123456");
        result.Country.Should().Be(CountryPhoneFormats.UnitedKingdom);
        result.LocalNumber.Should().Be("7911123456");
    }

    [Fact]
    public void Parse_UnknownPrefix_FallsBackToUs()
    {
        var result = PhoneNumberFormatter.Parse("+999 12345");
        result.Country.Should().Be(CountryPhoneFormats.UnitedStates);
        result.LocalNumber.Should().Be("12345");
    }

    [Fact]
    public void FormatForStorage_BuildsDialCodePlusLocal()
    {
        var stored = PhoneNumberFormatter.FormatForStorage(
            CountryPhoneFormats.UnitedStates, "(555) 555-1212");
        stored.Should().Be("+1 (555) 555-1212");
    }

    [Fact]
    public void FormatForStorage_EmptyLocal_ReturnsEmpty()
    {
        PhoneNumberFormatter.FormatForStorage(CountryPhoneFormats.UnitedStates, "")
            .Should().BeEmpty();
        PhoneNumberFormatter.FormatForStorage(CountryPhoneFormats.UnitedStates, null)
            .Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_UsNumber_IsStable()
    {
        const string input = "+1 (555) 555-1212";
        var parsed = PhoneNumberFormatter.Parse(input);
        var output = PhoneNumberFormatter.FormatForStorage(parsed.Country, parsed.LocalNumber);
        output.Should().Be(input);
    }

    [Fact]
    public void RoundTrip_UkNumber_IsStable()
    {
        const string input = "+44 20 7946 0958";
        var parsed = PhoneNumberFormatter.Parse(input);
        var output = PhoneNumberFormatter.FormatForStorage(parsed.Country, parsed.LocalNumber);
        output.Should().Be(input);
    }

    [Fact]
    public void FormatForDisplay_BareUsNumber_PrependsUsPrefix()
    {
        PhoneNumberFormatter.FormatForDisplay("(555) 555-1212")
            .Should().Be("+1 (555) 555-1212");
    }

    [Fact]
    public void FormatForDisplay_RawDigitsOnly_AppliesMask()
    {
        PhoneNumberFormatter.FormatForDisplay("5135551212")
            .Should().Be("+1 (513) 555-1212");
    }

    [Fact]
    public void FormatForDisplay_PrefixedRawDigits_AppliesMask()
    {
        PhoneNumberFormatter.FormatForDisplay("+1 5135551212")
            .Should().Be("+1 (513) 555-1212");
    }

    [Fact]
    public void ApplyMask_VariableLengthCountry_ReturnsLocalUnchanged()
    {
        var uk = CountryPhoneFormats.UnitedKingdom;
        PhoneNumberFormatter.ApplyMask(uk, "20 7946 0958")
            .Should().Be("20 7946 0958");
    }

    [Fact]
    public void FormatForDisplay_EmptyInput_ReturnsEmpty()
    {
        PhoneNumberFormatter.FormatForDisplay(null).Should().BeEmpty();
        PhoneNumberFormatter.FormatForDisplay("").Should().BeEmpty();
    }

    [Fact]
    public void CountDigits_IgnoresNonDigits()
    {
        PhoneNumberFormatter.CountDigits("+1 (555) 555-1212").Should().Be(11);
    }

    [Fact]
    public void StripFormatting_KeepsLeadingPlus()
    {
        PhoneNumberFormatter.StripFormatting("+1 (555) 555-1212").Should().Be("+15555551212");
        PhoneNumberFormatter.StripFormatting("(555) 555-1212").Should().Be("5555551212");
    }

    [Fact]
    public void ByDialingCode_PlusOne_AlwaysReturnsUs()
    {
        CountryPhoneFormats.ByDialingCode("+1").Should().Be(CountryPhoneFormats.UnitedStates);
    }
}
