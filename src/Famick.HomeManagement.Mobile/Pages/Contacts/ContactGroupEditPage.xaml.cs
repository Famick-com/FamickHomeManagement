using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(GroupId), "GroupId")]
public partial class ContactGroupEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private string _groupId = string.Empty;
    private ContactDetailDto? _existingGroup;
    private List<ContactTagDto> _allTags = new();
    private readonly HashSet<Guid> _selectedTagIds = new();
    private bool _isEditMode;
    private CancellationTokenSource? _addressSearchCts;
    private Guid? _selectedAddressId;
    private bool _suppressAddressSearch;

    public string GroupId
    {
        get => _groupId;
        set
        {
            _groupId = value;
            _isEditMode = !string.IsNullOrEmpty(value) && Guid.TryParse(value, out _);
            UpdateTitleAndLabels();
            if (_isEditMode) _ = LoadExistingGroupAsync();
        }
    }

    public ContactGroupEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        TypePicker.SelectedIndex = 0;
        _ = LoadTagsAsync();
    }

    private async Task LoadExistingGroupAsync()
    {
        if (!Guid.TryParse(_groupId, out var id)) return;

        var result = await _apiClient.GetContactGroupAsync(id);
        if (result.Success && result.Data != null)
        {
            _existingGroup = result.Data;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TypePicker.SelectedIndex = _existingGroup.ContactType ?? 0;
                GroupNameEntry.Text = _existingGroup.DisplayName ?? _existingGroup.FullName;
                WebsiteEntry.Text = _existingGroup.Website;
                CategoryEntry.Text = _existingGroup.BusinessCategory;
                NotesEditor.Text = _existingGroup.Notes;

                _selectedTagIds.Clear();
                foreach (var tag in _existingGroup.Tags)
                    _selectedTagIds.Add(tag.Id);

                UpdateTagChips();
            });
        }
    }

    private async Task LoadTagsAsync()
    {
        var result = await _apiClient.GetContactTagsAsync();
        if (result.Success && result.Data != null)
        {
            _allTags = result.Data;
            MainThread.BeginInvokeOnMainThread(UpdateTagChips);
        }
    }

    private void UpdateTagChips()
    {
        TagsLayout.Children.Clear();
        foreach (var tag in _allTags)
        {
            var isSelected = _selectedTagIds.Contains(tag.Id);
            var tagColor = !string.IsNullOrEmpty(tag.Color) ? Color.FromArgb(tag.Color) : Color.FromArgb("#9E9E9E");

            var chip = new Border
            {
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Stroke = tagColor,
                StrokeThickness = isSelected ? 0 : 1,
                BackgroundColor = isSelected ? tagColor : Colors.Transparent,
                Content = new Label
                {
                    Text = tag.Name,
                    FontSize = 13,
                    TextColor = isSelected ? Colors.White : tagColor
                }
            };
            chip.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    if (_selectedTagIds.Contains(tag.Id))
                        _selectedTagIds.Remove(tag.Id);
                    else
                        _selectedTagIds.Add(tag.Id);
                    UpdateTagChips();
                })
            });
            TagsLayout.Children.Add(chip);
        }
    }

    private void OnTypeChanged(object? sender, EventArgs e)
    {
        var isBusiness = TypePicker.SelectedIndex == 1;
        BusinessFields.IsVisible = isBusiness;
        PhoneField.IsVisible = isBusiness && !_isEditMode;
        UpdateTitleAndLabels();
    }

    private void UpdateTitleAndLabels()
    {
        var isBusiness = TypePicker.SelectedIndex == 1;
        var typeLabel = isBusiness ? "Business" : "Household";

        Title = _isEditMode ? $"Edit {typeLabel}" : $"New {typeLabel}";
        NameFieldLabel.Text = isBusiness ? "Business Name *" : "Household Name *";

        // Show address and first member sections only in create mode
        AddressSection.IsVisible = !_isEditMode;
        MemberSection.IsVisible = !_isEditMode;
        PhoneField.IsVisible = isBusiness && !_isEditMode;
    }

    private async void OnAddressLine1TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressAddressSearch) return;

        // If user is typing after selecting an address, clear the selection
        if (_selectedAddressId.HasValue)
        {
            _selectedAddressId = null;
            SelectedAddressLabel.IsVisible = false;
            SetAddressFieldsReadOnly(false);
            ClearAddressFieldsExceptLine1();
        }

        _addressSearchCts?.Cancel();

        var query = e.NewTextValue?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            AddressSearchResultsView.IsVisible = false;
            AddressSearchResultsView.ItemsSource = null;
            return;
        }

        _addressSearchCts = new CancellationTokenSource();
        var token = _addressSearchCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            var result = await _apiClient.SearchAddressesAsync(query, 10);
            if (token.IsCancellationRequested) return;

            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                AddressSearchResultsView.ItemsSource = result.Data;
                AddressSearchResultsView.IsVisible = true;
            }
            else
            {
                AddressSearchResultsView.IsVisible = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled, expected
        }
    }

    private void OnAddressSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AddressDto address) return;

        _suppressAddressSearch = true;
        _selectedAddressId = address.Id;

        AddressLine1Entry.Text = address.AddressLine1;
        AddressLine2Entry.Text = address.AddressLine2;
        CityEntry.Text = address.City;
        StateEntry.Text = address.StateProvince;
        PostalEntry.Text = address.PostalCode;
        CountryEntry.Text = address.Country;

        _suppressAddressSearch = false;

        SelectedAddressLabel.IsVisible = true;
        AddressSearchResultsView.IsVisible = false;

        SetAddressFieldsReadOnly(true);
        AddressLine1Entry.IsReadOnly = true;
    }

    private void ClearAddressFieldsExceptLine1()
    {
        _suppressAddressSearch = true;
        AddressLine2Entry.Text = string.Empty;
        CityEntry.Text = string.Empty;
        StateEntry.Text = string.Empty;
        PostalEntry.Text = string.Empty;
        CountryEntry.Text = string.Empty;
        _suppressAddressSearch = false;
    }

    private void SetAddressFieldsReadOnly(bool readOnly)
    {
        AddressLine2Entry.IsReadOnly = readOnly;
        CityEntry.IsReadOnly = readOnly;
        StateEntry.IsReadOnly = readOnly;
        PostalEntry.IsReadOnly = readOnly;
        CountryEntry.IsReadOnly = readOnly;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = GroupNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            var typeLabel = TypePicker.SelectedIndex == 1 ? "Business" : "Household";
            await DisplayAlert("Validation", $"{typeLabel} name is required.", "OK");
            return;
        }

        SavingIndicator.IsVisible = true;
        SavingIndicator.IsRunning = true;

        try
        {
            if (_isEditMode && _existingGroup != null)
            {
                var request = new UpdateContactGroupRequest
                {
                    ContactType = TypePicker.SelectedIndex,
                    GroupName = name,
                    Notes = NotesEditor.Text,
                    Website = WebsiteEntry.Text,
                    BusinessCategory = CategoryEntry.Text,
                    IsActive = true
                };
                var result = await _apiClient.UpdateContactGroupAsync(_existingGroup.Id, request);
                if (result.Success)
                {
                    // Update tags
                    var existingTagIds = _existingGroup.Tags.Select(t => t.Id).ToHashSet();
                    foreach (var tagId in _selectedTagIds.Except(existingTagIds))
                        await _apiClient.AddTagToContactAsync(_existingGroup.Id, tagId);
                    foreach (var tagId in existingTagIds.Except(_selectedTagIds))
                        await _apiClient.RemoveTagFromContactAsync(_existingGroup.Id, tagId);

                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update group", "OK");
                }
            }
            else
            {
                // 1. Create the group
                var isBusiness = TypePicker.SelectedIndex == 1;
                var request = new CreateContactGroupRequest
                {
                    ContactType = TypePicker.SelectedIndex,
                    GroupName = name,
                    Notes = NotesEditor.Text,
                    Website = WebsiteEntry.Text,
                    BusinessCategory = CategoryEntry.Text,
                    TagIds = _selectedTagIds.Count > 0 ? _selectedTagIds.ToList() : null
                };
                var result = await _apiClient.CreateContactGroupAsync(request);
                if (!result.Success || result.Data == null)
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create group", "OK");
                    return;
                }

                var groupId = result.Data.Id;

                // 2. Add address if provided
                var hasAddress = !string.IsNullOrWhiteSpace(AddressLine1Entry.Text)
                    || !string.IsNullOrWhiteSpace(CityEntry.Text);
                if (hasAddress)
                {
                    await _apiClient.AddContactAddressAsync(groupId, new AddContactAddressRequest
                    {
                        AddressId = _selectedAddressId,
                        AddressLine1 = AddressLine1Entry.Text?.Trim(),
                        AddressLine2 = AddressLine2Entry.Text?.Trim(),
                        City = CityEntry.Text?.Trim(),
                        StateProvince = StateEntry.Text?.Trim(),
                        PostalCode = PostalEntry.Text?.Trim(),
                        Country = CountryEntry.Text?.Trim(),
                        Tag = isBusiness ? 1 : 0, // Work : Home
                        IsPrimary = true
                    });
                }

                // 3. Add business phone if provided
                if (isBusiness && !string.IsNullOrWhiteSpace(PhoneEntry.Text))
                {
                    await _apiClient.AddContactPhoneAsync(groupId, new AddPhoneRequest
                    {
                        PhoneNumber = PhoneEntry.Text.Trim(),
                        Tag = 1, // Work
                        IsPrimary = true
                    });
                }

                // 4. Create first member if provided
                var hasFirstMember = !string.IsNullOrWhiteSpace(MemberFirstNameEntry.Text)
                    || !string.IsNullOrWhiteSpace(MemberLastNameEntry.Text);
                if (hasFirstMember)
                {
                    var memberResult = await _apiClient.CreateContactAsync(new CreateContactRequest
                    {
                        FirstName = MemberFirstNameEntry.Text?.Trim(),
                        LastName = MemberLastNameEntry.Text?.Trim(),
                        ParentContactId = groupId
                    });

                    if (memberResult.Success && memberResult.Data != null)
                    {
                        var memberId = memberResult.Data.Id;

                        // Add member email if provided
                        if (!string.IsNullOrWhiteSpace(MemberEmailEntry.Text))
                        {
                            await _apiClient.AddContactEmailAsync(memberId, new AddEmailRequest
                            {
                                Email = MemberEmailEntry.Text.Trim(),
                                Tag = 0, // Personal
                                IsPrimary = true
                            });
                        }

                        // Add member phone if provided
                        if (!string.IsNullOrWhiteSpace(MemberPhoneEntry.Text))
                        {
                            await _apiClient.AddContactPhoneAsync(memberId, new AddPhoneRequest
                            {
                                PhoneNumber = MemberPhoneEntry.Text.Trim(),
                                Tag = 0, // Mobile
                                IsPrimary = true
                            });
                        }
                    }
                }

                await Shell.Current.GoToAsync("..");
            }
        }
        finally
        {
            SavingIndicator.IsVisible = false;
            SavingIndicator.IsRunning = false;
        }
    }
}
