using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the BLE scanner reads a barcode.
/// Value is the raw barcode string.
/// </summary>
public sealed class BleScannerBarcodeMessage(string barcode) : ValueChangedMessage<string>(barcode);
