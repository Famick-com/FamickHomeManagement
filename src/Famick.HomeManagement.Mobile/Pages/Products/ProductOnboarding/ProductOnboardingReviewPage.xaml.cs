using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;

public partial class ProductOnboardingReviewPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ProductOnboardingAnswersDto _answers = new();
    private ProductOnboardingPreviewResponse? _preview;
    private readonly Dictionary<string, HashSet<Guid>> _selectedByCategory = new();
    private readonly Dictionary<string, bool> _expandedCategories = new();

    public ProductOnboardingReviewPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    public void SetAnswers(ProductOnboardingAnswersDto answers)
    {
        _answers = answers;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.PreviewProductOnboardingAsync(_answers);
            if (!result.Success || result.Data == null)
            {
                ShowError(result.ErrorMessage ?? "Failed to load product preview.");
                return;
            }

            _preview = result.Data;

            // Select all products by default
            _selectedByCategory.Clear();
            _expandedCategories.Clear();
            foreach (var category in _preview.Categories)
            {
                var ids = new HashSet<Guid>(category.Items.Select(i => i.Id));
                _selectedByCategory[category.Category] = ids;
                _expandedCategories[category.Category] = false;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var totalSelected = _selectedByCategory.Values.Sum(s => s.Count);
                SummaryLabel.Text = $"{totalSelected} of {_preview.FilteredCount} products selected from {_preview.Categories.Count} categories";
                BuildCategoryList();
            });
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

    private void BuildCategoryList()
    {
        CategoriesLayout.Children.Clear();

        if (_preview == null) return;

        foreach (var category in _preview.Categories)
        {
            var isExpanded = _expandedCategories.GetValueOrDefault(category.Category, false);
            var selectedIds = _selectedByCategory.GetValueOrDefault(category.Category, new HashSet<Guid>());
            var allSelected = selectedIds.Count == category.Items.Count;

            // Category header
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(16, 12)
            };

            var categoryLabel = new Label
            {
                Text = $"{category.Category} ({selectedIds.Count}/{category.Items.Count})",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
            };
            Grid.SetColumn(categoryLabel, 0);
            headerGrid.Children.Add(categoryLabel);

            // Select/Deselect All button
            var selectAllBtn = new Button
            {
                Text = allSelected ? "Deselect All" : "Select All",
                FontSize = 11,
                HeightRequest = 30,
                Padding = new Thickness(8, 0),
                CornerRadius = 6,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#1976D2"),
                VerticalOptions = LayoutOptions.Center
            };
            var catName = category.Category;
            var catItems = category.Items;
            selectAllBtn.Clicked += (_, _) =>
            {
                var set = _selectedByCategory.GetValueOrDefault(catName, new HashSet<Guid>());
                if (set.Count == catItems.Count)
                    set.Clear();
                else
                {
                    set.Clear();
                    foreach (var item in catItems)
                        set.Add(item.Id);
                }
                _selectedByCategory[catName] = set;
                UpdateSummary();
                BuildCategoryList();
            };
            Grid.SetColumn(selectAllBtn, 1);
            headerGrid.Children.Add(selectAllBtn);

            // Expand/collapse chevron
            var chevron = new Label
            {
                Text = isExpanded ? "\u25BC" : "\u25B6",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(8, 0, 0, 0),
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#999999") : Color.FromArgb("#666666")
            };
            Grid.SetColumn(chevron, 2);
            headerGrid.Children.Add(chevron);

            var headerTap = new TapGestureRecognizer();
            headerTap.Tapped += (_, _) =>
            {
                _expandedCategories[catName] = !_expandedCategories.GetValueOrDefault(catName, false);
                BuildCategoryList();
            };
            headerGrid.GestureRecognizers.Add(headerTap);

            var categoryContainer = new VerticalStackLayout { Spacing = 0 };

            var categoryBorder = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#2A2A2A") : Colors.White,
                Content = categoryContainer
            };

            categoryContainer.Children.Add(headerGrid);

            // Product items (if expanded)
            if (isExpanded)
            {
                var divider = new BoxView
                {
                    HeightRequest = 1,
                    Margin = new Thickness(16, 0),
                    BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#3A3A3A") : Color.FromArgb("#E8E8E8")
                };
                categoryContainer.Children.Add(divider);

                foreach (var item in catItems)
                {
                    var isItemSelected = selectedIds.Contains(item.Id);
                    var itemGrid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = GridLength.Star }
                        },
                        Padding = new Thickness(16, 8)
                    };

                    var checkBox = new CheckBox
                    {
                        IsChecked = isItemSelected,
                        VerticalOptions = LayoutOptions.Center,
                        Color = Color.FromArgb("#1976D2")
                    };
                    var itemId = item.Id;
                    checkBox.CheckedChanged += (_, args) =>
                    {
                        var set = _selectedByCategory.GetValueOrDefault(catName, new HashSet<Guid>());
                        if (args.Value)
                            set.Add(itemId);
                        else
                            set.Remove(itemId);
                        _selectedByCategory[catName] = set;
                        UpdateSummary();
                    };
                    Grid.SetColumn(checkBox, 0);
                    itemGrid.Children.Add(checkBox);

                    var itemContent = new VerticalStackLayout
                    {
                        VerticalOptions = LayoutOptions.Center,
                        Spacing = 2
                    };
                    itemContent.Children.Add(new Label
                    {
                        Text = item.Name,
                        FontSize = 14,
                        TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
                    });
                    if (!string.IsNullOrEmpty(item.ContainerType))
                    {
                        itemContent.Children.Add(new Label
                        {
                            Text = item.ContainerType,
                            FontSize = 12,
                            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#999999") : Color.FromArgb("#666666")
                        });
                    }
                    if (item.IsStaple)
                    {
                        itemContent.Children.Add(new Label
                        {
                            Text = "Staple item",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#1976D2")
                        });
                    }
                    Grid.SetColumn(itemContent, 1);
                    itemGrid.Children.Add(itemContent);

                    categoryContainer.Children.Add(itemGrid);
                }
            }

            CategoriesLayout.Children.Add(categoryBorder);
        }
    }

    private void UpdateSummary()
    {
        if (_preview == null) return;
        var totalSelected = _selectedByCategory.Values.Sum(s => s.Count);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SummaryLabel.Text = $"{totalSelected} of {_preview.FilteredCount} products selected from {_preview.Categories.Count} categories";
            CreateButton.Text = $"Create {totalSelected} Products";
        });
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnCreateProductsClicked(object? sender, EventArgs e)
    {
        var allSelectedIds = _selectedByCategory.Values
            .SelectMany(s => s)
            .ToList();

        if (allSelectedIds.Count == 0)
        {
            await DisplayAlertAsync("No Products Selected",
                "Please select at least one product to create, or go back to adjust your preferences.",
                "OK");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var request = new ProductOnboardingCompleteRequest
            {
                Answers = _answers,
                SelectedMasterProductIds = allSelectedIds
            };

            var result = await _apiClient.CompleteProductOnboardingAsync(request);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to create products.");
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CompletionDetailsLabel.Text = $"{result.Data!.ProductsCreated} products created" +
                    (result.Data.ProductsSkipped > 0 ? $", {result.Data.ProductsSkipped} skipped (already existed)" : "");
                CompletionPanel.IsVisible = true;
                BottomBar.IsVisible = false;
                CategoriesLayout.IsVisible = false;
                SummaryLabel.IsVisible = false;
            });
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

    private async void OnGoToProductsClicked(object? sender, EventArgs e)
    {
        // Navigate back to root and then to products
        await Navigation.PopToRootAsync();
        await Shell.Current.GoToAsync("//products");
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.IsVisible = false;
        });
    }
}
