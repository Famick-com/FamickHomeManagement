using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

[QueryProperty(nameof(ContactId), "ContactId")]
[QueryProperty(nameof(ParentGroupId), "ParentGroupId")]
public partial class ContactEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private string _contactId = string.Empty;
    private string _parentGroupId = string.Empty;
    private ContactDetailDto? _existing;
    private List<ContactGroupSummaryDto> _groups = new();
    private List<ContactTagDto> _allTags = new();
    private readonly HashSet<Guid> _selectedTagIds = new();
    private bool _isEditMode;

    public string ContactId
    {
        get => _contactId;
        set
        {
            _contactId = value;
            _isEditMode = !string.IsNullOrEmpty(value) && Guid.TryParse(value, out _);
            Title = _isEditMode ? "Edit Contact" : "New Contact";
            if (_isEditMode) _ = LoadExistingContactAsync();
        }
    }

    public string ParentGroupId
    {
        get => _parentGroupId;
        set
        {
            _parentGroupId = value;
            SelectGroupInPicker();
        }
    }

    public ContactEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        GenderPicker.SelectedIndex = 0;
        VisibilityPicker.SelectedIndex = 0;
        _ = LoadGroupsAndTagsAsync();
    }

    private async Task LoadGroupsAndTagsAsync()
    {
        var groupsTask = _apiClient.GetContactGroupsAsync(pageSize: 100);
        var tagsTask = _apiClient.GetContactTagsAsync();

        await Task.WhenAll(groupsTask, tagsTask);

        var groupsResult = groupsTask.Result;
        var tagsResult = tagsTask.Result;

        if (groupsResult.Success && groupsResult.Data != null)
        {
            _groups = groupsResult.Data.Items;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GroupPicker.Items.Clear();
                GroupPicker.Items.Add("(None)");
                foreach (var g in _groups)
                    GroupPicker.Items.Add(g.GroupName);
                GroupPicker.SelectedIndex = 0;
                SelectGroupInPicker();
            });
        }

        if (tagsResult.Success && tagsResult.Data != null)
        {
            _allTags = tagsResult.Data;
            MainThread.BeginInvokeOnMainThread(UpdateTagChips);
        }
    }

    private void SelectGroupInPicker()
    {
        if (_groups.Count == 0 || !Guid.TryParse(_parentGroupId, out var gid)) return;
        var idx = _groups.FindIndex(g => g.Id == gid);
        if (idx >= 0)
            MainThread.BeginInvokeOnMainThread(() => GroupPicker.SelectedIndex = idx + 1);
    }

    private async Task LoadExistingContactAsync()
    {
        if (!Guid.TryParse(_contactId, out var id)) return;

        var result = await _apiClient.GetContactAsync(id);
        if (result.Success && result.Data != null)
        {
            _existing = result.Data;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FirstNameEntry.Text = _existing.FirstName;
                MiddleNameEntry.Text = _existing.MiddleName;
                LastNameEntry.Text = _existing.LastName;
                PreferredNameEntry.Text = _existing.PreferredName;
                CompanyEntry.Text = _existing.CompanyName;
                TitleEntry.Text = _existing.Title;
                GenderPicker.SelectedIndex = _existing.Gender;
                BirthYearEntry.Text = _existing.BirthYear?.ToString();
                BirthMonthEntry.Text = _existing.BirthMonth?.ToString();
                BirthDayEntry.Text = _existing.BirthDay?.ToString();
                NotesEditor.Text = _existing.Notes;
                VisibilityPicker.SelectedIndex = _existing.Visibility;
                GravatarSwitch.IsToggled = _existing.UseGravatar;

                _selectedTagIds.Clear();
                foreach (var tag in _existing.Tags)
                    _selectedTagIds.Add(tag.Id);

                UpdateTagChips();

                // Select group
                if (_existing.ParentContactId.HasValue && _groups.Count > 0)
                {
                    var idx = _groups.FindIndex(g => g.Id == _existing.ParentContactId.Value);
                    if (idx >= 0) GroupPicker.SelectedIndex = idx + 1;
                }
            });
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

    private int ComputeBirthDatePrecision()
    {
        var hasYear = int.TryParse(BirthYearEntry.Text, out _);
        var hasMonth = int.TryParse(BirthMonthEntry.Text, out _);
        var hasDay = int.TryParse(BirthDayEntry.Text, out _);

        if (hasYear && hasMonth && hasDay) return 3; // Full
        if (hasYear && hasMonth) return 2; // YearMonth
        if (hasYear) return 1; // Year
        return 0; // Unknown
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var first = FirstNameEntry.Text?.Trim();
        var last = LastNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last))
        {
            await DisplayAlert("Validation", "First name or last name is required.", "OK");
            return;
        }

        SavingIndicator.IsVisible = true;
        SavingIndicator.IsRunning = true;

        try
        {
            Guid? parentGroupId = null;
            if (GroupPicker.SelectedIndex > 0 && GroupPicker.SelectedIndex <= _groups.Count)
                parentGroupId = _groups[GroupPicker.SelectedIndex - 1].Id;

            int.TryParse(BirthYearEntry.Text, out var birthYear);
            int.TryParse(BirthMonthEntry.Text, out var birthMonth);
            int.TryParse(BirthDayEntry.Text, out var birthDay);

            if (_isEditMode && _existing != null)
            {
                var request = new UpdateContactRequest
                {
                    FirstName = first,
                    MiddleName = MiddleNameEntry.Text?.Trim(),
                    LastName = last,
                    PreferredName = PreferredNameEntry.Text?.Trim(),
                    CompanyName = CompanyEntry.Text?.Trim(),
                    Title = TitleEntry.Text?.Trim(),
                    Gender = GenderPicker.SelectedIndex,
                    BirthYear = birthYear > 0 ? birthYear : null,
                    BirthMonth = birthMonth > 0 ? birthMonth : null,
                    BirthDay = birthDay > 0 ? birthDay : null,
                    BirthDatePrecision = ComputeBirthDatePrecision(),
                    Notes = NotesEditor.Text,
                    Visibility = VisibilityPicker.SelectedIndex,
                    UseGravatar = GravatarSwitch.IsToggled
                };
                var result = await _apiClient.UpdateContactAsync(_existing.Id, request);
                if (result.Success)
                {
                    // Move group if changed
                    if (parentGroupId != _existing.ParentContactId && parentGroupId.HasValue)
                        await _apiClient.MoveContactToGroupAsync(_existing.Id, parentGroupId.Value);

                    // Update tags
                    var existingTagIds = _existing.Tags.Select(t => t.Id).ToHashSet();
                    foreach (var tagId in _selectedTagIds.Except(existingTagIds))
                        await _apiClient.AddTagToContactAsync(_existing.Id, tagId);
                    foreach (var tagId in existingTagIds.Except(_selectedTagIds))
                        await _apiClient.RemoveTagFromContactAsync(_existing.Id, tagId);

                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update contact", "OK");
                }
            }
            else
            {
                var request = new CreateContactRequest
                {
                    FirstName = first,
                    MiddleName = MiddleNameEntry.Text?.Trim(),
                    LastName = last,
                    PreferredName = PreferredNameEntry.Text?.Trim(),
                    CompanyName = CompanyEntry.Text?.Trim(),
                    Title = TitleEntry.Text?.Trim(),
                    Gender = GenderPicker.SelectedIndex,
                    BirthYear = birthYear > 0 ? birthYear : null,
                    BirthMonth = birthMonth > 0 ? birthMonth : null,
                    BirthDay = birthDay > 0 ? birthDay : null,
                    BirthDatePrecision = ComputeBirthDatePrecision(),
                    Notes = NotesEditor.Text,
                    Visibility = VisibilityPicker.SelectedIndex,
                    TagIds = _selectedTagIds.Count > 0 ? _selectedTagIds.ToList() : null,
                    UseGravatar = GravatarSwitch.IsToggled,
                    ParentContactId = parentGroupId
                };
                var result = await _apiClient.CreateContactAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create contact", "OK");
                }
            }
        }
        finally
        {
            SavingIndicator.IsVisible = false;
            SavingIndicator.IsRunning = false;
        }
    }
}
