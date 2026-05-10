using Famick.HomeManagement.UI.Components.Authentication;
using Famick.HomeManagement.UI.Services;
using MudBlazor;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Phase 2.5 — Blazor WASM implementation of <see cref="IStepUpReauthCoordinator"/>.
///
/// Shows <c>ReauthDialog</c> via MudBlazor's <see cref="IDialogService"/>. The
/// dialog handles its own password / passkey calls and writes the new tokens
/// to <see cref="ITokenStorage"/> before closing. We just translate the
/// dialog's <see cref="DialogResult"/> into a yes/no return so
/// <see cref="HttpApiClient"/> knows whether to retry the original request.
/// </summary>
public sealed class StepUpReauthCoordinator : IStepUpReauthCoordinator
{
    private readonly IDialogService _dialogService;

    public StepUpReauthCoordinator(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task<bool> RequestStepUpAsync(CancellationToken cancellationToken = default)
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            BackdropClick = false,
        };

        var dialog = await _dialogService.ShowAsync<ReauthDialog>(
            title: null,
            parameters: new DialogParameters(),
            options: options);

        var result = await dialog.Result;
        if (result is null || result.Canceled)
        {
            return false;
        }

        return result.Data is true;
    }
}
