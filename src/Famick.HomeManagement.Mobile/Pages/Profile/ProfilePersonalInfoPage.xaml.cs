using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Services;
using static Famick.HomeManagement.Mobile.Pages.Profile.ProfileUiHelpers;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfilePersonalInfoPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private UserProfileMobile? _profile;
    private Entry? _firstNameEntry;
    private Entry? _lastNameEntry;
    private Picker? _languagePicker;
    private Image? _profileImage;
    private bool _needsReload;

    private static readonly List<(string Code, string Name)> SupportedLanguages =
    [
        ("en", "English"),
        ("de", "Deutsch"),
        ("fr", "Francais"),
        ("es", "Espanol"),
        ("pt", "Portugues"),
        ("it", "Italiano"),
        ("nl", "Nederlands"),
        ("pl", "Polski"),
        ("da", "Dansk"),
        ("sv", "Svenska"),
        ("no", "Norsk"),
        ("fi", "Suomi")
    ];

    public ProfilePersonalInfoPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private async Task LoadProfileAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var result = await _apiClient.GetProfileAsync();
            if (result.Success && result.Data != null)
            {
                _profile = result.Data;
                RenderPersonalInfo();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to load profile", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load profile: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void RenderPersonalInfo()
    {
        PersonalInfoStack.Children.Clear();

        if (_profile == null) return;

        // Profile picture section
        PersonalInfoStack.Children.Add(CreateProfilePictureCard());

        // Email (read-only)
        PersonalInfoStack.Children.Add(CreateCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                CreateLabel("Email", true),
                CreateLabel(_profile.Email)
            }
        }));

        // Editable fields card
        _firstNameEntry = new Entry
        {
            Text = _profile.FirstName,
            Placeholder = "First Name",
            TextColor = GetTextColor(),
            PlaceholderColor = GetSecondaryTextColor()
        };

        _lastNameEntry = new Entry
        {
            Text = _profile.LastName,
            Placeholder = "Last Name",
            TextColor = GetTextColor(),
            PlaceholderColor = GetSecondaryTextColor()
        };

        _languagePicker = new Picker
        {
            Title = "Language",
            TextColor = GetTextColor(),
            TitleColor = GetSecondaryTextColor()
        };
        foreach (var lang in SupportedLanguages)
            _languagePicker.Items.Add(lang.Name);

        var selectedLangIndex = SupportedLanguages.FindIndex(l => l.Code == (_profile.PreferredLanguage ?? "en"));
        _languagePicker.SelectedIndex = selectedLangIndex >= 0 ? selectedLangIndex : 0;

        var saveButton = new Button
        {
            Text = "Save Profile",
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
            Margin = new Thickness(0, 5, 0, 0)
        };
        saveButton.Clicked += OnSaveProfileClicked;

        PersonalInfoStack.Children.Add(CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                CreateLabel("First Name", true),
                _firstNameEntry,
                CreateLabel("Last Name", true),
                _lastNameEntry,
                CreateLabel("Language", true),
                _languagePicker,
                saveButton
            }
        }));

        // Contact info
        if (_profile.Contact != null && _profile.ContactId != null)
        {
            var contactChildren = new VerticalStackLayout { Spacing = 8 };

            var headerRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }};
            headerRow.Add(CreateLabel("Contact Information", true, 16), 0);

            var editButton = new Button
            {
                Text = "Edit",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#1976D2"),
                FontSize = 14,
                Padding = new Thickness(0),
                VerticalOptions = LayoutOptions.Center
            };
            editButton.Clicked += OnEditContactClicked;
            headerRow.Add(editButton, 1);
            contactChildren.Children.Add(headerRow);

            if (_profile.Contact.PhoneNumbers.Count > 0)
            {
                contactChildren.Children.Add(CreateLabel("Phone Numbers", true));
                foreach (var phone in _profile.Contact.PhoneNumbers)
                {
                    var label = string.IsNullOrEmpty(phone.Label) ? "" : $" ({phone.Label})";
                    var primary = phone.IsPrimary ? " *" : "";
                    contactChildren.Children.Add(CreateLabel($"{phone.PhoneNumber}{label}{primary}"));
                }
            }

            if (_profile.Contact.EmailAddresses.Count > 0)
            {
                contactChildren.Children.Add(CreateLabel("Email Addresses", true));
                foreach (var email in _profile.Contact.EmailAddresses)
                {
                    var label = string.IsNullOrEmpty(email.Label) ? "" : $" ({email.Label})";
                    var primary = email.IsPrimary ? " *" : "";
                    contactChildren.Children.Add(CreateLabel($"{email.Email}{label}{primary}"));
                }
            }

            PersonalInfoStack.Children.Add(CreateCard(contactChildren));
        }
    }

    private async void OnEditContactClicked(object? sender, EventArgs e)
    {
        if (_profile?.ContactId == null) return;

        _needsReload = true;
        await Shell.Current.GoToAsync(nameof(ContactEditPage), new Dictionary<string, object>
        {
            { "ContactId", _profile.ContactId.Value.ToString() }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // On first load, _profile is null; on return from edit, reload to pick up changes
        if (_profile == null)
            await LoadProfileAsync();
        else if (_needsReload)
        {
            _needsReload = false;
            _profile = null;
            await LoadProfileAsync();
        }
    }

    private View CreateProfilePictureCard()
    {
        _profileImage = new Image
        {
            HeightRequest = 100,
            WidthRequest = 100,
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Center
        };

        // Apply circular clip
        _profileImage.Clip = new Microsoft.Maui.Controls.Shapes.EllipseGeometry
        {
            Center = new Point(50, 50),
            RadiusX = 50,
            RadiusY = 50
        };

        UpdateProfileImageSource();

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnProfileImageTapped;
        _profileImage.GestureRecognizers.Add(tapGesture);

        var changeLabel = new Label
        {
            Text = "Tap to change photo",
            FontSize = 12,
            TextColor = Color.FromArgb("#1976D2"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
        changeLabel.GestureRecognizers.Add(tapGesture);

        var stack = new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 0,
            Children = { _profileImage, changeLabel }
        };

        // Add remove button if user has a custom image
        if (_profile?.Contact != null && !string.IsNullOrEmpty(_profile.Contact.ProfileImageUrl))
        {
            var removeButton = new Button
            {
                Text = "Remove Photo",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#D32F2F"),
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Center,
                Padding = new Thickness(0)
            };
            removeButton.Clicked += OnRemoveProfileImageClicked;
            stack.Children.Add(removeButton);
        }

        return CreateCard(stack);
    }

    private async void UpdateProfileImageSource()
    {
        if (_profileImage == null || _profile == null) return;

        // If user has a profile image, load via the authenticated profile API
        if (_profile.Contact != null && !string.IsNullOrEmpty(_profile.Contact.ProfileImageUrl))
        {
            try
            {
                var imageStream = await _apiClient.GetProfileImageStreamAsync();
                if (imageStream != null)
                {
                    _profileImage.Source = ImageSource.FromStream(() => imageStream);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProfilePersonalInfo] Failed to load profile image: {ex.Message}");
            }
        }

        // Fall back to Gravatar
        if (_profile.Contact is { UseGravatar: true } && !string.IsNullOrEmpty(_profile.Contact.GravatarUrl))
        {
            _profileImage.Source = new UriImageSource
            {
                Uri = new Uri(_profile.Contact.GravatarUrl),
                CachingEnabled = false
            };
            return;
        }

        // Default placeholder
        _profileImage.Source = "tab_person";
    }

    private async void OnProfileImageTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select Profile Photo"
            });

            if (result == null) return;

            // Open crop page
            using var imageStream = await result.OpenReadAsync();
            var cropPage = new ProfileImageCropPage(imageStream);
            await Navigation.PushModalAsync(new NavigationPage(cropPage));

            var croppedStream = await cropPage.CropResultTask;
            if (croppedStream == null) return;

            await UploadImageAsync(croppedStream);
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permission Required", "Photo library access is needed to select a profile picture.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to pick photo: {ex.Message}", "OK");
        }
    }

    private async Task UploadImageAsync(Stream imageStream)
    {
        try
        {
            var uploadResult = await _apiClient.UploadProfileImageAsync(imageStream, "profile.png");

            if (uploadResult.Success)
            {
                // Reload profile to get updated image URL
                var profileResult = await _apiClient.GetProfileAsync();
                if (profileResult.Success && profileResult.Data != null)
                {
                    _profile = profileResult.Data;
                    RenderPersonalInfo();
                }
            }
            else
            {
                await DisplayAlert("Error", uploadResult.ErrorMessage ?? "Failed to upload image", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to upload image: {ex.Message}", "OK");
        }
    }

    private async void OnRemoveProfileImageClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Remove Photo", "Are you sure you want to remove your profile photo?", "Remove", "Cancel");
        if (!confirm) return;

        try
        {
            var result = await _apiClient.DeleteProfileImageAsync();
            if (result.Success)
            {
                // Reload profile to reflect removal
                var profileResult = await _apiClient.GetProfileAsync();
                if (profileResult.Success && profileResult.Data != null)
                {
                    _profile = profileResult.Data;
                    RenderPersonalInfo();
                }
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to remove image", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to remove image: {ex.Message}", "OK");
        }
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        if (_firstNameEntry == null || _lastNameEntry == null || _languagePicker == null) return;

        var selectedLangCode = _languagePicker.SelectedIndex >= 0
            ? SupportedLanguages[_languagePicker.SelectedIndex].Code
            : "en";

        var request = new UpdateProfileMobileRequest
        {
            FirstName = _firstNameEntry.Text ?? "",
            LastName = _lastNameEntry.Text ?? "",
            PreferredLanguage = selectedLangCode
        };

        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Saving...";
        }

        try
        {
            var result = await _apiClient.UpdateProfileAsync(request);
            if (result.Success)
                await DisplayAlert("Success", "Profile updated successfully", "OK");
            else
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update profile", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
        finally
        {
            if (sender is Button btn2)
            {
                btn2.IsEnabled = true;
                btn2.Text = "Save Profile";
            }
        }
    }
}
