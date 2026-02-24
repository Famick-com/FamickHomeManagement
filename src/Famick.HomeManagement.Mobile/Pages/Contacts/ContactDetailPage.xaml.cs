using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(ContactId), "ContactId")]
public partial class ContactDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ContactDetailDto? _contact;
    private string _contactId = string.Empty;

    public string ContactId
    {
        get => _contactId;
        set
        {
            _contactId = value;
            _ = LoadContactAsync();
        }
    }

    public ContactDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_contact != null)
            await LoadContactAsync();
    }

    private async Task LoadContactAsync()
    {
        if (!Guid.TryParse(_contactId, out var id)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ContentScroll.IsVisible = false;
            ErrorFrame.IsVisible = false;
        });

        try
        {
            var result = await _apiClient.GetContactAsync(id);
            if (result.Success && result.Data != null)
            {
                _contact = result.Data;
                MainThread.BeginInvokeOnMainThread(BindContactData);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingIndicator.IsVisible = false;
                    ErrorFrame.IsVisible = true;
                    ErrorLabel.Text = result.ErrorMessage ?? "Failed to load contact";
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

    private void BindContactData()
    {
        if (_contact == null) return;

        Title = _contact.DisplayName ?? "Contact";
        DisplayNameLabel.Text = _contact.DisplayName ?? "";

        // Initials
        var first = _contact.FirstName;
        var last = _contact.LastName;
        if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(last))
            InitialsLabel.Text = $"{first[0]}{last[0]}".ToUpper();
        else if (!string.IsNullOrEmpty(first))
            InitialsLabel.Text = first[0].ToString().ToUpper();
        else
            InitialsLabel.Text = "?";

        // Profile image
        if (!string.IsNullOrEmpty(_contact.ProfileImageUrl))
            _ = LoadProfileImageAsync(_contact.ProfileImageUrl);
        else if (!string.IsNullOrEmpty(_contact.GravatarUrl) && _contact.UseGravatar)
            _ = LoadProfileImageAsync(_contact.GravatarUrl);

        // Company + title
        var companyTitle = string.Join(" - ",
            new[] { _contact.Title, _contact.CompanyName }.Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(companyTitle))
        {
            CompanyTitleLabel.Text = companyTitle;
            CompanyTitleLabel.IsVisible = true;
        }

        // Group name
        if (!string.IsNullOrEmpty(_contact.ParentGroupName))
        {
            GroupNameLabel.Text = $"Member of: {_contact.ParentGroupName}";
            GroupNameLabel.IsVisible = true;
        }

        // Birthday
        if (!string.IsNullOrEmpty(_contact.FormattedBirthDate))
        {
            var text = _contact.FormattedBirthDate;
            if (_contact.Age.HasValue) text += $" (age {_contact.Age})";
            BirthdayLabel.Text = text;
            BirthdayLabel.IsVisible = true;
        }

        // Tags
        TagsLayout.Children.Clear();
        foreach (var tag in _contact.Tags)
        {
            var color = !string.IsNullOrEmpty(tag.Color) ? Color.FromArgb(tag.Color) : Color.FromArgb("#9E9E9E");
            TagsLayout.Children.Add(new Border
            {
                Padding = new Thickness(8, 2),
                Margin = new Thickness(0, 0, 4, 4),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Stroke = Colors.Transparent,
                BackgroundColor = color,
                Content = new Label { Text = tag.Name, FontSize = 11, TextColor = Colors.White }
            });
        }

        // Phones
        PhonesCollection.ItemsSource = new ObservableCollection<ContactPhoneNumberDto>(_contact.PhoneNumbers);
        PhonesSection.IsVisible = _contact.PhoneNumbers.Count > 0 || true; // always show for + button

        // Emails
        EmailsCollection.ItemsSource = new ObservableCollection<ContactEmailAddressDto>(_contact.EmailAddresses);
        EmailsSection.IsVisible = true;

        // Addresses
        AddressesCollection.ItemsSource = new ObservableCollection<ContactAddressDto>(_contact.Addresses);
        AddressesSection.IsVisible = true;

        // Social Media
        SocialMediaCollection.ItemsSource = new ObservableCollection<ContactSocialMediaDto>(_contact.SocialMedia);
        SocialSection.IsVisible = _contact.SocialMedia.Count > 0 || true;

        // Relationships
        RelationshipsCollection.ItemsSource = new ObservableCollection<ContactRelationshipDto>(_contact.Relationships);
        RelationshipsSection.IsVisible = true;

        // Notes
        if (!string.IsNullOrEmpty(_contact.Notes))
        {
            NotesSection.IsVisible = true;
            NotesLabel.Text = _contact.Notes;
        }

        // Shares
        SharesCollection.ItemsSource = new ObservableCollection<ContactUserShareDto>(_contact.SharedWithUsers);
        SharingSection.IsVisible = true;

        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
    }

    private async Task LoadProfileImageAsync(string url)
    {
        var source = await _apiClient.LoadImageAsync(url);
        if (source != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ProfileImage.Source = source;
                ProfileImage.IsVisible = true;
            });
        }
    }

    // --- Navigation ---

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        await Shell.Current.GoToAsync(nameof(ContactEditPage), new Dictionary<string, object>
        {
            { "ContactId", _contact.Id.ToString() }
        });
    }

    private async void OnGroupNameTapped(object? sender, EventArgs e)
    {
        if (_contact?.ParentContactId != null)
        {
            await Shell.Current.GoToAsync(nameof(ContactGroupDetailPage), new Dictionary<string, object>
            {
                { "GroupId", _contact.ParentContactId.Value.ToString() }
            });
        }
    }

    private async void OnViewHistoryClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        await Shell.Current.GoToAsync(nameof(ContactAuditLogPage), new Dictionary<string, object>
        {
            { "ContactId", _contact.Id.ToString() }
        });
    }

    // --- Profile Image ---

    private async void OnProfileImageTapped(object? sender, EventArgs e)
    {
        if (_contact == null) return;

        var action = await DisplayActionSheet("Profile Image", "Cancel", null,
            "Take Photo", "Choose from Gallery", "Remove Image");

        switch (action)
        {
            case "Take Photo":
                await CaptureAndUploadImageAsync(true);
                break;
            case "Choose from Gallery":
                await CaptureAndUploadImageAsync(false);
                break;
            case "Remove Image":
                var result = await _apiClient.DeleteContactProfileImageAsync(_contact.Id);
                if (result.Success)
                {
                    ProfileImage.IsVisible = false;
                    ProfileImage.Source = null;
                }
                break;
        }
    }

    private async Task CaptureAndUploadImageAsync(bool useCamera)
    {
        if (_contact == null) return;

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
                    Title = "Select Profile Image"
                });
            }

            if (photo == null) return;

            using var stream = await photo.OpenReadAsync();
            var result = await _apiClient.UploadContactProfileImageAsync(_contact.Id, stream, photo.FileName);
            if (result.Success)
            {
                await LoadContactAsync(); // Reload to get new image URL
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

    // --- Phone Actions ---

    private async void OnAddPhoneClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        var popup = new AddPhonePopup();
        var popupResult = await this.ShowPopupAsync<AddPhoneResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactPhoneAsync(_contact.Id, new AddPhoneRequest
        {
            PhoneNumber = result.PhoneNumber,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary
        });
        if (apiResult.Success) await LoadContactAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add phone", "OK");
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ContactPhoneNumberDto phone })
        {
            try { PhoneDialer.Default.Open(phone.PhoneNumber); }
            catch { await DisplayAlert("Error", "Cannot open phone dialer", "OK"); }
        }
    }

    private async void OnTextClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ContactPhoneNumberDto phone })
        {
            try { await Sms.Default.ComposeAsync(new SmsMessage("", new[] { phone.PhoneNumber })); }
            catch { await DisplayAlert("Error", "Cannot open messaging", "OK"); }
        }
    }

    private async void OnSetPrimaryPhoneSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactPhoneNumberDto phone } && _contact != null)
        {
            var result = await _apiClient.SetPrimaryPhoneAsync(_contact.Id, phone.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    private async void OnDeletePhoneSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactPhoneNumberDto phone })
        {
            var confirm = await DisplayAlert("Delete", $"Delete {phone.PhoneNumber}?", "Delete", "Cancel");
            if (!confirm) return;
            var result = await _apiClient.RemoveContactPhoneAsync(phone.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    // --- Email Actions ---

    private async void OnAddEmailClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        var popup = new AddEmailPopup();
        var popupResult = await this.ShowPopupAsync<AddEmailResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactEmailAsync(_contact.Id, new AddEmailRequest
        {
            Email = result.Email,
            Tag = result.Tag,
            IsPrimary = result.IsPrimary,
            Label = result.Label
        });
        if (apiResult.Success) await LoadContactAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add email", "OK");
    }

    private async void OnSendEmailClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ContactEmailAddressDto email })
        {
            try
            {
                var message = new EmailMessage { To = new List<string> { email.Email } };
                await Email.Default.ComposeAsync(message);
            }
            catch { await DisplayAlert("Error", "Cannot open email client", "OK"); }
        }
    }

    private async void OnSetPrimaryEmailSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactEmailAddressDto email } && _contact != null)
        {
            var result = await _apiClient.SetPrimaryEmailAsync(_contact.Id, email.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    private async void OnDeleteEmailSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactEmailAddressDto email })
        {
            var confirm = await DisplayAlert("Delete", $"Delete {email.Email}?", "Delete", "Cancel");
            if (!confirm) return;
            var result = await _apiClient.RemoveContactEmailAsync(email.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    // --- Address Actions ---

    private async void OnAddAddressClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        var popup = new AddAddressPopup(_apiClient);
        var popupResult = await this.ShowPopupAsync<AddAddressResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactAddressAsync(_contact.Id, new AddContactAddressRequest
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
        if (apiResult.Success) await LoadContactAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add address", "OK");
    }

    private async void OnAddressTapped(object? sender, EventArgs e)
    {
        if (sender is not Border { BindingContext: ContactAddressDto addr }) return;
        if (addr.Address.Latitude != null && addr.Address.Longitude != null)
        {
            await Map.Default.OpenAsync(addr.Address.Latitude.Value, addr.Address.Longitude.Value,
                new MapLaunchOptions { Name = addr.Address.DisplayAddress });
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
            }, new MapLaunchOptions());
        }
    }

    private async void OnSetPrimaryAddressSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactAddressDto addr } && _contact != null)
        {
            var result = await _apiClient.SetPrimaryAddressAsync(_contact.Id, addr.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    private async void OnDeleteAddressSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactAddressDto addr } && _contact != null)
        {
            var confirm = await DisplayAlert("Delete", "Delete this address?", "Delete", "Cancel");
            if (!confirm) return;
            var result = await _apiClient.RemoveContactAddressAsync(_contact.Id, addr.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    // --- Social Media Actions ---

    private async void OnAddSocialMediaClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        var popup = new AddSocialMediaPopup();
        var popupResult = await this.ShowPopupAsync<AddSocialMediaResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactSocialMediaAsync(_contact.Id, new AddSocialMediaRequest
        {
            Service = result.Service,
            Username = result.Username,
            ProfileUrl = result.ProfileUrl
        });
        if (apiResult.Success) await LoadContactAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add social media", "OK");
    }

    private async void OnOpenSocialClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ContactSocialMediaDto social } && !string.IsNullOrEmpty(social.ProfileUrl))
        {
            try { await Launcher.OpenAsync(new Uri(social.ProfileUrl)); }
            catch { /* ignore */ }
        }
    }

    private async void OnDeleteSocialSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactSocialMediaDto social })
        {
            var confirm = await DisplayAlert("Delete", $"Remove {social.ServiceLabel} profile?", "Delete", "Cancel");
            if (!confirm) return;
            var result = await _apiClient.RemoveContactSocialMediaAsync(social.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    // --- Relationship Actions ---

    private async void OnAddRelationshipClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        var popup = new AddRelationshipPopup(_apiClient);
        var popupResult = await this.ShowPopupAsync<AddRelationshipResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.AddContactRelationshipAsync(_contact.Id, new AddRelationshipRequest
        {
            TargetContactId = result.TargetContactId,
            RelationshipType = result.RelationshipType,
            CustomLabel = result.CustomLabel,
            CreateInverse = result.CreateInverse
        });
        if (apiResult.Success) await LoadContactAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add relationship", "OK");
    }

    private async void OnRelationshipContactTapped(object? sender, EventArgs e)
    {
        if (sender is Label { BindingContext: ContactRelationshipDto rel })
        {
            await Shell.Current.GoToAsync(nameof(ContactDetailPage), new Dictionary<string, object>
            {
                { "ContactId", rel.TargetContactId.ToString() }
            });
        }
    }

    private async void OnDeleteRelationshipSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactRelationshipDto rel })
        {
            var confirm = await DisplayAlert("Delete", "Remove this relationship?", "Delete", "Cancel");
            if (!confirm) return;
            var result = await _apiClient.RemoveContactRelationshipAsync(rel.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    // --- Sharing Actions ---

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (_contact == null) return;
        var popup = new ShareContactPopup(_apiClient);
        var popupResult = await this.ShowPopupAsync<ShareContactResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var apiResult = await _apiClient.ShareContactAsync(_contact.Id, new ShareContactRequest
        {
            SharedWithUserId = result.UserId,
            CanEdit = result.CanEdit
        });
        if (apiResult.Success) await LoadContactAsync();
        else await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to share contact", "OK");
    }

    private async void OnRemoveShareSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactUserShareDto share })
        {
            var confirm = await DisplayAlert("Remove", $"Stop sharing with {share.SharedWithUserName}?", "Remove", "Cancel");
            if (!confirm) return;
            var result = await _apiClient.RemoveContactShareAsync(share.Id);
            if (result.Success) await LoadContactAsync();
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadContactAsync();
    }
}
