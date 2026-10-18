using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class PasswordResetServiceTests
{
    [Fact]
    public async Task ResetPassword_ClearsMustChangePassword()
    {
        // A reset is the user choosing their own password, so a pending forced change is
        // satisfied. Leaving the flag set prompted for a second change right after the reset.
        await using var db = NewDb();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "reset@example.com",
            Username = "reset@example.com",
            PasswordHash = "old-hash",
            MustChangePassword = true,
        };
        db.Users.Add(user);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = Hash("reset-token"),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("new-hash");
        var service = new PasswordResetService(
            db,
            Mock.Of<IEmailService>(),
            Mock.Of<IMessageService>(),
            hasher.Object,
            Options.Create(new EmailSettings()),
            NullLogger<PasswordResetService>.Instance);

        var response = await service.ResetPasswordAsync(
            new ResetPasswordRequest { Token = "reset-token", NewPassword = "N3w-Passw0rd!", ConfirmPassword = "N3w-Passw0rd!" },
            "127.0.0.1");

        response.Success.Should().BeTrue();
        var saved = await db.Users.SingleAsync(u => u.Id == user.Id);
        saved.PasswordHash.Should().Be("new-hash");
        saved.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPassword_RefusesAShortPassword_AndKeepsTheToken()
    {
        await using var db = NewDb();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "reset@example.com",
            Username = "reset@example.com",
            PasswordHash = "old-hash",
        };
        db.Users.Add(user);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = Hash("reset-token"),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();

        var service = new PasswordResetService(
            db,
            Mock.Of<IEmailService>(),
            Mock.Of<IMessageService>(),
            Mock.Of<IPasswordHasher>(),
            Options.Create(new EmailSettings()),
            NullLogger<PasswordResetService>.Instance);

        var response = await service.ResetPasswordAsync(
            new ResetPasswordRequest { Token = "reset-token", NewPassword = "short", ConfirmPassword = "short" },
            "127.0.0.1");

        response.Success.Should().BeFalse();
        (await db.Users.AsNoTracking().SingleAsync()).PasswordHash.Should().Be("old-hash");
        (await db.PasswordResetTokens.AsNoTracking().SingleAsync()).IsValid.Should().BeTrue("the user can try again with a longer one");
    }

    private static HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"password-reset-{Guid.NewGuid()}")
            .Options);

    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
