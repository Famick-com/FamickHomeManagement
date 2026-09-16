namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// <see cref="IStoreConfiguration"/> from the running app.
/// </summary>
public class StoreConfiguration : IStoreConfiguration
{
    private readonly ApiSettings _apiSettings;

    public StoreConfiguration(ApiSettings apiSettings) => _apiSettings = apiSettings;

    public bool IsCloudHousehold => _apiSettings.IsCloudServer();

    /// <remarks>
    /// Chosen at runtime rather than with <c>#if</c>, so one purchase implementation serves
    /// both platforms. The cost is that each binary carries both keys — acceptable, because
    /// these are the store's <em>public</em> SDK keys, which are designed to ship inside app
    /// binaries and are not secrets in the way a licence key is.
    ///
    /// <para>Empty when the build environment supplied none, which is the normal state of a
    /// clone with no secrets. The purchase service treats that as "no store", so the app
    /// still runs and the purchase surface is simply absent.</para>
    /// </remarks>
    public string ApiKey =>
        DeviceInfo.Current.Platform == Microsoft.Maui.Devices.DevicePlatform.iOS
        || DeviceInfo.Current.Platform == Microsoft.Maui.Devices.DevicePlatform.MacCatalyst
            ? LicenseKeys.RevenueCatIos
            : LicenseKeys.RevenueCatAndroid;
}
