using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardMembersPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private List<HouseholdMemberDto> _members = new();

    public WizardMembersPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        SetLoading(true);
        try
        {
            var result = await _apiClient.GetHouseholdMembersAsync();
            if (result.Success && result.Data != null)
            {
                _members = result.Data;
                var currentUser = _members.FirstOrDefault(m => m.IsCurrentUser);
                var others = _members.Where(m => !m.IsCurrentUser).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (currentUser != null)
                    {
                        MyFirstNameEntry.Text = currentUser.FirstName;
                        MyLastNameEntry.Text = currentUser.LastName;
                    }
                    RenderMembersList(others);
                });
            }
        }
        catch
        {
            // Continue without pre-fill
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnSaveMyInfoClicked(object? sender, EventArgs e)
    {
        var firstName = MyFirstNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(firstName))
        {
            ShowError("Please enter your first name.");
            return;
        }

        SetLoading(true);
        HideError();
        try
        {
            var request = new SaveCurrentUserContactRequest
            {
                FirstName = firstName,
                LastName = MyLastNameEntry.Text?.Trim()
            };
            var result = await _apiClient.SaveCurrentUserContactAsync(request);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to save.");
                return;
            }
            await LoadDataAsync();
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

    private async void OnShowAddMemberClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new WizardAddMemberPage(_apiClient));
    }

    private void RenderMembersList(List<HouseholdMemberDto> others)
    {
        MembersListLayout.Children.Clear();
        EmptyMembersLabel.IsVisible = others.Count == 0;

        foreach (var member in others)
        {
            var border = new Border
            {
                Padding = new Thickness(10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Margin = new Thickness(0, 4),
                BackgroundColor = Colors.Transparent
            };
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E0E0E0"), Color.FromArgb("#444444"));

            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var nameLabel = new Label { Text = member.DisplayName, FontAttributes = FontAttributes.Bold };
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            var relLabel = new Label { Text = member.RelationshipType, FontSize = 12 };
            relLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#999999"));

            var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
            stack.Children.Add(nameLabel);
            stack.Children.Add(relLabel);
            Grid.SetColumn(stack, 0);

            var deleteBtn = new Button
            {
                Text = "Remove",
                FontSize = 12,
                HeightRequest = 32,
                Padding = new Thickness(8, 0),
                BackgroundColor = Colors.Transparent,
                VerticalOptions = LayoutOptions.Center
            };
            deleteBtn.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#E53935"), Color.FromArgb("#EF5350"));
            var capturedMember = member;
            deleteBtn.Clicked += async (s, ev) => await DeleteMemberAsync(capturedMember);
            Grid.SetColumn(deleteBtn, 1);

            grid.Children.Add(stack);
            grid.Children.Add(deleteBtn);
            border.Content = grid;
            MembersListLayout.Children.Add(border);
        }
    }

    private async Task DeleteMemberAsync(HouseholdMemberDto member)
    {
        var confirm = await DisplayAlertAsync("Remove Member",
            $"Remove {member.DisplayName} from household?", "Remove", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeleteHouseholdMemberAsync(member.ContactId);
        if (result.Success)
        {
            await LoadDataAsync();
        }
        else
        {
            ShowError(result.ErrorMessage ?? "Failed to remove member.");
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardHomeStatsPage>());
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardHomeStatsPage>());
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
