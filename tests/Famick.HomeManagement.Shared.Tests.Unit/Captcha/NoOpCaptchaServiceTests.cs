using Famick.HomeManagement.Shared.Captcha;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Captcha;

public class NoOpCaptchaServiceTests
{
    [Theory]
    [InlineData("real-token")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAsync_always_succeeds(string? token)
    {
        var sut = new NoOpCaptchaService();

        var result = await sut.ValidateAsync(token, action: "test");

        result.Success.Should().BeTrue();
        result.Score.Should().Be(1.0);
        result.FailureReason.Should().BeNull();
    }
}
