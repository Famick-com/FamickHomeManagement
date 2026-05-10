using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Famick.HomeManagement.Mobile.Messages;

/// <summary>
/// Phase 2.5 — sent when the server returns a 403 STEP_UP_REQUIRED on a
/// sensitive endpoint. The TCS is the channel back from the reauth modal:
///
/// - Modal completes with a non-null access token   => handler swaps the
///   token and retries the original request.
/// - Modal completes with null (user cancelled)     => handler returns the
///   original 403 to the caller, which surfaces normally.
///
/// The handler awaits the TCS with a timeout so a hung modal can't block
/// the request indefinitely.
/// </summary>
public sealed class StepUpRequiredMessage(TaskCompletionSource<string?> tcs)
    : ValueChangedMessage<TaskCompletionSource<string?>>(tcs);
