using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Pages.Wizard;
using Famick.HomeManagement.Mobile.Services;
using Syncfusion.Maui.Core;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdOverviewPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;
    private List<HouseholdMemberDto> _members = new();

    public HouseholdOverviewPage(ShoppingApiClient apiClient)
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
            var tenantTask = _apiClient.GetTenantAsync();
            var membersTask = _apiClient.GetHouseholdMembersAsync();
            await Task.WhenAll(homeTask, tenantTask, membersTask);

            var homeResult = homeTask.Result;
            var tenantResult = tenantTask.Result;
            var membersResult = membersTask.Result;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (homeResult.Success && homeResult.Data != null)
                {
                    _home = homeResult.Data;

                    // Household name from tenant
                    if (tenantResult.Success && tenantResult.Data != null)
                    {
                        HouseholdNameLabel.Text = tenantResult.Data.Name ?? "My Home";
                    }
                    else
                    {
                        HouseholdNameLabel.Text = "My Home";
                    }
                    AddressLabel.IsVisible = false;

                    // Members
                    if (membersResult.Success && membersResult.Data != null)
                        _members = membersResult.Data;
                    else
                        _members = new();

                    PopulateData();
                    RenderMembersList();
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

        UnitLabel.Text = _home.Unit ?? "--";
        YearBuiltLabel.Text = _home.YearBuilt?.ToString() ?? "--";
        SqFtLabel.Text = _home.SquareFootage?.ToString("N0") ?? "--";
        BedroomsLabel.Text = _home.Bedrooms?.ToString() ?? "--";
        BathroomsLabel.Text = _home.Bathrooms?.ToString("0.#") ?? "--";

        // HOA
        var hasHoa = !string.IsNullOrEmpty(_home.HoaName);
        HoaSectionLabel.IsVisible = hasHoa;
        HoaCard.IsVisible = hasHoa;

        if (hasHoa)
        {
            HoaNameLabel.Text = _home.HoaName;

            HoaContactStack.IsVisible = !string.IsNullOrEmpty(_home.HoaContactInfo);
            HoaContactLabel.Text = _home.HoaContactInfo;

            HoaRulesStack.IsVisible = !string.IsNullOrEmpty(_home.HoaRulesLink);
            HoaRulesLabel.Text = _home.HoaRulesLink;
        }
    }

    private void RenderMembersList()
    {
        MembersListLayout.Children.Clear();

        if (_members.Count == 0)
        {
            NoMembersLabel.IsVisible = true;
            return;
        }

        NoMembersLabel.IsVisible = false;

        foreach (var member in _members)
        {
            var card = new Border
            {
                Padding = new Thickness(12),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Colors.White,
            };
            card.Shadow = new Shadow { Brush = Brush.Black, Offset = new Point(0, 1), Radius = 3, Opacity = 0.08f };

            var initials = "?";
            if (!string.IsNullOrEmpty(member.FirstName) && !string.IsNullOrEmpty(member.LastName))
                initials = $"{member.FirstName[0]}{member.LastName[0]}".ToUpper();
            else if (!string.IsNullOrEmpty(member.FirstName))
                initials = member.FirstName[0].ToString().ToUpper();

            var avatarView = new SfAvatarView
            {
                WidthRequest = 40,
                HeightRequest = 40,
                AvatarShape = AvatarShape.Circle,
                ContentType = ContentType.Initials,
                AvatarName = initials,
                BackgroundColor = Color.FromArgb("#4CAF50"),
                InitialsColor = Colors.White,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                Stroke = Colors.Transparent,
                StrokeThickness = 0
            };

            // Load profile image async
            if (!string.IsNullOrEmpty(member.ProfileImageUrl))
            {
                var avatarRef = avatarView;
                _ = Task.Run(async () =>
                {
                    var source = await _apiClient.LoadImageAsync(member.ProfileImageUrl);
                    if (source != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            avatarRef.ImageSource = source;
                            avatarRef.ContentType = ContentType.Custom;
                        });
                    }
                });
            }

            var nameLabel = new Label
            {
                Text = member.DisplayName,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black
            };

            var detailParts = new List<string>();
            if (!string.IsNullOrEmpty(member.RelationshipType))
                detailParts.Add(member.RelationshipType);

            var detailLabel = new Label
            {
                Text = detailParts.Count > 0 ? string.Join(" - ", detailParts) : "",
                FontSize = 12,
                TextColor = Colors.Gray,
                IsVisible = detailParts.Count > 0
            };

            // Account status
            var hasAccount = member.HasUserAccount;
            var statusLabel = new Label
            {
                Text = hasAccount ? "Has account" : "No account",
                FontSize = 11,
                TextColor = hasAccount ? Color.FromArgb("#4CAF50") : Colors.Gray
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Children.Add(nameLabel);
            if (detailParts.Count > 0)
                textStack.Children.Add(detailLabel);
            textStack.Children.Add(statusLabel);

            var chevron = new Label
            {
                Text = ">",
                FontSize = 18,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Center
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12
            };
            grid.Add(avatarView, 0);
            grid.Add(textStack, 1);
            grid.Add(chevron, 2);

            card.Content = grid;

            var tapGesture = new TapGestureRecognizer();
            var contactId = member.ContactId;
            tapGesture.Tapped += async (s, e) =>
            {
                await Shell.Current.GoToAsync(nameof(ContactDetailPage), new Dictionary<string, object>
                {
                    { "ContactId", contactId.ToString() }
                });
            };
            card.GestureRecognizers.Add(tapGesture);

            MembersListLayout.Children.Add(card);
        }
    }

    private async void OnAddMemberClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(WizardAddMemberPage));
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(HouseholdOverviewEditPage));
    }

    private async void OnHoaRulesLinkTapped(object? sender, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_home?.HoaRulesLink))
        {
            try { await Launcher.OpenAsync(new Uri(_home.HoaRulesLink)); }
            catch { await DisplayAlert("Error", "Could not open link", "OK"); }
        }
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
