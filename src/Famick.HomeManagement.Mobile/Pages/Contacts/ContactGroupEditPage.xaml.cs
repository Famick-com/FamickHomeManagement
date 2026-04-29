using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Syncfusion.Maui.Core;
using SfSelectionChangedEventArgs = Syncfusion.Maui.Core.Chips.SelectionChangedEventArgs;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(GroupId), "GroupId")]
public partial class ContactGroupEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly ContactGroupEditFormModel _model = new();
    private string _groupId = string.Empty;
    private ContactDetailDto? _existingGroup;
    private List<ContactTagDto> _allTags = new();
    private readonly HashSet<Guid> _selectedTagIds = new();
    private bool _isEditMode;
    private Guid? _existingPhoneId;

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

        ContactForm.DataObject = _model;
        PrimaryPhoneEditor.BindingContext = _model;
        MemberPhoneEditor.BindingContext = _model;
        TypeChipGroup.SelectedItem = TypeChipGroup.Items[0];

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
                var typeIndex = _existingGroup.ContactType ?? 0;
                _model.ContactType = typeIndex;
                TypeChipGroup.SelectedItem = TypeChipGroup.Items[typeIndex];

                _model.GroupName = _existingGroup.DisplayName ?? _existingGroup.FullName;
                _model.Website = _existingGroup.Website;
                _model.BusinessCategory = _existingGroup.BusinessCategory;
                _model.Notes = _existingGroup.Notes;

                if (_existingGroup.PhoneNumbers.Count > 0)
                {
                    var primary = _existingGroup.PhoneNumbers.FirstOrDefault(p => p.IsPrimary)
                        ?? _existingGroup.PhoneNumbers[0];
                    _model.PhoneNumber = primary.PhoneNumber;
                    _existingPhoneId = primary.Id;
                }

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

    private void OnTypeChipChanged(object? sender, SfSelectionChangedEventArgs e)
    {
        if (TypeChipGroup.SelectedItem is not SfChip selected) return;
        var index = TypeChipGroup.Items.IndexOf(selected);
        if (index < 0) return;
        _model.ContactType = index;
        UpdateTitleAndLabels();
    }

    private void UpdateTitleAndLabels()
    {
        var isBusiness = _model.ContactType == 1;
        var typeLabel = isBusiness ? "Business" : "Household";

        Title = _isEditMode ? $"Edit {typeLabel}" : $"New {typeLabel}";
        NameItem.LabelText = isBusiness ? "Business Name" : "Household Name";
        NameItem.PlaceholderText = isBusiness ? "e.g. Acme Plumbing" : "e.g. Smith Family";

        WebsiteItem.IsVisible = isBusiness;
        CategoryItem.IsVisible = isBusiness;

        if (_isEditMode)
        {
            if (ContactForm.Items.Contains(AddressGroup))
                ContactForm.Items.Remove(AddressGroup);
            if (ContactForm.Items.Contains(MemberGroup))
                ContactForm.Items.Remove(MemberGroup);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = _model.GroupName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            var typeLabel = _model.ContactType == 1 ? "Business" : "Household";
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
                    ContactType = _model.ContactType,
                    GroupName = name,
                    Notes = _model.Notes,
                    Website = _model.Website,
                    BusinessCategory = _model.BusinessCategory,
                    IsActive = true
                };
                var result = await _apiClient.UpdateContactGroupAsync(_existingGroup.Id, request);
                if (result.Success)
                {
                    var isBusiness = _model.ContactType == 1;
                    var phoneTag = isBusiness ? 2 : 1; // Work : Home
                    var hasPhone = !string.IsNullOrWhiteSpace(_model.PhoneNumber);

                    // Groups are constrained to a single phone — purge any extras
                    foreach (var extra in _existingGroup.PhoneNumbers.Where(p => p.Id != _existingPhoneId).ToList())
                        await _apiClient.RemoveContactPhoneAsync(extra.Id);

                    if (_existingPhoneId.HasValue && hasPhone)
                    {
                        await _apiClient.UpdateContactPhoneAsync(_existingPhoneId.Value, new AddPhoneRequest
                        {
                            PhoneNumber = _model.PhoneNumber!.Trim(),
                            Tag = phoneTag,
                            IsPrimary = true
                        });
                    }
                    else if (!_existingPhoneId.HasValue && hasPhone)
                    {
                        await _apiClient.AddContactPhoneAsync(_existingGroup.Id, new AddPhoneRequest
                        {
                            PhoneNumber = _model.PhoneNumber!.Trim(),
                            Tag = phoneTag,
                            IsPrimary = true
                        });
                    }
                    else if (_existingPhoneId.HasValue && !hasPhone)
                    {
                        await _apiClient.RemoveContactPhoneAsync(_existingPhoneId.Value);
                    }

                    var existingTagIds = _existingGroup.Tags.Select(t => t.Id).ToHashSet();
                    foreach (var tagId in _selectedTagIds.Except(existingTagIds))
                        await _apiClient.AddTagToContactAsync(_existingGroup.Id, tagId);
                    foreach (var tagId in existingTagIds.Except(_selectedTagIds))
                        await _apiClient.RemoveTagFromContactAsync(_existingGroup.Id, tagId);

                    _ = SyncContactToDeviceAsync(_existingGroup.Id);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update group", "OK");
                }
            }
            else
            {
                var isBusiness = _model.ContactType == 1;
                var request = new CreateContactGroupRequest
                {
                    ContactType = _model.ContactType,
                    GroupName = name,
                    Notes = _model.Notes,
                    Website = _model.Website,
                    BusinessCategory = _model.BusinessCategory,
                    TagIds = _selectedTagIds.Count > 0 ? _selectedTagIds.ToList() : null
                };
                var result = await _apiClient.CreateContactGroupAsync(request);
                if (!result.Success || result.Data == null)
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create group", "OK");
                    return;
                }

                var groupId = result.Data.Id;

                var resolvedAddress = await AddressAutocomplete.CommitAsync();
                if (resolvedAddress != null)
                {
                    await _apiClient.AddContactAddressAsync(groupId, new AddContactAddressRequest
                    {
                        AddressId = resolvedAddress.Id,
                        Tag = isBusiness ? 1 : 0,
                        IsPrimary = true
                    });
                }

                if (!string.IsNullOrWhiteSpace(_model.PhoneNumber))
                {
                    await _apiClient.AddContactPhoneAsync(groupId, new AddPhoneRequest
                    {
                        PhoneNumber = _model.PhoneNumber.Trim(),
                        Tag = isBusiness ? 2 : 1,
                        IsPrimary = true
                    });
                }

                var hasFirstMember = !string.IsNullOrWhiteSpace(_model.MemberFirstName)
                    || !string.IsNullOrWhiteSpace(_model.MemberLastName);
                if (hasFirstMember)
                {
                    var memberResult = await _apiClient.CreateContactAsync(new CreateContactRequest
                    {
                        FirstName = _model.MemberFirstName?.Trim(),
                        LastName = _model.MemberLastName?.Trim(),
                        ParentContactId = groupId
                    });

                    if (memberResult.Success && memberResult.Data != null)
                    {
                        var memberId = memberResult.Data.Id;

                        if (!string.IsNullOrWhiteSpace(_model.MemberEmail))
                        {
                            await _apiClient.AddContactEmailAsync(memberId, new AddEmailRequest
                            {
                                Email = _model.MemberEmail.Trim(),
                                Tag = 0,
                                IsPrimary = true
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(_model.MemberPhone))
                        {
                            await _apiClient.AddContactPhoneAsync(memberId, new AddPhoneRequest
                            {
                                PhoneNumber = _model.MemberPhone.Trim(),
                                Tag = 0,
                                IsPrimary = true
                            });
                        }
                    }
                }

                _ = SyncContactToDeviceAsync(groupId);
                await Shell.Current.GoToAsync("..");
            }
        }
        finally
        {
            SavingIndicator.IsVisible = false;
            SavingIndicator.IsRunning = false;
        }
    }

    private async Task SyncContactToDeviceAsync(Guid contactId)
    {
        try
        {
            var orchestrator = Handler?.MauiContext?.Services.GetService<ContactSyncOrchestrator>();
            if (orchestrator != null)
                await orchestrator.SyncSingleContactAsync(contactId);
        }
        catch { /* Non-critical — server push provides fallback */ }
    }
}
