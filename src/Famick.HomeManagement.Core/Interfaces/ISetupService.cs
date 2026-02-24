using Famick.HomeManagement.Core.DTOs.Setup;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for checking application setup status
/// </summary>
public interface ISetupService
{
    /// <summary>
    /// Checks if initial setup is required (no users exist)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Setup status response indicating if setup is needed</returns>
    Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any users exist in the database
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if users exist, false otherwise</returns>
    Task<bool> HasUsersAsync(CancellationToken cancellationToken = default);
}
