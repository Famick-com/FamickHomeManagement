using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using Microsoft.AspNetCore.Components.Authorization;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Implementation of IUserPermissions that checks user roles from the authentication state.
/// Subscribes to auth state changes via IMessageBus to invalidate the cached permission when the user logs in/out.
/// </summary>
public class UserPermissions : IUserPermissions, IDisposable
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IDisposable _subscription;
    private bool? _canEdit;

    public UserPermissions(AuthenticationStateProvider authStateProvider, IMessageBus messageBus)
    {
        _authStateProvider = authStateProvider;
        _subscription = messageBus.Subscribe<AuthenticationStateChangedMessage>(_ => _canEdit = null);
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

    public void Dispose() => _subscription.Dispose();
}
