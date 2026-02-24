using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for GPS location detection and store matching.
/// </summary>
public class LocationService
{
    /// <summary>
    /// Checks if location services are enabled on the device.
    /// </summary>
    public bool IsLocationEnabled
    {
        get
        {
            try
            {
                var status = Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().Result;
                return status == PermissionStatus.Granted;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Gets the current GPS location.
    /// </summary>
    public async Task<Location?> GetCurrentLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return null;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            return await Geolocation.Default.GetLocationAsync(request);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the nearest store within the specified distance.
    /// </summary>
    public StoreSummary? FindNearestStore(
        IEnumerable<StoreSummary> stores,
        Location currentLocation,
        double maxDistanceMeters = 500)
    {
        StoreSummary? nearest = null;
        double minDistance = double.MaxValue;

        foreach (var store in stores)
        {
            if (!store.Latitude.HasValue || !store.Longitude.HasValue)
                continue;

            var distance = CalculateHaversineDistance(
                currentLocation.Latitude,
                currentLocation.Longitude,
                store.Latitude.Value,
                store.Longitude.Value);

            if (distance < minDistance && distance <= maxDistanceMeters)
            {
                minDistance = distance;
                nearest = store;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Calculates the distance between two coordinates using the Haversine formula.
    /// </summary>
    private static double CalculateHaversineDistance(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
