using CommunityToolkit.Mvvm.Messaging.Messages;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the BLE scanner connection state changes.
/// </summary>
public sealed class BleScannerStateMessage(BleScannerConnectionState state)
    : ValueChangedMessage<BleScannerConnectionState>(state);
