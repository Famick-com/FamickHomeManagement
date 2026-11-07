using System.Reflection;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.UI.Components.Recipes;
using Famick.HomeManagement.UI.Localization;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;

namespace Famick.HomeManagement.Tests.Unit.Navigation;

public class RecipeShareDialogTests
{
    [Theory]
    [InlineData("https://example.test/", "https://example.test/recipes/share/token")]
    [InlineData("https://example.test/api/hassio_ingress/abc/", "https://example.test/api/hassio_ingress/abc/recipes/share/token")]
    public async Task ShareLink_UsesAppBaseForDisplayAndClipboard(string baseUri, string expected)
    {
        var js = new RecordingJsRuntime();
        var dialog = new RecipeShareDialog();
        dialog.ShareTokens.Add(new RecipeShareDto
            { ShareUrl = "/recipes/share/token", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        SetInjectedProperty(dialog, "Navigation", new TestNavigationManager(baseUri));
        SetInjectedProperty(dialog, "JS", js);
        SetInjectedProperty(dialog, "Snackbar", Mock.Of<ISnackbar>());
        SetInjectedProperty(dialog, "L", Mock.Of<ILocalizer>());

        typeof(RecipeShareDialog).GetMethod("OnParametersSet", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialog, null);

        var displayedUrl = typeof(RecipeShareDialog).GetProperty("AbsoluteShareUrl", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dialog);
        displayedUrl.Should().Be(expected);

        var copy = (Task)typeof(RecipeShareDialog).GetMethod("CopyLinkAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialog, null)!;
        await copy;
        js.Identifier.Should().Be("navigator.clipboard.writeText");
        js.Arguments.Should().Equal(expected);
    }

    private static void SetInjectedProperty(RecipeShareDialog dialog, string name, object value) =>
        typeof(RecipeShareDialog).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(dialog, value);

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri) => Initialize(baseUri, baseUri);

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public string? Identifier { get; private set; }
        public object?[]? Arguments { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Identifier = identifier;
            Arguments = args;
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
