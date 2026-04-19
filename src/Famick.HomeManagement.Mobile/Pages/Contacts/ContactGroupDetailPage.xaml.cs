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
        BindableLayout.SetItemsSource(MembersLayout, Members);
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
        AvatarView.BackgroundColor = isBusiness ? Color.FromArgb("#2196F3") : Color.FromArgb("#4CAF50");
        AvatarView.AvatarName = GroupNameLabel.Text ?? "?";

        if (!string.IsNullOrEmpty(_group.ProfileImageUrl))
            _ = LoadProfileImageAsync();

        // Type badge
        TypeLabel.Text = isBusiness ? "Business" : "Household";
        TypeBadge.BackgroundColor = isBusiness ? Color.FromArgb("#2196F3") : Color.FromArgb("#4CAF50");

        // Business fields
        BusinessSection.IsVisible = isBusiness;
        if (isBusiness)
        {
            var bizPhones = _group.PhoneNumbers ?? new List<ContactPhoneNumberDto>();
            var primaryPhone = bizPhones.FirstOrDefault(p => p.IsPrimary)
                ?? bizPhones.FirstOrDefault();
            if (primaryPhone != null)
            {
                BusinessPhoneLabel.Text = primaryPhone.PhoneNumber;
                BusinessPhoneLabel.IsVisible = true;
            }
            else
            {
                BusinessPhoneLabel.IsVisible = false;
            }

            WebsiteLabel.Text = _group.Website;
            WebsiteLabel.IsVisible = !string.IsNullOrEmpty(_group.Website);
            CategoryLabel.Text = _group.BusinessCategory;
            CategoryLabel.IsVisible = !string.IsNullOrEmpty(_group.BusinessCategory);
        }

        // Addresses
        AddressesCollection.ItemsSource = new ObservableCollection<ContactAddressDto>(_group.Addresses);
        AddressHeader.ContactId = _group.Id;

        // Phones
        var phones = _group.PhoneNumbers ?? new List<ContactPhoneNumberDto>();
        PhonesCollection.ItemsSource = new ObservableCollection<ContactPhoneNumberDto>(phones);
        PhoneHeader.ContactId = _group.Id;

        // Tags
        TagsLayout.Children.Clear();
        if (_group.Tags?.Count > 0)
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

    private async void OnAvatarTapped(object? sender, EventArgs e)
    {
        if (_group == null) return;

        var hasImage = AvatarView.ImageSource != null;
        var options = hasImage
            ? new[] { "Take Photo", "Choose from Gallery", "Remove Image" }
            : new[] { "Take Photo", "Choose from Gallery" };

        var action = await DisplayActionSheet("Group Photo", "Cancel", null, options);

        switch (action)
        {
            case "Take Photo":
                await CaptureAndUploadGroupImageAsync(true);
                break;
            case "Choose from Gallery":
                await CaptureAndUploadGroupImageAsync(false);
                break;
            case "Remove Image":
                var result = await _apiClient.DeleteContactProfileImageAsync(_group.Id);
                if (result.Success)
                {
                    AvatarView.ImageSource = null;
                    AvatarView.ContentType = Syncfusion.Maui.Core.ContentType.Initials;
                }
                break;
        }
    }

    private async Task CaptureAndUploadGroupImageAsync(bool useCamera)
    {
        if (_group == null) return;

        try
        {
            FileResult? photo;
            if (useCamera)
            {
                photo = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select Group Photo"
                });
            }

            if (photo == null) return;

            var stream = await photo.OpenReadAsync();
            var cropPage = new Profile.ProfileImageCropPage(stream);
            await Navigation.PushModalAsync(new NavigationPage(cropPage));
            var croppedStream = await cropPage.CropResultTask;
            if (croppedStream == null) return;

            var result = await _apiClient.UploadContactProfileImageAsync(_group.Id, croppedStream, "profile.png");
            if (result.Success)
            {
                await LoadGroupAsync(); // Reload to get new image URL
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to upload image", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to capture image: {ex.Message}", "OK");
        }
    }

    private async Task LoadProfileImageAsync()
    {
        var source = await _apiClient.LoadImageAsync(_group?.ProfileImageUrl);
        if (source != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AvatarView.ImageSource = source;
                AvatarView.ContentType = Syncfusion.Maui.Core.ContentType.Custom;
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

    private async void OnMemberTapped(object? sender, EventArgs e)
    {
        if (sender is not Border { BindingContext: ContactDisplayModel selected }) return;

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
        if (sender is not SwipeItemView { BindingContext: ContactDisplayModel member }) return;

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
        if (sender is not SwipeItemView { BindingContext: ContactDisplayModel member }) return;

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

    private async void OnBusinessPhoneTapped(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(BusinessPhoneLabel.Text))
        {
            try { PhoneDialer.Default.Open(BusinessPhoneLabel.Text); }
            catch { await DisplayAlert("Error", "Cannot open phone dialer", "OK"); }
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

    // --- Contact Data Actions ---

    private async void OnContactDataChanged(object? sender, EventArgs e) => await LoadGroupAsync();

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadGroupAsync();
    }
}
