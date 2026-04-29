using Famick.HomeManagement.Mobile.Controls;
using Famick.HomeManagement.Mobile.Models;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controls;

public class AddressAutocompleteControllerTests
{
    private static AddressAutocompleteController Create(FakeClient client, FakeDelay? delay = null) =>
        new(client, delay ?? new FakeDelay(), debounce: TimeSpan.FromMilliseconds(1));

    [Fact]
    public async Task OnLine1Changed_DoesNotQuery_WhenUnderMinLength()
    {
        var client = new FakeClient();
        var controller = Create(client);

        await controller.OnLine1Changed("a");

        client.AutocompleteCalls.Should().Be(0);
        controller.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task OnLine1Changed_QueriesAndPublishesSuggestions()
    {
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = Guid.NewGuid(), Source = "Local", AddressLine1 = "123 Main St", AddressId = Guid.NewGuid() }
            }
        };
        var controller = Create(client);
        var publishCount = 0;
        controller.SuggestionsChanged += () => publishCount++;

        await controller.OnLine1Changed("123");

        client.AutocompleteCalls.Should().Be(1);
        client.LastAutocompleteQuery.Should().Be("123");
        controller.Suggestions.Should().HaveCount(1);
        publishCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OnLine1Changed_BurstOfChanges_CancelsEarlierQueries()
    {
        var client = new FakeClient { AutocompleteDelay = TimeSpan.FromMilliseconds(50) };
        var controller = Create(client);

        // Kick off three rapid edits; only the last should survive.
        var t1 = controller.OnLine1Changed("12");
        var t2 = controller.OnLine1Changed("123");
        var t3 = controller.OnLine1Changed("1234");
        await Task.WhenAll(t1, t2, t3);

        client.LastAutocompleteQuery.Should().Be("1234");
    }

    [Fact]
    public async Task OnLine1Changed_OffersManualEntryPrompt_WhenNoResultsAndLongEnough()
    {
        var client = new FakeClient { AutocompleteResponse = new List<AddressSuggestionDto>() };
        var controller = Create(client);

        await controller.OnLine1Changed("xyzxyz");

        // The prompt is the user-facing affordance — manual entry itself
        // doesn't engage until they tap it.
        controller.OfferManualEntryPrompt.Should().BeTrue();
        controller.IsManualEntryEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task OnLine1Changed_DoesNotOfferPrompt_WhenBelowThreshold()
    {
        var client = new FakeClient { AutocompleteResponse = new List<AddressSuggestionDto>() };
        var controller = Create(client);

        await controller.OnLine1Changed("xy"); // meets minQuery (2) but below autoUnlock (3)

        controller.OfferManualEntryPrompt.Should().BeFalse();
        controller.IsManualEntryEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task OnLine1Changed_HidesPrompt_WhenResultsReturn()
    {
        var client = new FakeClient { AutocompleteResponse = new List<AddressSuggestionDto>() };
        var controller = Create(client);

        await controller.OnLine1Changed("xyzxyz");
        controller.OfferManualEntryPrompt.Should().BeTrue();

        client.AutocompleteResponse = new List<AddressSuggestionDto>
        {
            new() { SuggestionId = Guid.NewGuid(), Source = "Smarty", AddressLine1 = "123 Main St" }
        };
        await controller.OnLine1Changed("123 main");

        controller.OfferManualEntryPrompt.Should().BeFalse();
    }

    [Fact]
    public async Task EnableManualEntry_ClearsPromptAndEnablesManualMode()
    {
        var client = new FakeClient { AutocompleteResponse = new List<AddressSuggestionDto>() };
        var controller = Create(client);
        var unlockedFiredCount = 0;
        controller.ManualEntryUnlocked += () => unlockedFiredCount++;

        await controller.OnLine1Changed("xyzxyz");
        controller.OfferManualEntryPrompt.Should().BeTrue();

        controller.EnableManualEntry();

        controller.IsManualEntryEnabled.Should().BeTrue();
        controller.OfferManualEntryPrompt.Should().BeFalse();
        unlockedFiredCount.Should().Be(1);
    }

    [Fact]
    public async Task OnLine1Changed_ClearsPreviousSelection()
    {
        var addressId = Guid.NewGuid();
        var client = new FakeClient
        {
            ResolveResponse = ResolveAddressSuggestionResult.Ok(new AddressDto
            {
                Id = addressId,
                AddressLine1 = "123 Main St"
            }),
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = Guid.NewGuid(), Source = "Local", AddressLine1 = "123 Main St", AddressId = addressId }
            }
        };
        var controller = Create(client);

        await controller.OnLine1Changed("123");
        await controller.OnSuggestionSelected(controller.Suggestions[0].SuggestionId);
        controller.SelectedAddressId.Should().Be(addressId);

        await controller.OnLine1Changed("124");

        controller.SelectedAddressId.Should().BeNull();
    }

    [Fact]
    public async Task OnSuggestionSelected_Resolves_AndPopulatesFields()
    {
        var addressId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = suggestionId, Source = "Smarty", AddressLine1 = "123 Main St" }
            },
            ResolveResponse = ResolveAddressSuggestionResult.Ok(new AddressDto
            {
                Id = addressId,
                AddressLine1 = "123 MAIN ST",
                City = "SPRINGFIELD",
                StateProvince = "IL",
                PostalCode = "62701-1234"
            })
        };
        var controller = Create(client);
        AddressDto? receivedAddress = null;
        controller.AddressSelected += a => receivedAddress = a;

        await controller.OnLine1Changed("123");
        await controller.OnSuggestionSelected(suggestionId);

        controller.SelectedAddressId.Should().Be(addressId);
        controller.Fields.Line1.Should().Be("123 MAIN ST");
        controller.Fields.PostalCode.Should().Be("62701-1234");
        receivedAddress.Should().NotBeNull();
        receivedAddress!.Id.Should().Be(addressId);
        controller.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task OnSuggestionSelected_ForwardsLine2Override()
    {
        var suggestionId = Guid.NewGuid();
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = suggestionId, Source = "Smarty" }
            },
            ResolveResponse = ResolveAddressSuggestionResult.Ok(new AddressDto { Id = Guid.NewGuid() })
        };
        var controller = Create(client);
        controller.Fields.Line2 = "Apt 4";

        await controller.OnLine1Changed("123");
        await controller.OnSuggestionSelected(suggestionId);

        client.LastResolveRequest.Should().NotBeNull();
        client.LastResolveRequest!.AddressLine2.Should().Be("Apt 4");
    }

    [Fact]
    public async Task OnSuggestionSelected_ReportsExpiration_AndClearsSuggestions()
    {
        var suggestionId = Guid.NewGuid();
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = suggestionId, Source = "Smarty" }
            },
            ResolveResponse = ResolveAddressSuggestionResult.Expired()
        };
        var controller = Create(client);
        string? error = null;
        controller.ErrorOccurred += e => error = e;

        await controller.OnLine1Changed("123");
        await controller.OnSuggestionSelected(suggestionId);

        error.Should().NotBeNull();
        controller.SelectedAddressId.Should().BeNull();
        controller.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task CommitAsync_ReturnsSnapshot_WithoutNetworkCall_WhenAlreadyResolved()
    {
        var addressId = Guid.NewGuid();
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = Guid.NewGuid(), Source = "Local", AddressId = addressId }
            },
            ResolveResponse = ResolveAddressSuggestionResult.Ok(new AddressDto
            {
                Id = addressId,
                AddressLine1 = "123 Main St"
            })
        };
        var controller = Create(client);

        await controller.OnLine1Changed("123");
        await controller.OnSuggestionSelected(controller.Suggestions[0].SuggestionId);
        var before = client.StandardizeCalls;

        var committed = await controller.CommitAsync();

        committed.Should().NotBeNull();
        committed!.Id.Should().Be(addressId);
        committed.AddressLine1.Should().Be("123 Main St");
        client.StandardizeCalls.Should().Be(before);
    }

    [Fact]
    public async Task CommitAsync_CallsStandardize_WhenManualFieldsAreSet()
    {
        var addressId = Guid.NewGuid();
        var client = new FakeClient
        {
            StandardizeResponse = new AddressDto
            {
                Id = addressId,
                AddressLine1 = "1 INFINITE LOOP",
                City = "CUPERTINO",
                StateProvince = "CA",
                PostalCode = "95014"
            }
        };
        var controller = Create(client);
        controller.Fields.Line1 = "1 infinite loop";
        controller.Fields.City = "cupertino";
        controller.Fields.StateProvince = "ca";
        controller.Fields.PostalCode = "95014";
        controller.Fields.Country = "US";

        var committed = await controller.CommitAsync();

        committed.Should().NotBeNull();
        committed!.Id.Should().Be(addressId);
        client.StandardizeCalls.Should().Be(1);
        controller.Fields.Line1.Should().Be("1 INFINITE LOOP");
    }

    [Fact]
    public async Task CommitAsync_ReturnsNull_WhenNothingSupplied()
    {
        var client = new FakeClient();
        var controller = Create(client);

        var committed = await controller.CommitAsync();

        committed.Should().BeNull();
        client.StandardizeCalls.Should().Be(0);
    }

    [Fact]
    public void Preload_PopulatesFields_AndMarksSelected()
    {
        var controller = Create(new FakeClient());
        var addressId = Guid.NewGuid();

        controller.Preload(new AddressDto
        {
            Id = addressId,
            AddressLine1 = "100 Market St",
            City = "San Francisco",
            StateProvince = "CA",
            PostalCode = "94103"
        });

        controller.SelectedAddressId.Should().Be(addressId);
        controller.LastSelectedSource.Should().Be("Local");
        controller.Fields.Line1.Should().Be("100 Market St");
        controller.IsManualEntryEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task LastSelectedSource_ReflectsLocalHit()
    {
        var addressId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = suggestionId, Source = "Local", AddressId = addressId, AddressLine1 = "x" }
            },
            ResolveResponse = ResolveAddressSuggestionResult.Ok(new AddressDto { Id = addressId, AddressLine1 = "x" })
        };
        var controller = Create(client);

        await controller.OnLine1Changed("xx");
        await controller.OnSuggestionSelected(suggestionId);

        controller.LastSelectedSource.Should().Be("Local");
    }

    [Fact]
    public async Task LastSelectedSource_ReflectsExternalProvider()
    {
        var addressId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var client = new FakeClient
        {
            AutocompleteResponse = new List<AddressSuggestionDto>
            {
                new() { SuggestionId = suggestionId, Source = "Smarty", AddressLine1 = "y" }
            },
            ResolveResponse = ResolveAddressSuggestionResult.Ok(new AddressDto { Id = addressId, AddressLine1 = "y" })
        };
        var controller = Create(client);

        await controller.OnLine1Changed("yy");
        await controller.OnSuggestionSelected(suggestionId);

        controller.LastSelectedSource.Should().Be("Smarty");
    }

    [Fact]
    public async Task LastSelectedSource_IsManual_OnCommitWithoutSelection()
    {
        var addressId = Guid.NewGuid();
        var client = new FakeClient
        {
            StandardizeResponse = new AddressDto { Id = addressId, AddressLine1 = "z" }
        };
        var controller = Create(client);
        controller.Fields.Line1 = "z";
        controller.Fields.City = "Town";

        await controller.CommitAsync();

        controller.LastSelectedSource.Should().Be("Manual");
    }

    [Fact]
    public void Reset_ClearsLastSelectedSource()
    {
        var controller = Create(new FakeClient());
        controller.Preload(new AddressDto { Id = Guid.NewGuid(), AddressLine1 = "x" });
        controller.LastSelectedSource.Should().Be("Local");

        controller.Reset();

        controller.LastSelectedSource.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var controller = Create(new FakeClient());
        controller.Fields.Line1 = "123 Main St";
        controller.Preload(new AddressDto { Id = Guid.NewGuid(), AddressLine1 = "x" });

        controller.Reset();

        controller.SelectedAddressId.Should().BeNull();
        controller.Fields.Line1.Should().BeNull();
        controller.IsManualEntryEnabled.Should().BeFalse();
        controller.Suggestions.Should().BeEmpty();
    }

    // ---------- Fakes ----------

    private sealed class FakeClient : IAddressAutocompleteClient
    {
        public List<AddressSuggestionDto> AutocompleteResponse { get; set; } = new();
        public ResolveAddressSuggestionResult ResolveResponse { get; set; } =
            ResolveAddressSuggestionResult.Fail("not configured");
        public AddressDto? StandardizeResponse { get; set; }
        public TimeSpan AutocompleteDelay { get; set; } = TimeSpan.Zero;

        public int AutocompleteCalls { get; private set; }
        public int StandardizeCalls { get; private set; }
        public string? LastAutocompleteQuery { get; private set; }
        public ResolveAddressSuggestionRequest? LastResolveRequest { get; private set; }

        public async Task<List<AddressSuggestionDto>> GetAutocompleteAsync(string query, int limit, CancellationToken ct)
        {
            AutocompleteCalls++;
            LastAutocompleteQuery = query;
            if (AutocompleteDelay > TimeSpan.Zero)
                await Task.Delay(AutocompleteDelay, ct);
            return AutocompleteResponse;
        }

        public Task<ResolveAddressSuggestionResult> ResolveAsync(ResolveAddressSuggestionRequest request, CancellationToken ct)
        {
            LastResolveRequest = request;
            return Task.FromResult(ResolveResponse);
        }

        public Task<AddressDto?> StandardizeAsync(StandardizeAddressRequest request, CancellationToken ct)
        {
            StandardizeCalls++;
            return Task.FromResult(StandardizeResponse);
        }
    }

    private sealed class FakeDelay : IDelayProvider
    {
        public Task DelayAsync(TimeSpan duration, CancellationToken ct) => Task.CompletedTask;
    }
}
