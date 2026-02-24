using Famick.HomeManagement.Core.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for retrieving configuration settings
/// </summary>
[ApiController]
[Route("api/v1/configuration")]
public class ConfigurationController : ControllerBase
{
    private readonly AppStoreLinksSettings _appStoreLinks;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IOptions<AppStoreLinksSettings> appStoreLinks,
        ILogger<ConfigurationController> logger)
    {
        _appStoreLinks = appStoreLinks.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets app store links for mobile app download
    /// </summary>
    /// <returns>App store URLs and deep link scheme</returns>
    [HttpGet("app-links")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AppLinksResponse), 200)]
    public IActionResult GetAppLinks()
    {
        _logger.LogDebug("Getting app store links");

        return Ok(new AppLinksResponse
        {
            AppleAppStore = _appStoreLinks.AppleAppStore,
            GooglePlayStore = _appStoreLinks.GooglePlayStore,
            DeepLinkScheme = _appStoreLinks.DeepLinkScheme
        });
    }
}

/// <summary>
/// Response model for app store links
/// </summary>
public class AppLinksResponse
{
    /// <summary>
    /// URL to the app on the Apple App Store
    /// </summary>
    public string AppleAppStore { get; set; } = string.Empty;

    /// <summary>
    /// URL to the app on the Google Play Store
    /// </summary>
    public string GooglePlayStore { get; set; } = string.Empty;

    /// <summary>
    /// Custom URL scheme for deep linking
    /// </summary>
    public string DeepLinkScheme { get; set; } = string.Empty;
}
