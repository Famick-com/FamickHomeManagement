using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(GroupId), "GroupId")]
public partial class ContactGroupDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ContactDetailDto? _group;
    private string _groupId = string.Empty;

    public ObservableCollection<ContactDisplayModel> Members { get; } = new();
    public ObservableCollection<ContactAddressDto> Addresses { get; } = new();

    public string GroupId
    {
        get => _groupId;
        set
        {
            _groupId = value;
            _ = LoadGroupAsync();
        }
    }

    public ContactGroupDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        MembersCollection.ItemsSource = Members;
        AddressesCollection.ItemsSource = Addresses;
    }

    private async Task LoadGroupAsync()
    {
        if (!Guid.TryParse(_groupId, out var id)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ContentScroll.IsVisible = false;
            ErrorFrame.IsVisible = false;
        });

        try
        {
            var result = await _apiClient.GetContactGroupAsync(id);
            if (result.Success && result.Data != null)
            {
                _group = result.Data;
                MainThread.BeginInvokeOnMainThread(() => BindGroupData());
                _ = LoadMemberThumbnailsAsync();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingIndicator.IsVisible = false;
                    ErrorFrame.IsVisible = true;
                    ErrorLabel.Text = result.ErrorMessage ?? "Failed to load group";
                });
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                ErrorFrame.IsVisible = true;
                ErrorLabel.Text = $"Connection error: {ex.Message}";
            });
        }
    }

    private void BindGroupData()
    {
        if (_group == null) return;

        Title = _group.DisplayName ?? "Group";
        GroupNameLabel.Text = _group.DisplayName ?? _group.FullName ?? "";

        // Avatar
        var isBusiness = _group.ContactType == 1;
        AvatarBorder.BackgroundColor = isBusiness ? Color.FromArgb("#2196F3") : Color.FromArgb("#4CAF50");
        var words = (GroupNameLabel.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        InitialsLabel.Text = words.Length >= 2 ? $"{words[0][0]}{words[1][0]}".ToUpper()
            : words.Length == 1 ? words[0][0].ToString().ToUpper() : "?";

        if (!string.IsNullOrEmpty(_group.ProfileImageUrl))
            _ = LoadProfileImageAsync();

        // Type badge
        TypeLabel.Text = isBusiness ? "Business" : "Household";
        TypeBadge.BackgroundColor = isBusiness ? Color.FromArgb("#2196F3") : Color.FromArgb("#4CAF50");

        // Business fields
        BusinessSection.IsVisible = isBusiness;
        if (isBusiness)
        {
            WebsiteLabel.Text = _group.Website;
            WebsiteLabel.IsVisible = !string.IsNullOrEmpty(_group.Website);
            CategoryLabel.Text = _group.BusinessCategory;
            CategoryLabel.IsVisible = !string.IsNullOrEmpty(_group.BusinessCategory);
        }

        // Addresses
        Addresses.Clear();
        foreach (var addr in _group.Addresses)
            Addresses.Add(addr);
        NoAddressesLabel.IsVisible = Addresses.Count == 0;

        // Tags
        TagsLayout.Children.Clear();
        if (_group.Tags.Count > 0)
        {
            foreach (var tag in _group.Tags)
            {
                var tagColor = !string.IsNullOrEmpty(tag.Color)
                    ? Color.FromArgb(tag.Color) : Color.FromArgb("#9E9E9E");
                var chip = new Border
                {
                    Padding = new Thickness(8, 2),
                    Margin = new Thickness(0, 0, 4, 4),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Stroke = Colors.Transparent,
                    BackgroundColor = tagColor,
                    Content = new Label
                    {
                        Text = tag.Name,
                        FontSize = 11,
                        TextColor = Colors.White
                    }
                };
                TagsLayout.Children.Add(chip);
            }
        }

        // Members
        Members.Clear();
        if (_group.Members != null)
        {
            foreach (var member in _group.Members)
            {
                Members.Add(new ContactDisplayModel(member));
            }
        }
        NoMembersLabel.IsVisible = Members.Count == 0;

        // Notes
        if (!string.IsNullOrEmpty(_group.Notes))
        {
            NotesSection.IsVisible = true;
            NotesLabel.Text = _group.Notes;
        }

        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
    }

    private async Task LoadProfileImageAsync()
    {
        var source = await _apiClient.LoadImageAsync(_group?.ProfileImageUrl);
        if (source != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ProfileImage.Source = source;
                ProfileImage.IsVisible = true;
            });
        }
    }

    private async Task LoadMemberThumbnailsAsync()
    {
        foreach (var member in Members.ToList())
        {
            var url = member.ProfileImageUrl ?? member.GravatarUrl;
            if (string.IsNullOrEmpty(url)) continue;
            var source = await _apiClient.LoadImageAsync(url);
            if (source != null)
                MainThread.BeginInvokeOnMainThread(() => member.ThumbnailSource = source);
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_group == null) return;
        await Shell.Current.GoToAsync(nameof(ContactGroupEditPage), new Dictionary<string, object>
        {
            { "GroupId", _group.Id.ToString() }
        });
    }

    private async void OnMemberSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ContactDisplayModel selected) return;
        MembersCollection.SelectedItem = null;

        await Shell.Current.GoToAsync(nameof(ContactDetailPage), new Dictionary<string, object>
        {
            { "ContactId", selected.Id.ToString() }
        });
    }

    private async void OnAddMemberClicked(object? sender, EventArgs e)
    {
        if (_group == null) return;
        await Shell.Current.GoToAsync(nameof(ContactEditPage), new Dictionary<string, object>
        {
            { "ContactId", string.Empty },
            { "ParentGroupId", _group.Id.ToString() }
        });
    }

    private async void OnMoveMemberSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactDisplayModel member }) return;

        var popup = new MoveToGroupPopup(_apiClient, _group?.Id);
        var popupResult = await this.ShowPopupAsync<MoveToGroupResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var moveResult = await _apiClient.MoveContactToGroupAsync(member.Id, result.GroupId);
        if (moveResult.Success)
        {
            Members.Remove(member);
            if (Members.Count == 0) NoMembersLabel.IsVisible = true;
        }
        else
        {
            await DisplayAlert("Error", moveResult.ErrorMessage ?? "Failed to move contact", "OK");
        }
    }

    private async void OnRemoveMemberSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactDisplayModel member }) return;

        var confirm = await DisplayAlert("Remove Member",
            $"Remove \"{member.DisplayName}\" from this group?", "Remove", "Cancel");
        if (!confirm) return;

        var deleteResult = await _apiClient.DeleteContactAsync(member.Id);
        if (deleteResult.Success)
        {
            Members.Remove(member);
            if (Members.Count == 0) NoMembersLabel.IsVisible = true;
        }
        else
        {
            await DisplayAlert("Error", deleteResult.ErrorMessage ?? "Failed to remove member", "OK");
        }
    }

    private async void OnWebsiteTapped(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_group?.Website))
        {
            try
            {
                var uri = _group.Website.StartsWith("http") ? _group.Website : $"https://{_group.Website}";
                await Launcher.OpenAsync(new Uri(uri));
            }
            catch { /* ignore */ }
        }
    }

    // --- Address Actions ---

    private async void OnAddAddressClicked(object? sender, EventArgs e)
    {
        if (_group == null) return;
        var popup = new AddAddressPopup(_apiClient);
        var popupResult = await this.ShowPopupAsync<AddAddressResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactAddressAsync(_group.Id, new AddContactAddressRequest
        {
            AddressId = result.AddressId,
            AddressLine1 = result.AddressLine1,
            AddressLine2 = result.AddressLine2,
            City = result.City,
            StateProvince = result.StateProvince,
            PostalCode = result.PostalCode,
            Country = result.Country,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary
        });
        if (apiResult.Success) await LoadGroupAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add address", "OK");
    }

    private async void OnEditAddressSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactAddressDto addr } || _group == null) return;

        var popup = new AddAddressPopup(addr);
        var popupResult = await this.ShowPopupAsync<AddAddressResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.UpdateContactAddressAsync(_group.Id, addr.Id, new AddContactAddressRequest
        {
            AddressLine1 = result.AddressLine1,
            AddressLine2 = result.AddressLine2,
            City = result.City,
            StateProvince = result.StateProvince,
            PostalCode = result.PostalCode,
            Country = result.Country,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary
        });
        if (apiResult.Success) await LoadGroupAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to update address", "OK");
    }

    private async void OnSetPrimaryAddressSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactAddressDto addr } || _group == null) return;
        if (addr.IsPrimary) return;

        var result = await _apiClient.SetPrimaryAddressAsync(_group.Id, addr.Id);
        if (result.Success) await LoadGroupAsync();
    }

    private async void OnDeleteAddressSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactAddressDto addr } || _group == null) return;

        var confirm = await DisplayAlert("Delete Address",
            "Are you sure you want to delete this address?", "Delete", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.RemoveContactAddressAsync(_group.Id, addr.Id);
        if (result.Success)
        {
            Addresses.Remove(addr);
            NoAddressesLabel.IsVisible = Addresses.Count == 0;
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete address", "OK");
        }
    }

    private async void OnAddressItemTapped(object? sender, EventArgs e)
    {
        if (sender is not Border { BindingContext: ContactAddressDto addr }) return;

        if (addr.Address.Latitude != null && addr.Address.Longitude != null)
        {
            await Map.Default.OpenAsync(addr.Address.Latitude.Value, addr.Address.Longitude.Value,
                new MapLaunchOptions { Name = _group?.DisplayName });
        }
        else if (!string.IsNullOrEmpty(addr.Address.DisplayAddress))
        {
            await Map.Default.OpenAsync(new Placemark
            {
                Thoroughfare = addr.Address.AddressLine1,
                Locality = addr.Address.City,
                AdminArea = addr.Address.StateProvince,
                PostalCode = addr.Address.PostalCode,
                CountryName = addr.Address.Country
            }, new MapLaunchOptions { Name = _group?.DisplayName });
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadGroupAsync();
    }
}
