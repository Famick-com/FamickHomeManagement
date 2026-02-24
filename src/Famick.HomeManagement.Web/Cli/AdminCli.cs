using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Famick.HomeManagement.Web.Cli;

/// <summary>
/// CLI handler for administrative commands.
/// Usage: admin-cli [command] [args]
/// </summary>
public static class AdminCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLower();

        return command switch
        {
            "reset-password" => await ResetPasswordAsync(args[1..]),
            "help" or "--help" or "-h" => PrintHelp(),
            _ => UnknownCommand(command)
        };
    }

    private static async Task<int> ResetPasswordAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: Missing arguments");
            Console.WriteLine("Usage: admin-cli reset-password <email> <new-password>");
            return 1;
        }

        var email = args[0].ToLower().Trim();
        var newPassword = args[1];

        if (newPassword.Length < 8)
        {
            Console.WriteLine("Error: Password must be at least 8 characters long");
            return 1;
        }

        try
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Error: Database connection string not found");
                return 1;
            }

            // Create DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<Infrastructure.Data.HomeManagementDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            using var context = new Infrastructure.Data.HomeManagementDbContext(optionsBuilder.Options);

            // Find user by email (bypass tenant filter by using direct SQL)
            var user = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                Console.WriteLine($"Error: User with email '{email}' not found");
                return 1;
            }

            // Hash the new password using BCrypt
            var passwordHasher = new PasswordHasher(configuration);
            user.PasswordHash = passwordHasher.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            // Revoke all refresh tokens for this user
            var activeTokens = await context.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            Console.WriteLine($"Password reset successfully for user: {email}");
            if (activeTokens.Any())
            {
                Console.WriteLine($"Revoked {activeTokens.Count} active session(s)");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Admin CLI - Famick Home Management");
        Console.WriteLine();
        Console.WriteLine("Usage: admin-cli <command> [args]");
        Console.WriteLine();
        Console.WriteLine("Run 'admin-cli help' for available commands");
        return 1;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("Admin CLI - Famick Home Management");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine();
        Console.WriteLine("  reset-password <email> <new-password>");
        Console.WriteLine("      Reset a user's password and revoke all their sessions.");
        Console.WriteLine();
        Console.WriteLine("  help");
        Console.WriteLine("      Show this help message.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  admin-cli reset-password user@example.com NewPassword123");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.WriteLine($"Error: Unknown command '{command}'");
        Console.WriteLine("Run 'admin-cli help' for available commands");
        return 1;
    }
}
