using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Controls;

/// <summary>
/// Picks the row template for the add-item results list, which holds three kinds of row:
/// selectable products, section headings, and a progress placeholder while the store
/// search is still running.
/// </summary>
public class SearchResultTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ProductTemplate { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? ProgressTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) => item switch
    {
        SearchSectionHeader => HeaderTemplate,
        SearchProgressRow => ProgressTemplate,
        _ => ProductTemplate
    };
}
