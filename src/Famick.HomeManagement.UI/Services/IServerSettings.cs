namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Interface for managing server URL settings.
/// Implemented by mobile apps to allow changing the API base URL.
/// </summary>
public interface IServerSettings
{
    /// <summary>
    /// Gets or sets the API base URL.
    /// </summary>
    string BaseUrl { get; set; }
}
