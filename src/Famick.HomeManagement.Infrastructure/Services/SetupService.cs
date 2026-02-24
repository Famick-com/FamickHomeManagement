using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for checking application setup status
/// </summary>
public class SetupService : ISetupService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMultiTenancyOptions _multiTenancyOptions;
    private readonly ILogger<SetupService> _logger;

    public SetupService(
        HomeManagementDbContext context,
        ILogger<SetupService> logger,
        IMultiTenancyOptions? multiTenancyOptions = null)
    {
        _context = context;
        _multiTenancyOptions = multiTenancyOptions ?? new MultiTenancyOptions { IsMultiTenantEnabled = true };
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken = default)
    {
        var hasUsers = await HasUsersAsync(cancellationToken);

        if (!hasUsers)
        {
            _logger.LogInformation("Setup required: No users found in database");
            return new SetupStatusResponse
            {
                SetupRequired = true,
                Reason = "no_users",
                RequireLegalConsent = _multiTenancyOptions.IsMultiTenantEnabled
            };
        }

        return new SetupStatusResponse
        {
            SetupRequired = false,
            Reason = null,
            RequireLegalConsent = _multiTenancyOptions.IsMultiTenantEnabled
        };
    }

    /// <inheritdoc />
    public async Task<bool> HasUsersAsync(CancellationToken cancellationToken = default)
    {
        // Use IgnoreQueryFilters to bypass tenant filtering
        // This ensures we check for ALL users in the database
        return await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(cancellationToken);
    }
}
