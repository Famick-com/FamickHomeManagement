using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Controls;

/// <summary>
/// Client abstraction consumed by <see cref="AddressAutocompleteController"/>.
/// Implemented in production by an adapter around <c>ShoppingApiClient</c> and
/// faked in tests.
/// </summary>
public interface IAddressAutocompleteClient
{
    Task<List<AddressSuggestionDto>> GetAutocompleteAsync(string query, int limit, CancellationToken ct);
    Task<ResolveAddressSuggestionResult> ResolveAsync(ResolveAddressSuggestionRequest request, CancellationToken ct);
    Task<AddressDto?> StandardizeAsync(StandardizeAddressRequest request, CancellationToken ct);
    Task<ExpandSecondariesResult> GetSecondariesAsync(Guid suggestionId, CancellationToken ct);
}

/// <summary>
/// Lets tests collapse <see cref="AddressAutocompleteController"/>'s debounce
/// to zero without actually waiting.
/// </summary>
public interface IDelayProvider
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct);
}

public sealed class DefaultDelayProvider : IDelayProvider
{
    public Task DelayAsync(TimeSpan duration, CancellationToken ct) => Task.Delay(duration, ct);
}

/// <summary>
/// Mutable in-memory mirror of the address fields the UI binds to. Lives on
/// the controller so the view model stays dumb.
/// </summary>
public sealed class AddressFormState
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

/// <summary>
/// Orchestrates the autocomplete-plus-manual-entry flow for the MAUI
/// <c>AddressAutocompleteField</c> control. Pure C# — no MAUI types — so it
/// can be unit-tested without a device or runtime.
/// </summary>
public sealed class AddressAutocompleteController
{
    private readonly IAddressAutocompleteClient _client;
    private readonly IDelayProvider _delayProvider;
    private readonly TimeSpan _debounce;
    private readonly int _minQueryLength;
    private readonly int _autoUnlockThreshold;
    private readonly int _suggestionLimit;

    private CancellationTokenSource? _activeSearchCts;
    private IReadOnlyList<AddressSuggestionDto> _suggestions = Array.Empty<AddressSuggestionDto>();
    private IReadOnlyList<AddressSuggestionDto> _secondaryOptions = Array.Empty<AddressSuggestionDto>();
    private bool _isExpandingSecondaries;

    public AddressAutocompleteController(
        IAddressAutocompleteClient client,
        IDelayProvider? delayProvider = null,
        TimeSpan? debounce = null,
        int minQueryLength = 2,
        int autoUnlockThreshold = 3,
        int suggestionLimit = 10)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _delayProvider = delayProvider ?? new DefaultDelayProvider();
        _debounce = debounce ?? TimeSpan.FromMilliseconds(300);
        _minQueryLength = minQueryLength;
        _autoUnlockThreshold = autoUnlockThreshold;
        _suggestionLimit = suggestionLimit;
    }

    public AddressFormState Fields { get; } = new();

    public Guid? SelectedAddressId { get; private set; }
    public bool IsManualEntryEnabled { get; private set; }

    private bool _offerManualEntryPrompt;
    /// <summary>
    /// True when the provider returned zero matches for the current Line 1
    /// query and the text is long enough to warrant offering manual entry.
    /// The UI surfaces this as a single "No match, manually add" row in the
    /// suggestion dropdown; tapping it calls <see cref="EnableManualEntry"/>.
    /// </summary>
    public bool OfferManualEntryPrompt
    {
        get => _offerManualEntryPrompt;
        private set
        {
            if (_offerManualEntryPrompt == value) return;
            _offerManualEntryPrompt = value;
            ManualEntryPromptChanged?.Invoke();
        }
    }

    private bool _isSearching;
    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (_isSearching == value) return;
            _isSearching = value;
            SearchStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Origin of the most recently applied address. <c>"Local"</c> for a row
    /// already in the Addresses table (preload or local autocomplete hit),
    /// <c>"Manual"</c> when the user committed a typed address through
    /// <see cref="CommitAsync"/>, or the provider name (e.g. <c>"Smarty"</c>,
    /// <c>"Geoapify"</c>) for a freshly-resolved external suggestion.
    /// Null when nothing has been applied yet.
    /// </summary>
    public string? LastSelectedSource { get; private set; }

    public IReadOnlyList<AddressSuggestionDto> Suggestions
    {
        get => _suggestions;
        private set
        {
            _suggestions = value;
            SuggestionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Canonical apt/suite options published by the provider after the user
    /// picked a parent suggestion with multiple secondary units. Empty when
    /// the field is in any state other than awaiting a unit pick.
    /// </summary>
    public IReadOnlyList<AddressSuggestionDto> SecondaryOptions
    {
        get => _secondaryOptions;
        private set
        {
            _secondaryOptions = value;
            SecondaryOptionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// True while a secondary-expansion request is in flight. UI surfaces
    /// this as a small spinner next to the Apt/Suite combo so the user
    /// knows units are being fetched.
    /// </summary>
    public bool IsExpandingSecondaries
    {
        get => _isExpandingSecondaries;
        private set
        {
            if (_isExpandingSecondaries == value) return;
            _isExpandingSecondaries = value;
            ExpansionStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// True when the user has picked a parent suggestion with multiple
    /// secondary units but has not yet picked a specific unit. The Address
    /// row is not yet resolved — selecting a unit (or typing a free-form
    /// value and committing) finalizes resolution.
    /// </summary>
    public bool IsAwaitingSecondary { get; private set; }

    public event Action? SuggestionsChanged;
    public event Action<AddressDto>? AddressSelected;
    public event Action? ManualEntryUnlocked;
    public event Action? ManualEntryPromptChanged;
    public event Action? SearchStateChanged;
    public event Action? SecondaryOptionsChanged;
    public event Action? ExpansionStateChanged;
    public event Action<string>? ErrorOccurred;

    /// <summary>
    /// Pre-populates the controller with an existing address (edit-mode entry
    /// point). Sets <see cref="SelectedAddressId"/> so the control knows it
    /// doesn't need to re-save on commit unless the user edits.
    /// </summary>
    public void Preload(AddressDto? address)
    {
        if (address == null) return;
        Fields.Line1 = address.AddressLine1;
        Fields.Line2 = address.AddressLine2;
        Fields.City = address.City;
        Fields.StateProvince = address.StateProvince;
        Fields.PostalCode = address.PostalCode;
        Fields.Country = address.Country;
        SelectedAddressId = address.Id;
        LastSelectedSource = "Local";
        IsManualEntryEnabled = false;
        Suggestions = Array.Empty<AddressSuggestionDto>();
    }

    /// <summary>
    /// Called by the UI when the primary (Line 1) entry changes. Debounces
    /// and runs the autocomplete query.
    /// </summary>
    public async Task OnLine1Changed(string? text)
    {
        Fields.Line1 = text;

        // Editing Line 1 invalidates any prior selection.
        if (SelectedAddressId.HasValue)
            SelectedAddressId = null;

        _activeSearchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _activeSearchCts = cts;
        var ct = cts.Token;

        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length < _minQueryLength)
        {
            Suggestions = Array.Empty<AddressSuggestionDto>();
            OfferManualEntryPrompt = false;
            return;
        }

        try
        {
            await _delayProvider.DelayAsync(_debounce, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested) return;

        IsSearching = true;
        try
        {
            var results = await _client.GetAutocompleteAsync(trimmed, _suggestionLimit, ct);
            if (ct.IsCancellationRequested) return;

            Suggestions = results;

            // Surface the manual-entry affordance as an explicit dropdown
            // row when the provider couldn't match anything — keeps the
            // shared Addresses table from accumulating ad-hoc edits of
            // verified suggestions, while still giving the user a way out.
            OfferManualEntryPrompt =
                results.Count == 0
                && trimmed.Length >= _autoUnlockThreshold
                && !IsManualEntryEnabled;
        }
        catch (OperationCanceledException)
        {
            // Swallow — the user typed again or the field was disposed.
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Address lookup failed: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Called by the UI when the user taps a suggestion row. For parents
    /// with multiple secondary units this fetches the canonical unit list
    /// and transitions the field into the awaiting-unit state without yet
    /// resolving an Address row. For all other suggestions it follows the
    /// existing eager-resolve path.
    /// </summary>
    public async Task OnSuggestionSelected(Guid suggestionId, CancellationToken ct = default)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.SuggestionId == suggestionId);
        if (suggestion == null) return;

        if (suggestion.SecondaryCount > 1)
        {
            await ExpandSecondariesInternal(suggestion, ct);
            return;
        }

        await ResolveInternal(suggestion, ct);
    }

    /// <summary>
    /// Called by the UI when the user picks one of the canonical apt/suite
    /// units returned by <see cref="ExpandSecondariesInternal"/>. Resolves
    /// the child suggestion to a persisted Address and applies it.
    /// </summary>
    public async Task OnSecondaryOptionSelected(Guid childSuggestionId, CancellationToken ct = default)
    {
        var child = _secondaryOptions.FirstOrDefault(s => s.SuggestionId == childSuggestionId);
        if (child == null) return;

        await ResolveInternal(child, ct);
    }

    private async Task ExpandSecondariesInternal(AddressSuggestionDto parent, CancellationToken ct)
    {
        IsExpandingSecondaries = true;
        try
        {
            var result = await _client.GetSecondariesAsync(parent.SuggestionId, ct);

            if (!result.Success)
            {
                if (result.IsExpired)
                {
                    // The parent fell out of cache between when the user saw it
                    // and when they picked it. Re-run the autocomplete query to
                    // re-cache and try the matching parent once.
                    if (await TryReExpandAfterExpiry(parent, ct))
                        return;

                    Suggestions = Array.Empty<AddressSuggestionDto>();
                    ErrorOccurred?.Invoke("Suggestion expired. Please try again.");
                    return;
                }
                ErrorOccurred?.Invoke(result.ErrorMessage ?? "Failed to fetch unit list.");
                return;
            }

            // Pre-populate Line 1 / city / state / postal so the user can see
            // the building they're picking a unit in. The Address row is not
            // saved until the user selects a unit (or commits manually).
            Fields.Line1 = parent.AddressLine1;
            Fields.City = parent.City;
            Fields.StateProvince = parent.StateProvince;
            Fields.PostalCode = parent.PostalCode;
            Fields.Country = parent.Country;
            // Don't pre-fill Line 2 — the user is about to pick one.

            SecondaryOptions = result.Suggestions;
            IsAwaitingSecondary = true;
            Suggestions = Array.Empty<AddressSuggestionDto>();
        }
        catch (OperationCanceledException)
        {
            // caller cancelled
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Address lookup failed: {ex.Message}");
        }
        finally
        {
            IsExpandingSecondaries = false;
        }
    }

    private async Task<bool> TryReExpandAfterExpiry(AddressSuggestionDto staleParent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(staleParent.AddressLine1)) return false;

        try
        {
            var refreshed = await _client.GetAutocompleteAsync(staleParent.AddressLine1!, _suggestionLimit, ct);
            // Find a parent whose normalized address matches the stale one.
            var match = refreshed.FirstOrDefault(s =>
                string.Equals(s.AddressLine1, staleParent.AddressLine1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.City, staleParent.City, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.StateProvince, staleParent.StateProvince, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.PostalCode, staleParent.PostalCode, StringComparison.OrdinalIgnoreCase) &&
                s.SecondaryCount > 1);
            if (match == null) return false;

            var second = await _client.GetSecondariesAsync(match.SuggestionId, ct);
            if (!second.Success) return false;

            Fields.Line1 = match.AddressLine1;
            Fields.City = match.City;
            Fields.StateProvince = match.StateProvince;
            Fields.PostalCode = match.PostalCode;
            Fields.Country = match.Country;

            SecondaryOptions = second.Suggestions;
            IsAwaitingSecondary = true;
            Suggestions = Array.Empty<AddressSuggestionDto>();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task ResolveInternal(AddressSuggestionDto suggestion, CancellationToken ct)
    {
        try
        {
            var result = await _client.ResolveAsync(new ResolveAddressSuggestionRequest
            {
                SuggestionId = suggestion.SuggestionId,
                AddressLine2 = Fields.Line2
            }, ct);

            if (!result.Success)
            {
                if (result.IsExpired)
                {
                    // Ask caller to re-query; we just clear the list so the UI
                    // doesn't keep a dead suggestion around.
                    Suggestions = Array.Empty<AddressSuggestionDto>();
                    SecondaryOptions = Array.Empty<AddressSuggestionDto>();
                    IsAwaitingSecondary = false;
                    ErrorOccurred?.Invoke("Suggestion expired. Please try again.");
                    return;
                }
                ErrorOccurred?.Invoke(result.ErrorMessage ?? "Failed to resolve address.");
                return;
            }

            if (result.Address != null)
                ApplyAddress(result.Address, suggestion.Source);
        }
        catch (OperationCanceledException)
        {
            // caller cancelled
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Address lookup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Flips the controller into manual-entry mode explicitly (e.g., when
    /// the UI offers an "Enter manually" affordance).
    /// </summary>
    public void EnableManualEntry()
    {
        OfferManualEntryPrompt = false;
        if (IsManualEntryEnabled) return;
        IsManualEntryEnabled = true;
        ManualEntryUnlocked?.Invoke();
    }

    /// <summary>
    /// Called on save. If a suggestion is already resolved, returns a
    /// snapshot of the selected address with no network call; if the user
    /// filled in fields manually, runs the standardize-and-create path and
    /// returns the persisted address.
    /// </summary>
    public async Task<AddressDto?> CommitAsync(CancellationToken ct = default)
    {
        if (SelectedAddressId.HasValue)
            return SnapshotCurrentFields(SelectedAddressId.Value);

        if (string.IsNullOrWhiteSpace(Fields.Line1) && string.IsNullOrWhiteSpace(Fields.City))
            return null;

        try
        {
            var address = await _client.StandardizeAsync(new StandardizeAddressRequest
            {
                AddressLine1 = Fields.Line1,
                AddressLine2 = Fields.Line2,
                City = Fields.City,
                StateProvince = Fields.StateProvince,
                PostalCode = Fields.PostalCode,
                Country = Fields.Country
            }, ct);

            if (address != null)
            {
                ApplyAddress(address, "Manual");
                return address;
            }

            ErrorOccurred?.Invoke("Could not save address.");
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to save address: {ex.Message}");
            return null;
        }
    }

    private AddressDto SnapshotCurrentFields(Guid id) => new()
    {
        Id = id,
        AddressLine1 = Fields.Line1,
        AddressLine2 = Fields.Line2,
        City = Fields.City,
        StateProvince = Fields.StateProvince,
        PostalCode = Fields.PostalCode,
        Country = Fields.Country
    };

    /// <summary>
    /// Clears all state. Used when the UI is being re-bound to a new record.
    /// </summary>
    public void Reset()
    {
        _activeSearchCts?.Cancel();
        Fields.Line1 = Fields.Line2 = Fields.City =
            Fields.StateProvince = Fields.PostalCode = Fields.Country = null;
        SelectedAddressId = null;
        LastSelectedSource = null;
        IsManualEntryEnabled = false;
        OfferManualEntryPrompt = false;
        IsAwaitingSecondary = false;
        Suggestions = Array.Empty<AddressSuggestionDto>();
        SecondaryOptions = Array.Empty<AddressSuggestionDto>();
    }

    private void ApplyAddress(AddressDto address, string? source)
    {
        Fields.Line1 = address.AddressLine1;
        // Server never persists Line 2 on the shared Address row; it surfaces
        // the per-contact apt/suite hint via SuggestedLine2 when the resolved
        // suggestion was a secondary expansion. Fall back to AddressLine2 so
        // legacy rows that pre-date the split still preload correctly.
        Fields.Line2 = !string.IsNullOrWhiteSpace(address.SuggestedLine2)
            ? address.SuggestedLine2
            : (!string.IsNullOrWhiteSpace(address.AddressLine2) ? address.AddressLine2 : Fields.Line2);
        Fields.City = address.City;
        Fields.StateProvince = address.StateProvince;
        Fields.PostalCode = address.PostalCode;
        Fields.Country = address.Country;
        SelectedAddressId = address.Id;
        LastSelectedSource = source;
        OfferManualEntryPrompt = false;
        IsAwaitingSecondary = false;
        Suggestions = Array.Empty<AddressSuggestionDto>();
        SecondaryOptions = Array.Empty<AddressSuggestionDto>();
        AddressSelected?.Invoke(address);
    }
}
