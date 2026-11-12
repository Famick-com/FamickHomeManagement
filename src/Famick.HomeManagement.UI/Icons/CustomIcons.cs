namespace Famick.HomeManagement.UI.Icons;

public static class CustomIcons
{
    /// <summary>
    /// Fork + knife scaled into the top-left, with a calendar badge in the
    /// bottom-right corner. 24x24 viewBox.
    /// </summary>
    /// <remarks>
    /// MudBlazor's <c>Icon</c> parameter takes the inner SVG markup, which it
    /// injects into its own <c>&lt;svg viewBox="0 0 24 24"&gt;</c> — so this is
    /// elements, not bare path data, and <c>&lt;g transform&gt;</c> works for
    /// composing two stock 24x24 glyphs into one.
    /// </remarks>
    public const string MealPlanner =
        // Fork + knife (Material Design "Restaurant"), shrunk into the top-left.
        "<g transform=\"translate(-1.6 -1) scale(0.72)\">"
        + "<path d=\"M11 9H9V2H7v7H5V2H3v7c0 2.12 1.66 3.84 3.75 3.97V22h2.5v-9.03C11.34 12.84 13 11.12 13 9V2h-2v7zm5-3v8h2.5v8H21V2c-2.76 0-5 2.24-5 4z\"/>"
        + "</g>"
        // Calendar badge (Material Design "CalendarToday"), bottom-right.
        + "<g transform=\"translate(13.4 13.4) scale(0.4417)\">"
        + "<path d=\"M20 3h-1V1h-2v2H7V1H5v2H4c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 18H4V8h16v13z\"/>"
        + "</g>";

    /// <summary>
    /// The share icon Windows uses: an open box with an arrow curving out of its top-right
    /// corner. Drawn for this, as Material has none like it. Stroked, so each path turns off the
    /// fill MudBlazor's <c>&lt;svg&gt;</c> applies.
    /// </summary>
    public const string WindowsShare =
        "<path d=\"M11 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"
        + "<path d=\"M9 15c0-5.5 4-9 11-9M16.5 2.5 20 6l-3.5 3.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
}
