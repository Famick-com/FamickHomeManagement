using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Where this controller is routed is a correctness question, not a naming one.
/// </summary>
/// <remarks>
/// The mobile client withholds its bearer token from everything under <c>api/auth/</c> —
/// that prefix is where sign-in lives, so its handler treats the whole thing as anonymous
/// unless a path is explicitly opted back in. Account deletion first shipped under
/// <c>api/auth/account/deletion</c> and every call came back 401, with nothing in the
/// server logs to explain it because the request arrived genuinely unauthenticated.
/// <para>
/// Moving it under <c>api/v1</c> is the fix. This test exists because the endpoint is
/// account management and <c>api/auth</c> is where it looks like it belongs — the pull
/// toward moving it back is real, and the failure it causes is invisible from the server
/// side.
/// </para>
/// </remarks>
public class AccountDeletionRoutingTests
{
    [Fact]
    public void DeletionEndpointIsNotUnderTheAnonymousAuthPrefix()
    {
        var route = typeof(AccountDeletionApiController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();

        route.Template.Should().NotStartWith("api/auth",
            "the mobile client sends no bearer token to api/auth paths it has not explicitly " +
            "opted in, so an [Authorize] endpoint there answers 401 to every mobile call");
    }

    [Fact]
    public void DeletionEndpointRequiresAuthentication()
    {
        // The other half of the pairing: the route only needs to avoid api/auth because
        // the controller is authenticated. If that ever stops being true, anyone could
        // schedule anyone's household for deletion.
        typeof(AccountDeletionApiController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should().NotBeEmpty();
    }
}
