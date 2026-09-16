namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// The two things <see cref="PurchaseService"/> needs to know about its surroundings.
/// </summary>
/// <remarks>
/// Both answers come from MAUI — the server mode from stored preferences, the key from the
/// running platform and a value baked in at build time. Taking them as a dependency rather
/// than reading them directly is what lets the purchase service be tested at all: it is the
/// only code in the app that moves money, and the parts most worth testing are the ones
/// about which household a purchase belongs to.
/// </remarks>
public interface IStoreConfiguration
{
    /// <summary>
    /// Whether this household buys anything from us.
    /// </summary>
    /// <remarks>
    /// False for self-hosted and proxied households, which run their own server and owe
    /// nothing. There is no store for them and nothing may be offered.
    /// </remarks>
    bool IsCloudHousehold { get; }

    /// <summary>
    /// The public store SDK key for the platform this build is running on, or empty when
    /// the build environment supplied none.
    /// </summary>
    string ApiKey { get; }
}
