namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Service for checking user edit permissions based on roles.
/// Users with Admin or Editor roles can edit; Viewers are read-only.
/// </summary>
public interface IUserPermissions
{
    /// <summary>
    /// Asynchronously checks if the current user has edit permissions.
    /// </summary>
    Task<bool> CanEditAsync();

    /// <summary>
    /// Gets the cached edit permission value after initialization.
    /// Returns false if not yet initialized.
    /// </summary>
    bool CanEdit { get; }
}
