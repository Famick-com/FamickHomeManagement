using System.Security.Claims;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Web.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Middleware;

/// <summary>
/// The middleware against the real store: what a token is answered for each state a user can be in.
/// </summary>
public class JwtMinIatMiddlewareTests : IDisposable
{
    private const long Iat = 1_000_000;

    private readonly HomeManagementDbContext _db = new(
        new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"jwt-min-iat-{Guid.NewGuid()}")
            .Options);

    public void Dispose() => _db.Dispose();

    private async Task<Guid> AddUserAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "ann@example.com",
            Username = "ann@example.com",
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(bool Passed, int Status)> SendAsync(ClaimsPrincipal principal)
    {
        var passed = false;
        var middleware = new JwtMinIatMiddleware(_ =>
        {
            passed = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext { User = principal };

        await middleware.InvokeAsync(
            context,
            new JwtMinIatService(_db, NullLogger<JwtMinIatService>.Instance),
            NullLogger<JwtMinIatMiddleware>.Instance);

        return (passed, context.Response.StatusCode);
    }

    private static ClaimsPrincipal Token(Guid userId, params Claim[] extra) =>
        new(new ClaimsIdentity(
            new[] { new Claim("sub", userId.ToString()), new Claim("iat", Iat.ToString()) }.Concat(extra),
            "jwt"));

    [Fact]
    public async Task A_token_for_a_user_who_was_never_revoked_passes()
    {
        var (passed, _) = await SendAsync(Token(await AddUserAsync()));

        passed.Should().BeTrue();
    }

    [Fact]
    public async Task A_token_for_a_user_who_does_not_exist_is_refused()
    {
        var (passed, status) = await SendAsync(Token(Guid.NewGuid()));

        passed.Should().BeFalse();
        status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task A_token_for_a_user_deleted_after_it_was_issued_is_refused()
    {
        var userId = await AddUserAsync();
        _db.Users.Remove(await _db.Users.SingleAsync(u => u.Id == userId));
        await _db.SaveChangesAsync();

        var (passed, status) = await SendAsync(Token(userId));

        passed.Should().BeFalse();
        status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task A_token_older_than_the_minimum_is_refused_and_a_newer_one_passes()
    {
        var userId = await AddUserAsync();
        await new JwtMinIatService(_db, NullLogger<JwtMinIatService>.Instance).BumpAsync(userId, Iat + 1);

        (await SendAsync(Token(userId))).Passed.Should().BeFalse();

        await new JwtMinIatService(_db, NullLogger<JwtMinIatService>.Instance).BumpAsync(userId, Iat);
        var newer = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId.ToString()), new Claim("iat", (Iat + 1).ToString())], "jwt"));
        (await SendAsync(newer)).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task A_session_with_no_iat_is_not_checked()
    {
        // A Home Assistant Ingress session is built per request and carries no iat.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString())], "HaIngress"));

        var (passed, _) = await SendAsync(principal);

        passed.Should().BeTrue();
    }
}
