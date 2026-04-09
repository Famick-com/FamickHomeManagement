using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

public partial class ImportContactPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private SharedContactData? _contactData;
    private HouseholdMatch? _householdMatch;
    private ContactMatch? _contactMatch;

    // Track checkbox references for addresses/phones/emails
    private readonly List<(CheckBox Checkbox, SharedAddressEntry Entry)> _addressChecks = new();
    private readonly List<(CheckBox Checkbox, SharedPhoneEntry Entry)> _phoneChecks = new();
    private readonly List<(CheckBox Checkbox, SharedEmailEntry Entry)> _emailChecks = new();

    public ImportContactPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // If there's new pending data, use it (new share). Otherwise keep existing (returning from sub-page).
        if (App.PendingSharedContact != null)
        {
            _contactData = App.PendingSharedContact;
            App.PendingSharedContact = null;
            _householdMatch = null;
            _contactMatch = null;
            ResetUI();
            PopulateFields();
            ShowContent();
            await RunMatchingAsync();
            return;
        }

        // Returning from sub-page with existing data -- do nothing
        if (_contactData != null)
            return;

        // No data at all -- navigated back after import completed
        await Shell.Current.GoToAsync("..");
    }

    private void ResetUI()
    {
        _addressChecks.Clear();
        _phoneChecks.Clear();
        _emailChecks.Clear();
        AddressCheckboxes.Children.Clear();
        PhoneCheckboxes.Children.Clear();
        EmailCheckboxes.Children.Clear();
        AddressesSection.IsVisible = false;
        PhonesSection.IsVisible = false;
        EmailsSection.IsVisible = false;
        CompanySection.IsVisible = false;
        BirthdaySection.IsVisible = false;
        NotesSection.IsVisible = false;
        SharedInfoBanner.IsVisible = false;
        ActionButtonsSection.IsVisible = false;
        ImportProgressSection.IsVisible = false;
        ErrorFrame.IsVisible = false;
        AddToHouseholdButton.IsVisible = false;
        AddToExistingHouseholdButton.IsVisible = false;
        UpdateMatchedContactButton.IsVisible = false;
    }

    private void PopulateFields()
    {
        if (_contactData == null) return;

        ContactNameLabel.Text = _contactData.DisplayName;

        // Show banner if name is missing (shared info only)
        if (string.IsNullOrWhiteSpace(_contactData.FirstName) && string.IsNullOrWhiteSpace(_contactData.LastName))
        {
            SharedInfoBanner.IsVisible = true;
        }

        if (_contactData.HasCompany)
        {
            CompanyLabel.Text = _contactData.CompanyName;
            CompanyLabel.IsVisible = true;
        }

        // Addresses
        if (_contactData.HasAddress)
        {
            AddressesSection.IsVisible = true;
            foreach (var addr in _contactData.Addresses)
            {
                var checkbox = new CheckBox
                {
                    IsChecked = true,
                    Color = Color.FromArgb("#1976D2")
                };
                var label = new Label
                {
                    Text = $"{addr.TagLabel}: {addr.DisplayAddress.Replace("\n", ", ")}",
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = 14
                };
                var validatedLabel = new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#43A047"),
                    IsVisible = false
                };

                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(checkbox);
                var stack = new VerticalStackLayout { Spacing = 2 };
                stack.Children.Add(label);
                stack.Children.Add(validatedLabel);
                row.Children.Add(stack);

                AddressCheckboxes.Children.Add(row);
                _addressChecks.Add((checkbox, addr));
            }
        }

        // Phones
        if (_contactData.HasPhone)
        {
            PhonesSection.IsVisible = true;
            foreach (var phone in _contactData.PhoneNumbers)
            {
                var checkbox = new CheckBox
                {
                    IsChecked = true,
                    Color = Color.FromArgb("#1976D2")
                };
                var label = new Label
                {
                    Text = $"{phone.TagLabel}: {phone.PhoneNumber}",
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = 14
                };
                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(checkbox);
                row.Children.Add(label);
                PhoneCheckboxes.Children.Add(row);
                _phoneChecks.Add((checkbox, phone));
            }
        }

        // Emails
        if (_contactData.HasEmail)
        {
            EmailsSection.IsVisible = true;
            foreach (var email in _contactData.EmailAddresses)
            {
                var checkbox = new CheckBox
                {
                    IsChecked = true,
                    Color = Color.FromArgb("#1976D2")
                };
                var label = new Label
                {
                    Text = $"{email.TagLabel}: {email.Email}",
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = 14
                };
                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(checkbox);
                row.Children.Add(label);
                EmailCheckboxes.Children.Add(row);
                _emailChecks.Add((checkbox, email));
            }
        }

        // Company/Title
        if (_contactData.HasCompany)
        {
            CompanySection.IsVisible = true;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_contactData.CompanyName)) parts.Add(_contactData.CompanyName);
            if (!string.IsNullOrWhiteSpace(_contactData.Title)) parts.Add(_contactData.Title);
            CompanyCheckboxLabel.Text = $"Company: {string.Join(" - ", parts)}";
        }

        // Birthday
        if (_contactData.HasBirthday)
        {
            BirthdaySection.IsVisible = true;
            var dateParts = new List<string>();
            if (_contactData.BirthMonth.HasValue) dateParts.Add(_contactData.BirthMonth.Value.ToString("D2"));
            if (_contactData.BirthDay.HasValue) dateParts.Add(_contactData.BirthDay.Value.ToString("D2"));
            if (_contactData.BirthYear.HasValue) dateParts.Add(_contactData.BirthYear.Value.ToString());
            BirthdayCheckboxLabel.Text = $"Birthday: {string.Join("/", dateParts)}";
        }

        // Notes
        if (_contactData.HasNotes)
        {
            NotesSection.IsVisible = true;
            var preview = _contactData.Notes!.Length > 50
                ? _contactData.Notes[..50] + "..."
                : _contactData.Notes;
            NotesCheckboxLabel.Text = $"Notes: {preview}";
        }
    }

    private async Task RunMatchingAsync()
    {
        MatchingIndicator.IsVisible = true;
        MatchingLabel.IsVisible = true;

        try
        {
            // Validate addresses and find matches in parallel
            var validateTask = ValidateAddressesAsync();
            var matchTask = FindMatchesAsync();
            await Task.WhenAll(validateTask, matchTask);
        }
        finally
        {
            MatchingIndicator.IsVisible = false;
            MatchingLabel.IsVisible = false;
            ShowActionButtons();
        }
    }

    private async Task ValidateAddressesAsync()
    {
        if (_contactData == null) return;

        var tasks = _contactData.Addresses.Select(async (addr, index) =>
        {
            var request = new NormalizeAddressRequest
            {
                AddressLine1 = addr.AddressLine1,
                AddressLine2 = addr.AddressLine2,
                City = addr.City,
                StateProvince = addr.StateProvince,
                PostalCode = addr.PostalCode,
                Country = addr.Country
            };

            var result = await _apiClient.NormalizeSuggestionsAsync(request, 1);
            if (result.Success && result.Data?.Count > 0)
            {
                addr.ValidatedAddress = result.Data[0];
                addr.IsValidated = true;

                // Update the UI on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (index < AddressCheckboxes.Children.Count &&
                        AddressCheckboxes.Children[index] is HorizontalStackLayout row &&
                        row.Children.Count > 1 &&
                        row.Children[1] is VerticalStackLayout stack &&
                        stack.Children.Count > 1 &&
                        stack.Children[1] is Label validatedLabel)
                    {
                        validatedLabel.Text = $"(suggested) {addr.ValidatedAddress.FormattedAddress}";
                        validatedLabel.IsVisible = true;
                    }
                });
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task FindMatchesAsync()
    {
        if (_contactData == null) return;

        // Fetch household groups
        var groupsResult = await _apiClient.GetContactGroupsAsync(contactType: 0, pageSize: 100);
        if (!groupsResult.Success || groupsResult.Data?.Items == null)
            return;

        var households = groupsResult.Data.Items;

        // Try to match by last name
        HouseholdMatch? bestMatch = null;
        if (!string.IsNullOrWhiteSpace(_contactData.LastName))
        {
            foreach (var group in households)
            {
                var nameMatch = group.GroupName.Contains(_contactData.LastName, StringComparison.OrdinalIgnoreCase);
                var addressMatch = false;

                // Compare addresses if group has a primary address
                if (!string.IsNullOrWhiteSpace(group.PrimaryAddress) && _contactData.HasAddress)
                {
                    foreach (var addr in _contactData.Addresses)
                    {
                        // Simple postal code + street comparison
                        var addrDisplay = addr.DisplayAddress.Replace("\n", " ").ToLowerInvariant();
                        var groupAddr = group.PrimaryAddress.ToLowerInvariant();

                        if (!string.IsNullOrWhiteSpace(addr.PostalCode) &&
                            groupAddr.Contains(addr.PostalCode.ToLowerInvariant()))
                        {
                            addressMatch = true;
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(addr.AddressLine1) &&
                            groupAddr.Contains(addr.AddressLine1.ToLowerInvariant()))
                        {
                            addressMatch = true;
                            break;
                        }
                    }
                }

                if (nameMatch || addressMatch)
                {
                    var candidate = new HouseholdMatch(group.Id, group.GroupName, nameMatch, addressMatch);

                    // Prefer matches on both name + address, then name only, then address only
                    if (bestMatch == null ||
                        (candidate.IsNameMatch && candidate.IsAddressMatch) ||
                        (candidate.IsNameMatch && !bestMatch.IsNameMatch))
                    {
                        bestMatch = candidate;
                    }
                }
            }
        }

        _householdMatch = bestMatch;

        // If we found a household, look for existing contact within it
        if (_householdMatch != null)
        {
            var filter = new ContactFilterRequest
            {
                ParentContactId = _householdMatch.GroupId,
                PageSize = 100
            };
            var contactsResult = await _apiClient.GetContactsAsync(filter);
            if (contactsResult.Success && contactsResult.Data?.Items != null)
            {
                foreach (var member in contactsResult.Data.Items)
                {
                    if (member.IsGroup) continue;

                    var nameMatch =
                        string.Equals(member.FirstName, _contactData.FirstName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(member.LastName, _contactData.LastName, StringComparison.OrdinalIgnoreCase);

                    var phoneMatch = !string.IsNullOrWhiteSpace(member.PrimaryPhone) &&
                        _contactData.PhoneNumbers.Any(p =>
                            NormalizePhone(p.PhoneNumber) == NormalizePhone(member.PrimaryPhone));

                    var emailMatch = !string.IsNullOrWhiteSpace(member.PrimaryEmail) &&
                        _contactData.EmailAddresses.Any(e =>
                            string.Equals(e.Email, member.PrimaryEmail, StringComparison.OrdinalIgnoreCase));

                    if (nameMatch || phoneMatch || emailMatch)
                    {
                        _contactMatch = new ContactMatch(
                            member.Id,
                            member.DisplayName ?? $"{member.FirstName} {member.LastName}".Trim(),
                            _householdMatch.GroupId,
                            _householdMatch.GroupName);
                        break;
                    }
                }
            }
        }
    }

    private void ShowActionButtons()
    {
        ActionButtonsSection.IsVisible = true;

        if (_householdMatch != null)
        {
            AddToHouseholdButton.Text = $"Add To {_householdMatch.GroupName}";
            AddToHouseholdButton.IsVisible = true;
            // Also show "Add To Existing Household" for override
            AddToExistingHouseholdButton.IsVisible = true;
        }
        else
        {
            AddToExistingHouseholdButton.IsVisible = true;
        }

        if (_contactMatch != null)
        {
            UpdateMatchedContactButton.Text = $"Update {_contactMatch.DisplayName} in {_contactMatch.HouseholdName}";
            UpdateMatchedContactButton.IsVisible = true;
        }
    }

    private async void OnAddToHouseholdClicked(object? sender, EventArgs e)
    {
        if (_householdMatch == null) return;
        await ImportAsNewContact(_householdMatch.GroupId);
    }

    private async void OnAddToExistingHouseholdClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SelectHouseholdPage));
    }

    private async void OnAddToNewHouseholdClicked(object? sender, EventArgs e)
    {
        if (_contactData == null) return;

        var suggestedName = !string.IsNullOrWhiteSpace(_contactData.LastName)
            ? $"The {_contactData.LastName} Family"
            : "New Household";

        var popup = new NewHouseholdNamePopup(suggestedName);
        var popupResult = await this.ShowPopupAsync<string>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null)
            return;

        var householdName = popupResult.Result;
        if (string.IsNullOrWhiteSpace(householdName))
            return;

        ShowImportProgress("Creating household...");

        var groupRequest = new CreateContactGroupRequest
        {
            ContactType = 0, // Household
            GroupName = householdName
        };

        var groupResult = await _apiClient.CreateContactGroupAsync(groupRequest);
        if (!groupResult.Success || groupResult.Data == null)
        {
            ShowError($"Failed to create household: {groupResult.ErrorMessage}");
            return;
        }

        // Force address import for new household
        ForceSelectAllAddresses();

        // Add address to the group itself
        foreach (var (_, addr) in _addressChecks)
        {
            await AddAddressToContact(groupResult.Data.Id, addr);
        }

        await ImportAsNewContact(groupResult.Data.Id);
    }

    private async void OnUpdateMatchedContactClicked(object? sender, EventArgs e)
    {
        if (_contactMatch == null) return;
        await UpdateExistingContact(_contactMatch.ContactId);
    }

    private async void OnUpdateExistingContactClicked(object? sender, EventArgs e)
    {
        var popup = new SearchContactPopup(_apiClient);
        var popupResult = await this.ShowPopupAsync<ContactSummaryDto>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is not ContactSummaryDto selectedContact)
            return;

        await UpdateExistingContact(selectedContact.Id);
    }

    private async Task ImportAsNewContact(Guid parentGroupId)
    {
        if (_contactData == null) return;

        ShowImportProgress("Creating contact...");

        var request = new CreateContactRequest
        {
            FirstName = _contactData.FirstName,
            MiddleName = _contactData.MiddleName,
            LastName = _contactData.LastName,
            ParentContactId = parentGroupId,
            Visibility = 0 // TenantShared
        };

        if (CompanyCheckbox.IsChecked && _contactData.HasCompany)
        {
            request.CompanyName = _contactData.CompanyName;
            request.Title = _contactData.Title;
        }

        if (BirthdayCheckbox.IsChecked && _contactData.HasBirthday)
        {
            request.BirthYear = _contactData.BirthYear;
            request.BirthMonth = _contactData.BirthMonth;
            request.BirthDay = _contactData.BirthDay;
            if (_contactData.BirthYear.HasValue)
                request.BirthDatePrecision = 0; // Full date
            else
                request.BirthDatePrecision = 1; // Month/Day only
        }

        if (NotesCheckbox.IsChecked && _contactData.HasNotes)
        {
            request.Notes = _contactData.Notes;
        }

        var contactResult = await _apiClient.CreateContactAsync(request);
        if (!contactResult.Success || contactResult.Data == null)
        {
            ShowError($"Failed to create contact: {contactResult.ErrorMessage}");
            return;
        }

        var contactId = contactResult.Data.Id;

        // Add selected fields
        await AddSelectedFieldsToContact(contactId);

        // If the household has no address, set it from the imported contact's address
        await SetHouseholdAddressIfMissing(parentGroupId);

        // Upload profile image if available
        await UploadProfileImageIfAvailable(contactId);

        await NavigateToContact(contactId);
    }

    private async Task UpdateExistingContact(Guid contactId)
    {
        if (_contactData == null) return;

        ShowImportProgress("Updating contact...");

        // Fetch existing contact to compare
        var existingResult = await _apiClient.GetContactAsync(contactId);
        if (!existingResult.Success || existingResult.Data == null)
        {
            ShowError($"Failed to load contact: {existingResult.ErrorMessage}");
            return;
        }

        var existing = existingResult.Data;

        var updateRequest = new UpdateContactRequest
        {
            FirstName = existing.FirstName,
            MiddleName = existing.MiddleName,
            LastName = existing.LastName,
            PreferredName = existing.PreferredName,
            CompanyName = existing.CompanyName,
            Title = existing.Title,
            Gender = existing.Gender,
            BirthYear = existing.BirthYear,
            BirthMonth = existing.BirthMonth,
            BirthDay = existing.BirthDay,
            BirthDatePrecision = existing.BirthDatePrecision,
            Notes = existing.Notes,
            Visibility = existing.Visibility,
            IsActive = existing.IsActive,
            UseGravatar = existing.UseGravatar
        };

        var hasUpdates = false;

        if (CompanyCheckbox.IsChecked && _contactData.HasCompany)
        {
            updateRequest.CompanyName = _contactData.CompanyName;
            updateRequest.Title = _contactData.Title;
            hasUpdates = true;
        }

        if (BirthdayCheckbox.IsChecked && _contactData.HasBirthday)
        {
            updateRequest.BirthYear = _contactData.BirthYear;
            updateRequest.BirthMonth = _contactData.BirthMonth;
            updateRequest.BirthDay = _contactData.BirthDay;
            hasUpdates = true;
        }

        if (NotesCheckbox.IsChecked && _contactData.HasNotes)
        {
            var notes = existing.Notes;
            if (!string.IsNullOrWhiteSpace(notes))
                notes += "\n\n" + _contactData.Notes;
            else
                notes = _contactData.Notes;
            updateRequest.Notes = notes;
            hasUpdates = true;
        }

        if (hasUpdates)
        {
            await _apiClient.UpdateContactAsync(contactId, updateRequest);
        }

        // Add selected fields, skipping ones that already exist
        await AddSelectedFieldsToContact(contactId, existing);

        // Upload profile image if available
        await UploadProfileImageIfAvailable(contactId);

        await NavigateToContact(contactId);
    }

    private async Task AddSelectedFieldsToContact(Guid contactId, ContactDetailDto? existing = null)
    {
        // Build sets of existing values for duplicate detection
        var existingPhones = new HashSet<string>(
            existing?.PhoneNumbers?.Select(p => NormalizePhone(p.PhoneNumber)) ?? [],
            StringComparer.OrdinalIgnoreCase);
        var existingEmails = new HashSet<string>(
            existing?.EmailAddresses?.Select(e => e.Email) ?? [],
            StringComparer.OrdinalIgnoreCase);
        var existingAddressLines = new HashSet<string>(
            existing?.Addresses?.Select(a => (a.Address?.AddressLine1 ?? "").ToLowerInvariant().Trim())
                .Where(s => !string.IsNullOrEmpty(s)) ?? [],
            StringComparer.OrdinalIgnoreCase);

        // Add phones (skip duplicates)
        var isFirstPhone = existingPhones.Count == 0;
        foreach (var (checkbox, phone) in _phoneChecks)
        {
            if (!checkbox.IsChecked) continue;
            if (existingPhones.Contains(NormalizePhone(phone.PhoneNumber))) continue;
            ImportProgressLabel.Text = $"Adding phone {phone.PhoneNumber}...";
            await _apiClient.AddContactPhoneAsync(contactId, new AddPhoneRequest
            {
                PhoneNumber = phone.PhoneNumber,
                Tag = phone.Tag,
                IsPrimary = isFirstPhone
            });
            isFirstPhone = false;
        }

        // Add emails (skip duplicates)
        var isFirstEmail = existingEmails.Count == 0;
        foreach (var (checkbox, email) in _emailChecks)
        {
            if (!checkbox.IsChecked) continue;
            if (existingEmails.Contains(email.Email)) continue;
            ImportProgressLabel.Text = $"Adding email {email.Email}...";
            await _apiClient.AddContactEmailAsync(contactId, new AddEmailRequest
            {
                Email = email.Email,
                Tag = email.Tag,
                IsPrimary = isFirstEmail
            });
            isFirstEmail = false;
        }

        // Add addresses (skip duplicates by address line 1)
        foreach (var (checkbox, addr) in _addressChecks)
        {
            if (!checkbox.IsChecked) continue;
            var addrLine = (addr.ValidatedAddress?.AddressLine1 ?? addr.AddressLine1 ?? "").ToLowerInvariant().Trim();
            if (!string.IsNullOrEmpty(addrLine) && existingAddressLines.Contains(addrLine)) continue;
            await AddAddressToContact(contactId, addr);
        }
    }

    private async Task AddAddressToContact(Guid contactId, SharedAddressEntry addr)
    {
        ImportProgressLabel.Text = "Adding address...";

        var request = new AddContactAddressRequest
        {
            Tag = addr.Tag,
            IsPrimary = _addressChecks.FindIndex(a => a.Entry == addr) == 0
        };

        // Use validated address if available
        if (addr is { IsValidated: true, ValidatedAddress: not null })
        {
            var v = addr.ValidatedAddress;
            request.AddressLine1 = v.AddressLine1;
            request.AddressLine2 = v.AddressLine2;
            request.City = v.City;
            request.StateProvince = v.StateProvince;
            request.PostalCode = v.PostalCode;
            request.Country = v.Country;
            request.CountryCode = v.CountryCode;
            request.Latitude = v.Latitude;
            request.Longitude = v.Longitude;
            request.GeoapifyPlaceId = v.GeoapifyPlaceId;
            request.FormattedAddress = v.FormattedAddress;
        }
        else
        {
            request.AddressLine1 = addr.AddressLine1;
            request.AddressLine2 = addr.AddressLine2;
            request.City = addr.City;
            request.StateProvince = addr.StateProvince;
            request.PostalCode = addr.PostalCode;
            request.Country = addr.Country;
        }

        await _apiClient.AddContactAddressAsync(contactId, request);
    }

    private async Task SetHouseholdAddressIfMissing(Guid groupId)
    {
        // Check if any address was imported
        var importedAddress = _addressChecks.FirstOrDefault(a => a.Checkbox.IsChecked);
        if (importedAddress.Entry == null) return;

        // Check if the household already has an address
        var groupResult = await _apiClient.GetContactAddressesAsync(groupId);
        if (groupResult.Success && groupResult.Data?.Count > 0) return;

        // Add the first imported address to the household
        ImportProgressLabel.Text = "Setting household address...";
        await AddAddressToContact(groupId, importedAddress.Entry);
    }

    private async Task UploadProfileImageIfAvailable(Guid contactId)
    {
        if (_contactData is not { HasProfileImage: true }) return;

        ImportProgressLabel.Text = "Uploading profile image...";
        using var stream = new MemoryStream(_contactData.ProfileImageData!);
        await _apiClient.UploadContactProfileImageAsync(contactId, stream, "profile.jpg");
    }

    private void ForceSelectAllAddresses()
    {
        foreach (var (checkbox, _) in _addressChecks)
        {
            checkbox.IsChecked = true;
        }
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = false;
        ImportProgressSection.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }

    private void ShowImportProgress(string message)
    {
        ActionButtonsSection.IsVisible = false;
        ImportProgressSection.IsVisible = true;
        ImportProgressLabel.Text = message;
    }

    private async Task NavigateToContact(Guid contactId)
    {
        await Shell.Current.GoToAsync($"../{nameof(ContactDetailPage)}?ContactId={contactId}");
    }

    private async void OnGoBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Called when returning from SelectHouseholdPage with a selected household.
    /// </summary>
    public async Task HandleHouseholdSelected(Guid householdId)
    {
        await ImportAsNewContact(householdId);
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
