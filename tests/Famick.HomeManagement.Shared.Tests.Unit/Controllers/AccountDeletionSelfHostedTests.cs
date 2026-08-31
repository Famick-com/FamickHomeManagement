using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.Account;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Account deletion is cloud-only, for the same reason registration is.
/// </summary>
/// <remarks>
/// A self-hosted server holds one household, so its tenant is the whole installation.
/// Deleting it would empty the server and leave a freshly seeded, empty one behind on the
/// next start — an outcome nobody would choose from a settings screen, and one that cannot
/// be undone from inside the app that caused it.
/// </remarks>
public class AccountDeletionSelfHostedTests
{
    private readonly Mock<IAccountDeletionService> _service = new();

    [Fact]
    public async Task SelfHostedRefusesToScheduleADeletion()
    {
        var controller = Build(multiTenant: false);

        var result = await controller.Request(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        _service.Verify(
            s => s.RequestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the request must be refused before anything is scheduled");
    }

    [Fact]
    public async Task SelfHostedReportsThatDeletionIsUnavailable()
    {
        var controller = Build(multiTenant: false);

        var result = await controller.GetStatus(CancellationToken.None);

        // Answered rather than refused, so the client can hide the entry point instead of
        // offering a control that only fails when tapped.
        var status = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<AccountDeletionStatusDto>().Subject;

        status.IsSupported.Should().BeFalse();
    }

    [Fact]
    public async Task CloudAllowsIt()
    {
        var controller = Build(multiTenant: true);

        _service
            .Setup(s => s.RequestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDeletionRequestResultDto());

        var result = await controller.Request(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// An unbound option must not read as permission.
    /// </summary>
    /// <remarks>
    /// The fallback used when nothing is registered says multi-tenant, which would mean
    /// "deletion allowed" — the wrong way for a gate to fail. A host that forgot to bind it
    /// would then let someone destroy the only copy of their household.
    /// </remarks>
    [Fact]
    public async Task AnUnconfiguredHostRefusesRatherThanAssumingCloud()
    {
        var controller = Build(multiTenant: null);

        var result = await controller.Request(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private AccountDeletionApiController Build(bool? multiTenant)
    {
        var options = multiTenant is null
            ? null
            : new MultiTenancyOptions { IsMultiTenantEnabled = multiTenant.Value };

        var controller = new AccountDeletionApiController(
            _service.Object,
            Mock.Of<ILogger<AccountDeletionApiController>>(),
            options);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }
}
