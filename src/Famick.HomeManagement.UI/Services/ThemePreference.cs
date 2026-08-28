using Microsoft.JSInterop;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// The light/dark choice, shared by every screen that renders a theme.
/// <para>
/// One object rather than a field per page, because the sign-in screen and the app behind it
/// have to agree. They previously did not: the app had a toggle and the pre-auth pages were
/// pinned light, so signing in on a dark device meant crossing from a white screen into a dark
/// one.
/// </para>
/// </summary>
public interface IThemePreference
{
    /// <summary>Resolved preference. False until <see cref="InitializeAsync"/> has run.</summary>
    bool IsDarkMode { get; }

    /// <summary>Raised when the preference changes, so open components can re-render.</summary>
    event Action? Changed;

    /// <summary>
    /// Reads the stored choice, falling back to the operating system's. Safe to call repeatedly;
    /// only the first call does any work.
    /// </summary>
    Task InitializeAsync();

    /// <summary>Records an explicit choice, which outranks the operating system from then on.</summary>
    Task SetAsync(bool isDarkMode);

    /// <summary>Flips the current value and records it.</summary>
    Task ToggleAsync();
}

/// <inheritdoc />
public class ThemePreference : IThemePreference
{
    private readonly IJSRuntime _js;
    private bool _initialized;

    public ThemePreference(IJSRuntime js)
    {
        _js = js;
    }

    /// <inheritdoc />
    public bool IsDarkMode { get; private set; }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            // Resolves stored-choice-else-OS in one call, so the "has the user ever chosen"
            // question is answered in the one place that can see localStorage.
            IsDarkMode = await _js.InvokeAsync<bool>("famickTheme.resolve");
        }
        catch
        {
            // Prerendering, or JS unavailable. Light is the safer default: a light screen in a
            // dark room is unpleasant, a dark screen the user never asked for looks broken.
            IsDarkMode = false;
        }

        await ApplyBackgroundAsync();
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public async Task SetAsync(bool isDarkMode)
    {
        _initialized = true;

        if (IsDarkMode == isDarkMode) return;

        IsDarkMode = isDarkMode;

        try
        {
            await _js.InvokeVoidAsync("famickTheme.store", isDarkMode);
        }
        catch
        {
            // Applies for this session even if it cannot be remembered.
        }

        await ApplyBackgroundAsync();
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public Task ToggleAsync() => SetAsync(!IsDarkMode);

    /// <summary>
    /// Repaints the page background to match.
    /// <para>
    /// The background is an inline style so it can beat the stylesheets during boot, which also
    /// means MudBlazor cannot override it afterwards. Without repainting here, toggling leaves
    /// the previous background showing wherever the app's own surfaces do not reach — below a
    /// short page, most obviously — until the next reload.
    /// </para>
    /// </summary>
    private async Task ApplyBackgroundAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("famickTheme.apply", IsDarkMode);
        }
        catch
        {
            // No JS: nothing painted it in the first place.
        }
    }
}
