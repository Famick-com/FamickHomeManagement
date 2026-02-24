using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing notifications, preferences, and device tokens
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;

    public NotificationsController(
        INotificationService notificationService,
        IUnsubscribeTokenService unsubscribeTokenService,
        ITenantProvider tenantProvider,
        ILogger<NotificationsController> logger)
        : base(tenantProvider, logger)
    {
        _notificationService = notificationService;
        _unsubscribeTokenService = unsubscribeTokenService;
    }

    #region Notifications

    /// <summary>
    /// Lists notifications for the current user with optional filtering and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> List(
        [FromQuery] NotificationListRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        var result = await _notificationService.GetNotificationsAsync(
            userId.Value, request, cancellationToken);
        return ApiResponse(result);
    }

    /// <summary>
    /// Gets the count of unread notifications for the current user
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        var count = await _notificationService.GetUnreadCountAsync(userId.Value, cancellationToken);
        return ApiResponse(new { count });
    }

    /// <summary>
    /// Marks a specific notification as read
    /// </summary>
    [HttpPut("{id}/read")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        await _notificationService.MarkAsReadAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks all notifications as read for the current user
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        await _notificationService.MarkAllAsReadAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Dismisses (soft-deletes) a notification
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        await _notificationService.DismissAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Preferences

    /// <summary>
    /// Gets notification preferences for the current user
    /// </summary>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(List<NotificationPreferenceDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        var preferences = await _notificationService.GetPreferencesAsync(
            userId.Value, cancellationToken);
        return ApiResponse(preferences);
    }

    /// <summary>
    /// Updates notification preferences for the current user
    /// </summary>
    [HttpPut("preferences")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        await _notificationService.UpdatePreferencesAsync(
            userId.Value, request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Device Tokens

    /// <summary>
    /// Registers a push notification device token
    /// </summary>
    [HttpPost("device")]
    [ProducesResponseType(typeof(DeviceTokenDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        var result = await _notificationService.RegisterDeviceTokenAsync(
            userId.Value, request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>
    /// Unregisters a push notification device token
    /// </summary>
    [HttpDelete("device/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnregisterDevice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return UnauthorizedResponse();

        await _notificationService.UnregisterDeviceTokenAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Unsubscribe

    /// <summary>
    /// Processes an email unsubscribe request using a signed token (RFC 8058).
    /// This endpoint is anonymous - authentication is via the signed token.
    /// </summary>
    [HttpPost("unsubscribe")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Unsubscribe(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return ErrorResponse("Missing unsubscribe token");
        }

        if (!_unsubscribeTokenService.TryParseToken(token, out var claims))
        {
            return ErrorResponse("Invalid or expired unsubscribe token");
        }

        await _notificationService.DisableEmailForTypeAsync(
            claims!.UserId, claims.TenantId, claims.NotificationType, cancellationToken);

        _logger.LogInformation(
            "User {UserId} unsubscribed from {NotificationType} emails via token",
            claims.UserId, claims.NotificationType);

        return Ok(new { message = "Successfully unsubscribed", notificationType = claims.NotificationType.ToString() });
    }

    /// <summary>
    /// GET endpoint for email unsubscribe - redirects to notification center with unsubscribe query param.
    /// Email clients may use GET for one-click unsubscribe preview.
    /// </summary>
    [HttpGet("unsubscribe")]
    [AllowAnonymous]
    [ProducesResponseType(302)]
    [ProducesResponseType(400)]
    public IActionResult UnsubscribeGet([FromQuery] string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return ErrorResponse("Missing unsubscribe token");
        }

        if (!_unsubscribeTokenService.TryParseToken(token, out var claims))
        {
            return ErrorResponse("Invalid or expired unsubscribe token");
        }

        // Redirect to the notification center with unsubscribe confirmation
        return Redirect($"/notifications?unsubscribe={claims!.NotificationType}");
    }

    #endregion

    /// <summary>
    /// Gets the current user ID from the tenant provider
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        return _tenantProvider.UserId;
    }
}
