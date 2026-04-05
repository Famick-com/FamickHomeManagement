using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when the household profile image is uploaded or removed.
/// </summary>
public sealed class HouseholdProfileImageChangedMessage() : ValueChangedMessage<bool>(true);
