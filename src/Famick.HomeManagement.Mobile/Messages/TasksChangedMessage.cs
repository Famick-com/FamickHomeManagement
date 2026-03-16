using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Sent when tasks are completed or skipped in the wizard, carrying the remaining pending count.
/// </summary>
public sealed class TasksChangedMessage(int pendingCount) : ValueChangedMessage<int>(pendingCount);
