using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Opens the device's native contact picker and returns the selected contact.
/// </summary>
public interface IDeviceContactPicker
{
    Task<SharedContactData?> PickContactAsync();
}
