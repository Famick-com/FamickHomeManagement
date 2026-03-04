using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

[QueryProperty(nameof(MealId), "MealId")]
public partial class MealEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MealDetailMobile? _meal;
    private bool _isEditMode;
    private bool _loaded;
    private readonly List<MealItemEditModel> _items = new();

    // Search state
    private enum SearchMode { None, Product, Recipe }
    private SearchMode _searchMode = SearchMode.None;
    private CancellationTokenSource? _searchCts;
    private Guid? _storeSearchListId;

    public string MealId { get; set; } = string.Empty;

    public MealEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        // Hide CollectionViews initially to prevent iOS phantom rendering
        SearchResultsCollection.IsVisible = false;
        StoreResultsCollection.IsVisible = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded) return;
        _loaded = true;

        _isEditMode = !string.IsNullOrEmpty(MealId) && Guid.TryParse(MealId, out _);

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Meal";
            await LoadMealAsync();
        }
        else
        {
            TitleLabel.Text = "New Meal";
            RenderItems();
        }
    }

    #region Form Loading

    private async Task LoadMealAsync()
    {
        if (!Guid.TryParse(MealId, out var id)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;

        try
        {
            var result = await _apiClient.GetMealAsync(id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _meal = result.Data;
                    PopulateForm();
                }

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
                _ = DisplayAlert("Error", $"Failed to load meal: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_meal == null) return;

        NameEntry.Text = _meal.Name;
        NotesEditor.Text = _meal.Notes;
        FavoriteSwitch.IsToggled = _meal.IsFavorite;

        _items.Clear();
        foreach (var item in _meal.Items.OrderBy(i => i.SortOrder))
        {
            _items.Add(new MealItemEditModel
            {
                ItemType = item.ItemType,
                FreetextDescription = item.FreetextDescription,
                RecipeId = item.RecipeId,
                RecipeName = item.RecipeName,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductQuantity = item.ProductQuantity,
                ProductQuantityUnitName = item.ProductQuantityUnitName
            });
        }

        RenderItems();
    }

    #endregion

    #region Items Rendering

    private void RenderItems()
    {
        ItemsList.Children.Clear();
        EmptyItemsLabel.IsVisible = _items.Count == 0;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var index = i;

            var icon = item.ItemType switch
            {
                0 => "📖",
                1 => "🛒",
                2 => "📝",
                _ => "•"
            };

            var card = new Border
            {
                Padding = new Thickness(12, 10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Colors.White,
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

            var nameLabel = new Label
            {
                Text = $"{icon} {item.DisplayName}",
                FontSize = 15,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black,
            };
            textStack.Children.Add(nameLabel);

            var typeLabel = new Label
            {
                Text = item.ItemType switch
                {
                    0 => "Recipe",
                    1 => "Product",
                    2 => "Freetext",
                    _ => "Unknown"
                },
                FontSize = 12,
                TextColor = Color.FromArgb("#888888")
            };
            textStack.Children.Add(typeLabel);

            grid.Children.Add(textStack);
            Grid.SetColumn(textStack, 0);

            var deleteBtn = new Button
            {
                Text = "✕",
                FontSize = 16,
                Padding = new Thickness(8, 4),
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#E53935"),
                VerticalOptions = LayoutOptions.Center
            };
            deleteBtn.Clicked += (_, _) =>
            {
                _items.RemoveAt(index);
                RenderItems();
            };
            grid.Children.Add(deleteBtn);
            Grid.SetColumn(deleteBtn, 1);

            card.Content = grid;
            ItemsList.Children.Add(card);
        }
    }

    #endregion

    #region Search Overlay

    private void OnAddProductClicked(object? sender, EventArgs e)
    {
        ShowSearchOverlay(SearchMode.Product);
    }

    private void OnAddRecipeClicked(object? sender, EventArgs e)
    {
        ShowSearchOverlay(SearchMode.Recipe);
    }

    private void ShowSearchOverlay(SearchMode mode)
    {
        _searchMode = mode;
        SearchEntry.Text = string.Empty;
        SearchResultsCollection.ItemsSource = null;
        StoreResultsSection.IsVisible = false;
        SearchLoadingIndicator.IsVisible = false;

        if (mode == SearchMode.Product)
        {
            SearchTitleLabel.Text = "Search Products";
            SearchEntry.Placeholder = "Search products...";
            StoreSearchButton.IsVisible = true;
            SearchResultsCollection.ItemTemplate = CreateProductResultTemplate();
            SearchEmptyLabel.Text = "Type to search products...";
        }
        else
        {
            SearchTitleLabel.Text = "Search Recipes";
            SearchEntry.Placeholder = "Search recipes...";
            StoreSearchButton.IsVisible = false;
            SearchResultsCollection.ItemTemplate = CreateRecipeResultTemplate();
            SearchEmptyLabel.Text = "Type to search recipes...";
        }

        ContentScroll.IsVisible = false;
        SearchOverlay.InputTransparent = false;
        SearchOverlay.IsVisible = true;
        SearchResultsCollection.IsVisible = true;
        SearchResultsCollection.SelectionMode = SelectionMode.Single;
        StoreResultsCollection.SelectionMode = SelectionMode.Single;
        SearchEntry.Focus();
    }

    private void HideSearchOverlay()
    {
        _searchMode = SearchMode.None;
        _searchCts?.Cancel();
        SearchResultsCollection.SelectionMode = SelectionMode.None;
        SearchResultsCollection.ItemsSource = null;
        SearchResultsCollection.IsVisible = false;
        StoreResultsCollection.SelectionMode = SelectionMode.None;
        StoreResultsCollection.ItemsSource = null;
        StoreResultsCollection.IsVisible = false;
        SearchOverlay.IsVisible = false;
        SearchOverlay.InputTransparent = true;
        ContentScroll.IsVisible = true;
    }

    private void OnSearchCancelClicked(object? sender, EventArgs e)
    {
        HideSearchOverlay();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? string.Empty;
        if (query.Length < 2)
        {
            SearchResultsCollection.ItemsSource = null;
            StoreResultsSection.IsVisible = false;
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        _ = DebounceSearchAsync(query, ct);
    }

    private async Task DebounceSearchAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
            if (ct.IsCancellationRequested) return;
            await ExecuteSearchAsync(query, ct);
        }
        catch (OperationCanceledException) { }
    }

    private async Task ExecuteSearchAsync(string query, CancellationToken ct)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SearchLoadingIndicator.IsVisible = true;
            SearchLoadingIndicator.IsRunning = true;
        });

        try
        {
            if (_searchMode == SearchMode.Product)
            {
                var result = await _apiClient.AutocompleteProductsAsync(query, 15);
                if (ct.IsCancellationRequested) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (result.Success && result.Data != null)
                    {
                        SearchResultsCollection.ItemsSource = result.Data;
                        SearchEmptyLabel.Text = "No products found.";
                    }
                });
            }
            else if (_searchMode == SearchMode.Recipe)
            {
                var result = await _apiClient.GetRecipesAsync(query);
                if (ct.IsCancellationRequested) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (result.Success && result.Data != null)
                    {
                        SearchResultsCollection.ItemsSource = result.Data;
                        SearchEmptyLabel.Text = "No recipes found.";
                    }
                });
            }
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchLoadingIndicator.IsVisible = false;
                SearchLoadingIndicator.IsRunning = false;
            });
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        // Trigger immediate search on enter
        var query = SearchEntry.Text?.Trim() ?? string.Empty;
        if (query.Length < 2) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = ExecuteSearchAsync(query, _searchCts.Token);
    }

    private async void OnStoreSearchClicked(object? sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            await DisplayAlert("Search", "Enter a search term first.", "OK");
            return;
        }

        // Get a shopping list ID for store integration search
        if (_storeSearchListId == null)
        {
            var listsResult = await _apiClient.GetShoppingListsAsync();
            if (listsResult.Success && listsResult.Data?.Count > 0)
            {
                var listWithStore = listsResult.Data.FirstOrDefault(l => l.HasStoreIntegration)
                    ?? listsResult.Data.First();
                _storeSearchListId = listWithStore.Id;
            }
            else
            {
                await DisplayAlert("Store Search", "No shopping lists found. Create a shopping list with a store integration to search stores.", "OK");
                return;
            }
        }

        SearchLoadingIndicator.IsVisible = true;
        SearchLoadingIndicator.IsRunning = true;

        try
        {
            var result = await _apiClient.SearchProductsAsync(_storeSearchListId.Value, query);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data?.Count > 0)
                {
                    StoreResultsCollection.ItemTemplate = CreateStoreResultTemplate();
                    StoreResultsCollection.ItemsSource = result.Data;
                    StoreResultsCollection.IsVisible = true;
                    StoreResultsSection.IsVisible = true;
                }
                else
                {
                    StoreResultsSection.IsVisible = false;
                    _ = DisplayAlert("Store Search", result.ErrorMessage ?? "No store results found.", "OK");
                }
            });
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchLoadingIndicator.IsVisible = false;
                SearchLoadingIndicator.IsRunning = false;
            });
        }
    }

    private void OnSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        var selection = e.CurrentSelection.FirstOrDefault();
        if (selection == null) return;
        SearchResultsCollection.SelectedItem = null;

        if (_searchMode == SearchMode.Product && selection is ProductAutocompleteResult product)
        {
            _items.Add(new MealItemEditModel
            {
                ItemType = 1,
                ProductId = product.Id,
                ProductName = product.Name
            });
            HideSearchOverlay();
            RenderItems();
        }
        else if (_searchMode == SearchMode.Recipe && selection is RecipeSummary recipe)
        {
            _items.Add(new MealItemEditModel
            {
                ItemType = 0,
                RecipeId = recipe.Id,
                RecipeName = recipe.Name
            });
            HideSearchOverlay();
            RenderItems();
        }
    }

    private async void OnStoreResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not StoreProductResult storeProduct) return;
        StoreResultsCollection.SelectedItem = null;

        // Create a local product from the store result
        var createRequest = new CreateProductFromLookupMobileRequest
        {
            Name = storeProduct.Name,
            Brand = storeProduct.Brand,
            Barcode = storeProduct.Barcode,
            ImageUrl = storeProduct.ImageUrl,
            ExternalId = storeProduct.ExternalProductId,
            Aisle = storeProduct.Aisle,
            Department = storeProduct.Department,
            Price = storeProduct.Price
        };

        var result = await _apiClient.CreateProductFromLookupAsync(createRequest);
        if (result.Success && result.Data != null)
        {
            _items.Add(new MealItemEditModel
            {
                ItemType = 1,
                ProductId = result.Data.Id,
                ProductName = result.Data.Name
            });

            // Create a todo to populate the product details
            _ = _apiClient.CreateTodoItemAsync(new CreateTodoItemRequest
            {
                TaskType = "ReviewProduct",
                Reason = "Product added from store integration via meal planner",
                RelatedEntityId = result.Data.Id,
                RelatedEntityType = "Product",
                Description = $"Populate details for '{result.Data.Name}' (added from store search)"
            });

            HideSearchOverlay();
            RenderItems();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create product from store result", "OK");
        }
    }

    #endregion

    #region DataTemplates

    private DataTemplate CreateProductResultTemplate()
    {
        return new DataTemplate(() =>
        {
            var border = new Border
            {
                Margin = new Thickness(10, 3),
                Padding = new Thickness(12, 10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Colors.White,
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

            var nameLabel = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold };
            nameLabel.SetBinding(Label.TextProperty, "Name");
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            textStack.Children.Add(nameLabel);

            var descLabel = new Label { FontSize = 12 };
            descLabel.SetBinding(Label.TextProperty, "Description");
            descLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#888888"), Color.FromArgb("#999999"));
            textStack.Children.Add(descLabel);

            var groupLabel = new Label { FontSize = 11 };
            groupLabel.SetBinding(Label.TextProperty, "ProductGroupName");
            groupLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1976D2"), Color.FromArgb("#90CAF9"));
            textStack.Children.Add(groupLabel);

            grid.Children.Add(textStack);
            Grid.SetColumn(textStack, 0);

            var addLabel = new Label
            {
                Text = "+",
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };
            addLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1976D2"), Color.FromArgb("#90CAF9"));
            grid.Children.Add(addLabel);
            Grid.SetColumn(addLabel, 1);

            border.Content = grid;
            return border;
        });
    }

    private DataTemplate CreateRecipeResultTemplate()
    {
        return new DataTemplate(() =>
        {
            var border = new Border
            {
                Margin = new Thickness(10, 3),
                Padding = new Thickness(12, 10),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Colors.White,
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

            var nameLabel = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold };
            nameLabel.SetBinding(Label.TextProperty, "Name");
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            textStack.Children.Add(nameLabel);

            var detailStack = new HorizontalStackLayout { Spacing = 8 };

            var stepsLabel = new Label { FontSize = 12 };
            stepsLabel.SetBinding(Label.TextProperty, "StepCount", stringFormat: "{0} steps");
            stepsLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#888888"), Color.FromArgb("#999999"));
            detailStack.Children.Add(stepsLabel);

            var servingsLabel = new Label { FontSize = 12 };
            servingsLabel.SetBinding(Label.TextProperty, "Servings", stringFormat: "{0} servings");
            servingsLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#888888"), Color.FromArgb("#999999"));
            detailStack.Children.Add(servingsLabel);

            textStack.Children.Add(detailStack);

            var sourceLabel = new Label { FontSize = 11 };
            sourceLabel.SetBinding(Label.TextProperty, "Source");
            sourceLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#43A047"), Color.FromArgb("#81C784"));
            textStack.Children.Add(sourceLabel);

            grid.Children.Add(textStack);
            Grid.SetColumn(textStack, 0);

            var addLabel = new Label
            {
                Text = "+",
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };
            addLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#43A047"), Color.FromArgb("#81C784"));
            grid.Children.Add(addLabel);
            Grid.SetColumn(addLabel, 1);

            border.Content = grid;
            return border;
        });
    }

    private DataTemplate CreateStoreResultTemplate()
    {
        return new DataTemplate(() =>
        {
            var border = new Border
            {
                Margin = new Thickness(0, 3),
                Padding = new Thickness(12, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#333333") : Color.FromArgb("#F0F0F0"),
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

            var nameLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold };
            nameLabel.SetBinding(Label.TextProperty, "Name");
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            textStack.Children.Add(nameLabel);

            var brandLabel = new Label { FontSize = 12 };
            brandLabel.SetBinding(Label.TextProperty, "Brand");
            brandLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#888888"), Color.FromArgb("#999999"));
            textStack.Children.Add(brandLabel);

            var aisleLabel = new Label { FontSize = 11 };
            aisleLabel.SetBinding(Label.TextProperty, "Aisle", stringFormat: "Aisle: {0}");
            aisleLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#AAAAAA"));
            textStack.Children.Add(aisleLabel);

            grid.Children.Add(textStack);
            Grid.SetColumn(textStack, 0);

            var priceLabel = new Label
            {
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };
            priceLabel.SetBinding(Label.TextProperty, "Price", stringFormat: "${0:F2}");
            priceLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#43A047"), Color.FromArgb("#81C784"));
            grid.Children.Add(priceLabel);
            Grid.SetColumn(priceLabel, 1);

            border.Content = grid;
            return border;
        });
    }

    #endregion

    #region Save

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Meal name is required.", "OK");
            return;
        }

        // Ensure at least one item exists
        if (_items.Count == 0)
        {
            _items.Add(new MealItemEditModel
            {
                ItemType = 2,
                FreetextDescription = name
            });
        }

        SaveToolbarItem.IsEnabled = false;

        try
        {
            var items = _items.Select((item, idx) => new CreateMealItemMobileRequest
            {
                ItemType = item.ItemType,
                ProductId = item.ProductId,
                RecipeId = item.RecipeId,
                FreetextDescription = item.ItemType == 2 ? item.FreetextDescription : null,
                SortOrder = idx
            }).ToList();

            if (_isEditMode && _meal != null)
            {
                var request = new UpdateMealMobileRequest
                {
                    Name = name,
                    Notes = NotesEditor.Text?.Trim(),
                    IsFavorite = FavoriteSwitch.IsToggled,
                    Items = items
                };

                var result = await _apiClient.UpdateMealAsync(_meal.Id, request);
                if (result.Success)
                {
                    await Navigation.PopAsync();
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update meal", "OK");
            }
            else
            {
                var request = new CreateMealMobileRequest
                {
                    Name = name,
                    Notes = NotesEditor.Text?.Trim(),
                    IsFavorite = FavoriteSwitch.IsToggled,
                    Items = items
                };

                var result = await _apiClient.CreateMealAsync(request);
                if (result.Success && result.Data != null)
                {
                    await Navigation.PopAsync();
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create meal", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }

    #endregion

    private class MealItemEditModel
    {
        public int ItemType { get; set; }
        public string? FreetextDescription { get; set; }
        public Guid? RecipeId { get; set; }
        public string? RecipeName { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? ProductQuantity { get; set; }
        public string? ProductQuantityUnitName { get; set; }

        public string DisplayName => ItemType switch
        {
            0 => RecipeName ?? "Recipe",
            1 => ProductName ?? "Product",
            2 => FreetextDescription ?? "Item",
            _ => "Unknown"
        };
    }
}
