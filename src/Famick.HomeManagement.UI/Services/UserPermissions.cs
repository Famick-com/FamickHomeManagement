using Microsoft.AspNetCore.Components.Authorization;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Implementation of IUserPermissions that checks user roles from the authentication state.
/// </summary>
public class UserPermissions : IUserPermissions
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private bool? _canEdit;

    public UserPermissions(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    /// <inheritdoc />
    public bool CanEdit => _canEdit ?? false;

    /// <inheritdoc />
    public async Task<bool> CanEditAsync()
    {
        if (_canEdit.HasValue)
        {
            return _canEdit.Value;
        }

        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        // Admin and Editor roles can edit; Viewer role is read-only
        _canEdit = user.IsInRole("Admin") || user.IsInRole("Editor");

        return _canEdit.Value;
    }
}
