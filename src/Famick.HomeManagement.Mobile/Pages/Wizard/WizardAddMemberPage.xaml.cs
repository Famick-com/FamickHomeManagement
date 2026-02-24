using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardAddMemberPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    private static readonly string[] RelationshipTypes =
    [
        // Family - Parent/Child
        "Mother", "Father", "Parent", "Daughter", "Son", "Child",
        // Siblings
        "Sister", "Brother", "Sibling",
        // Extended
        "Grandmother", "Grandfather", "Grandparent",
        "Granddaughter", "Grandson", "Grandchild",
        "Aunt", "Uncle", "Niece", "Nephew", "Cousin",
        // In-Laws
        "Mother-in-Law", "Father-in-Law", "Sister-in-Law", "Brother-in-Law",
        "Daughter-in-Law", "Son-in-Law", "Sibling-in-Law",
        // Partners
        "Spouse", "Partner", "Ex-Spouse", "Ex-Partner",
        // Step-Family
        "Stepmother", "Stepfather", "Stepparent",
        "Stepdaughter", "Stepson", "Stepchild",
        "Stepsister", "Stepbrother", "Stepsibling",
        // Professional
        "Colleague", "Boss", "Manager", "Employee", "Client", "Vendor",
        // Social
        "Friend", "Neighbor", "Roommate", "Classmate",
        // Other
        "Other"
    ];

    public WizardAddMemberPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        RelationshipPicker.ItemsSource = RelationshipTypes;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var firstName = FirstNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(firstName))
        {
            ShowError("Please enter a first name.");
            return;
        }

        SetLoading(true);
        HideError();
        try
        {
            var lastName = LastNameEntry.Text?.Trim();
            var relationship = RelationshipPicker.SelectedItem as string;

            // Check for duplicates
            var dupCheck = await _apiClient.CheckDuplicateContactAsync(new CheckDuplicateContactRequest
            {
                FirstName = firstName,
                LastName = lastName
            });

            if (dupCheck.Success && dupCheck.Data?.HasDuplicates == true)
            {
                var match = dupCheck.Data.Matches.First();
                var useDuplicate = await DisplayAlertAsync(
                    "Possible Duplicate",
                    $"A contact named \"{match.DisplayName}\" already exists. Link this contact instead of creating a new one?",
                    "Link Existing", "Create New");

                if (useDuplicate)
                {
                    var linkResult = await _apiClient.AddHouseholdMemberAsync(new AddHouseholdMemberRequest
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        RelationshipType = relationship,
                        ExistingContactId = match.ContactId
                    });
                    if (linkResult.Success) { await Navigation.PopAsync(); return; }
                    ShowError(linkResult.ErrorMessage ?? "Failed to add member.");
                    return;
                }
            }

            var result = await _apiClient.AddHouseholdMemberAsync(new AddHouseholdMemberRequest
            {
                FirstName = firstName,
                LastName = lastName,
                RelationshipType = relationship
            });

            if (result.Success)
                await Navigation.PopAsync();
            else
                ShowError(result.ErrorMessage ?? "Failed to add member.");
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
    }

    private void HideError()
    {
        MainThread.BeginInvokeOnMainThread(() => ErrorLabel.IsVisible = false);
    }
}
