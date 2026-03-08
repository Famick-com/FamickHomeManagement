using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdHomeCarePage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;
    private List<PropertyLinkDto> _links = new();

    public HouseholdHomeCarePage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        ShowLoading();

        try
        {
            var homeTask = _apiClient.GetHomeAsync();
            var linksTask = _apiClient.GetPropertyLinksAsync();
            await Task.WhenAll(homeTask, linksTask);

            var homeResult = homeTask.Result;
            var linksResult = linksTask.Result;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (homeResult.Success && homeResult.Data != null)
                {
                    _home = homeResult.Data;
                    _links = linksResult.Success && linksResult.Data != null
                        ? linksResult.Data : new List<PropertyLinkDto>();
                    PopulateData();
                    ShowContent();
                }
                else
                {
                    ShowEmpty();
                }
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(ShowEmpty);
        }
    }

    private void PopulateData()
    {
        if (_home == null) return;

        // HVAC
        AcFilterSizesLabel.Text = _home.AcFilterSizes ?? "--";
        AcFilterIntervalLabel.Text = _home.AcFilterReplacementIntervalDays.HasValue
            ? $"{_home.AcFilterReplacementIntervalDays} days" : "--";

        // Maintenance
        FridgeFilterLabel.Text = _home.FridgeWaterFilterType ?? "--";
        UnderSinkFilterLabel.Text = _home.UnderSinkFilterType ?? "--";
        WholeHouseFilterLabel.Text = _home.WholeHouseFilterType ?? "--";
        BatteryTypeLabel.Text = _home.SmokeCoDetectorBatteryType ?? "--";
        HvacScheduleLabel.Text = _home.HvacServiceSchedule ?? "--";
        PestScheduleLabel.Text = _home.PestControlSchedule ?? "--";

        // Property Links
        RenderLinks();
    }

    private void RenderLinks()
    {
        LinksContainer.Children.Clear();
        NoLinksLabel.IsVisible = _links.Count == 0;

        foreach (var link in _links)
        {
            var linkId = link.Id;
            var card = new SwipeView();
            card.RightItems.Add(new SwipeItem
            {
                Text = "Delete",
                BackgroundColor = Color.FromArgb("#D32F2F"),
                Command = new Command(async () => await DeleteLinkAsync(linkId))
            });

            var border = new Border
            {
                Padding = new Thickness(12, 10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Colors.White
            };
            border.Shadow = new Shadow
            {
                Brush = Colors.Black,
                Offset = new Point(0, 1),
                Radius = 3,
                Opacity = 0.08f
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var labelStack = new VerticalStackLayout { Spacing = 2 };
            labelStack.Children.Add(new Label
            {
                Text = link.Label,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black
            });
            labelStack.Children.Add(new Label
            {
                Text = link.Url,
                FontSize = 12,
                TextColor = Color.FromArgb("#1976D2"),
                LineBreakMode = LineBreakMode.TailTruncation
            });

            row.Children.Add(labelStack);
            Grid.SetColumn(labelStack, 0);

            var chevron = new Label
            {
                Text = "›",
                FontSize = 22,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#616161") : Color.FromArgb("#BDBDBD")
            };
            row.Children.Add(chevron);
            Grid.SetColumn(chevron, 1);

            border.Content = row;
            var url = link.Url;
            border.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    try { await Launcher.OpenAsync(new Uri(url)); }
                    catch { await DisplayAlert("Error", "Could not open link", "OK"); }
                })
            });

            card.Content = border;
            LinksContainer.Children.Add(card);
        }
    }

    private async Task DeleteLinkAsync(Guid id)
    {
        var confirm = await DisplayAlert("Delete", "Delete this property link?", "Delete", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeletePropertyLinkAsync(id);
        if (result.Success)
        {
            await LoadAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete link", "OK");
        }
    }

    private async void OnAddLinkClicked(object? sender, EventArgs e)
    {
        var label = await DisplayPromptAsync("Add Link", "Label:", "OK", "Cancel", "e.g. County Records");
        if (string.IsNullOrWhiteSpace(label)) return;

        var url = await DisplayPromptAsync("Add Link", "URL:", "OK", "Cancel", "https://...");
        if (string.IsNullOrWhiteSpace(url)) return;

        var request = new CreatePropertyLinkRequest
        {
            Label = label.Trim(),
            Url = url.Trim()
        };

        var result = await _apiClient.CreatePropertyLinkAsync(request);
        if (result.Success)
        {
            await LoadAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add link", "OK");
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(HouseholdHomeCareEditPage));
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}
