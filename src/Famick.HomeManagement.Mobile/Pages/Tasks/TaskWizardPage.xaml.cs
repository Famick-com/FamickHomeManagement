using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Tasks;

public partial class TaskWizardPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    private List<TodoItemDto> _pendingTodos = new();
    private int _currentIndex;
    private int _completedCount;

    private ProductDto? _product;
    private List<LocationDto> _locations = new();
    private List<QuantityUnitSummary> _quantityUnits = new();
    private Guid? _selectedParentProductId;
    private string? _pendingParentName;
    private CancellationTokenSource? _parentSearchDebounce;
    private bool _suppressParentSearch;
    private bool _referenceDataLoaded;

    public TaskWizardPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // If returning from ProductEditPage, reload current task's product
        if (_product != null && _pendingTodos.Count > 0 && _currentIndex < _pendingTodos.Count)
        {
            var todo = _pendingTodos[_currentIndex];
            if (todo.RelatedEntityId.HasValue)
            {
                var result = await _apiClient.GetProductByIdAsync(todo.RelatedEntityId.Value);
                if (result.Success && result.Data != null)
                {
                    _product = result.Data;
                    MainThread.BeginInvokeOnMainThread(() => PopulateProductForm());
                }
            }
            return;
        }

        await LoadPendingTodosAsync();
    }

    private async Task LoadPendingTodosAsync()
    {
        ShowLoading();

        var result = await _apiClient.GetTodoItemsAsync(false);
        if (result.Success && result.Data != null)
        {
            _pendingTodos = result.Data
                .Where(t => t.TaskTypeName == "Product" && !t.IsCompleted)
                .ToList();
        }
        else
        {
            _pendingTodos = new List<TodoItemDto>();
        }

        _currentIndex = 0;
        _completedCount = 0;

        if (_pendingTodos.Count == 0)
        {
            ShowEmpty();
            return;
        }

        if (!_referenceDataLoaded)
        {
            await LoadReferenceDataAsync();
        }

        await ShowCurrentTaskAsync();
    }

    private async Task LoadReferenceDataAsync()
    {
        var locationsTask = _apiClient.GetLocationsAsync();
        var unitsTask = _apiClient.GetQuantityUnitsAsync();

        await Task.WhenAll(locationsTask, unitsTask);

        if (locationsTask.Result.Success && locationsTask.Result.Data != null)
            _locations = locationsTask.Result.Data;

        if (unitsTask.Result.Success && unitsTask.Result.Data != null)
            _quantityUnits = unitsTask.Result.Data;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LocationPicker.ItemsSource = _locations.Select(l => l.Name).ToList();
            PurchaseUnitPicker.ItemsSource = _quantityUnits.Select(u => u.Name).ToList();
            StockUnitPicker.ItemsSource = _quantityUnits.Select(u => u.Name).ToList();
        });

        _referenceDataLoaded = true;
    }

    private async Task ShowCurrentTaskAsync()
    {
        if (_currentIndex >= _pendingTodos.Count)
        {
            ShowCelebration();
            return;
        }

        var todo = _pendingTodos[_currentIndex];

        // Load the related product
        _product = null;
        _selectedParentProductId = null;

        if (todo.RelatedEntityId.HasValue)
        {
            var result = await _apiClient.GetProductByIdAsync(todo.RelatedEntityId.Value);
            if (result.Success && result.Data != null)
            {
                _product = result.Data;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProgressLabel.Text = $"Task {_currentIndex + 1} of {_pendingTodos.Count}";
            TaskDescriptionLabel.Text = todo.Description ?? "Review product";

            if (!string.IsNullOrEmpty(todo.Reason))
            {
                ReasonBanner.IsVisible = true;
                ReasonLabel.Text = todo.Reason;
            }
            else
            {
                ReasonBanner.IsVisible = false;
            }

            BackButton.IsEnabled = _currentIndex > 0;

            PopulateProductForm();
            ShowWizard();
        });
    }

    private void PopulateProductForm()
    {
        // Reset parent search
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;
        ParentSearchResults.IsVisible = false;
        CreateParentButton.IsVisible = false;
        SelectedParentBorder.IsVisible = false;
        PendingParentBorder.IsVisible = false;
        ClearParentButton.IsVisible = false;
        _selectedParentProductId = null;
        _pendingParentName = null;

        if (_product == null)
        {
            NameEntry.Text = string.Empty;
            LocationPicker.SelectedIndex = -1;
            PurchaseUnitPicker.SelectedIndex = -1;
            StockUnitPicker.SelectedIndex = -1;
            return;
        }

        NameEntry.Text = _product.Name;

        // Location
        var locIdx = _locations.FindIndex(l => l.Id == _product.LocationId);
        LocationPicker.SelectedIndex = locIdx >= 0 ? locIdx : -1;

        // Units
        var purchaseIdx = _quantityUnits.FindIndex(u => u.Id == _product.QuantityUnitIdPurchase);
        PurchaseUnitPicker.SelectedIndex = purchaseIdx >= 0 ? purchaseIdx : -1;

        var stockIdx = _quantityUnits.FindIndex(u => u.Id == _product.QuantityUnitIdStock);
        StockUnitPicker.SelectedIndex = stockIdx >= 0 ? stockIdx : -1;

        // Parent product
        if (_product.ParentProductId.HasValue && !string.IsNullOrEmpty(_product.ParentProductName))
        {
            _selectedParentProductId = _product.ParentProductId;
            ShowSelectedParent(_product.ParentProductName);
        }
    }

    #region Parent Product Search

    private void OnParentSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressParentSearch) return;

        _parentSearchDebounce?.Cancel();
        _parentSearchDebounce = new CancellationTokenSource();
        var token = _parentSearchDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var query = e.NewTextValue?.Trim();
                if (string.IsNullOrEmpty(query) || query.Length < 2)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ParentSearchResults.IsVisible = false;
                    });
                    return;
                }

                var result = await _apiClient.SearchParentProductsAsync(query);
                if (token.IsCancellationRequested) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (result.Success && result.Data != null && result.Data.Count > 0)
                    {
                        var filtered = _product != null
                            ? result.Data.Where(p => p.Id != _product.Id).ToList()
                            : result.Data;

                        ParentSearchResults.ItemsSource = filtered;
                        ParentSearchResults.IsVisible = filtered.Count > 0;
                        CreateParentButton.IsVisible = false;
                    }
                    else
                    {
                        ParentSearchResults.IsVisible = false;
                        CreateParentButton.Text = $"Create \"{query}\" as parent product";
                        CreateParentButton.IsVisible = true;
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private async void OnParentProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ParentProductSearchResultDto selected) return;

        ParentSearchResults.SelectedItem = null;
        ParentSearchResults.IsVisible = false;
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;

        Console.WriteLine($"[TaskWizard] Parent selected: Source={selected.Source}, Id={selected.Id}, MasterProductId={selected.MasterProductId}, Name={selected.Name}");

        if (selected.Source == "master" && selected.MasterProductId.HasValue)
        {
            Console.WriteLine($"[TaskWizard] Calling EnsureProductFromMasterAsync({selected.MasterProductId.Value})");
            var result = await _apiClient.EnsureProductFromMasterAsync(selected.MasterProductId.Value);
            Console.WriteLine($"[TaskWizard] EnsureFromMaster result: Success={result.Success}, HasData={result.Data != null}, Error={result.ErrorMessage}");

            if (result.Success && result.Data != null)
            {
                _selectedParentProductId = result.Data.Id;
                ShowSelectedParent(result.Data.Name);
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to add product from catalog", "OK");
            }
        }
        else
        {
            _selectedParentProductId = selected.Id;
            ShowSelectedParent(selected.Name);
        }
    }

    private void OnClearParentClicked(object? sender, EventArgs e)
    {
        _selectedParentProductId = null;
        _pendingParentName = null;
        SelectedParentBorder.IsVisible = false;
        PendingParentBorder.IsVisible = false;
        ClearParentButton.IsVisible = false;
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;
    }

    private void OnCreateParentClicked(object? sender, EventArgs e)
    {
        var searchText = ParentProductSearch.Text?.Trim();
        if (string.IsNullOrEmpty(searchText)) return;

        _pendingParentName = searchText;
        _selectedParentProductId = null;

        PendingParentLabel.Text = $"Will create: \"{searchText}\"";
        PendingParentBorder.IsVisible = true;
        ClearParentButton.IsVisible = true;
        ParentSearchResults.IsVisible = false;
        CreateParentButton.IsVisible = false;
        SelectedParentBorder.IsVisible = false;
        _suppressParentSearch = true;
        ParentProductSearch.Text = string.Empty;
        _suppressParentSearch = false;
    }

    private void ShowSelectedParent(string name)
    {
        _pendingParentName = null;
        PendingParentBorder.IsVisible = false;
        SelectedParentLabel.Text = name;
        SelectedParentBorder.IsVisible = true;
        ClearParentButton.IsVisible = true;
    }

    /// <summary>
    /// Creates the pending parent product using the current product's attributes.
    /// Returns the new parent product's ID, or null on failure.
    /// </summary>
    private async Task<Guid?> CreatePendingParentProductAsync()
    {
        if (string.IsNullOrEmpty(_pendingParentName)) return null;

        var request = new CreateProductRequest
        {
            Name = _pendingParentName,
            LocationId = LocationPicker.SelectedIndex >= 0
                ? _locations[LocationPicker.SelectedIndex].Id
                : _product?.LocationId,
            QuantityUnitIdPurchase = PurchaseUnitPicker.SelectedIndex >= 0
                ? _quantityUnits[PurchaseUnitPicker.SelectedIndex].Id
                : _product?.QuantityUnitIdPurchase,
            QuantityUnitIdStock = StockUnitPicker.SelectedIndex >= 0
                ? _quantityUnits[StockUnitPicker.SelectedIndex].Id
                : _product?.QuantityUnitIdStock,
            QuantityUnitFactorPurchaseToStock = _product?.QuantityUnitFactorPurchaseToStock ?? 1,
            MinStockAmount = _product?.MinStockAmount ?? 0,
            DefaultBestBeforeDays = _product?.DefaultBestBeforeDays ?? 0,
            TracksBestBeforeDate = _product?.TracksBestBeforeDate ?? true,
            IsActive = true
        };

        var result = await _apiClient.CreateProductAsync(request);
        if (result.Success && result.Data != null)
        {
            return result.Data.Id;
        }

        await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create parent product", "OK");
        return null;
    }

    #endregion

    #region Actions

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_product == null || _currentIndex >= _pendingTodos.Count) return;

        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Product name is required.", "OK");
            return;
        }

        SaveButton.IsEnabled = false;

        try
        {
            // Create pending parent product first if needed
            var parentId = _selectedParentProductId;
            if (_pendingParentName != null)
            {
                parentId = await CreatePendingParentProductAsync();
                if (parentId == null)
                {
                    SaveButton.IsEnabled = true;
                    return;
                }
            }

            var request = new UpdateProductMobileRequest
            {
                Name = name,
                Description = _product.Description,
                LocationId = LocationPicker.SelectedIndex >= 0
                    ? _locations[LocationPicker.SelectedIndex].Id
                    : _product.LocationId,
                QuantityUnitIdPurchase = PurchaseUnitPicker.SelectedIndex >= 0
                    ? _quantityUnits[PurchaseUnitPicker.SelectedIndex].Id
                    : _product.QuantityUnitIdPurchase,
                QuantityUnitIdStock = StockUnitPicker.SelectedIndex >= 0
                    ? _quantityUnits[StockUnitPicker.SelectedIndex].Id
                    : _product.QuantityUnitIdStock,
                QuantityUnitFactorPurchaseToStock = _product.QuantityUnitFactorPurchaseToStock,
                MinStockAmount = _product.MinStockAmount,
                DefaultBestBeforeDays = _product.DefaultBestBeforeDays,
                TracksBestBeforeDate = _product.TracksBestBeforeDate,
                IsActive = _product.IsActive,
                ExpiryWarningDays = _product.ExpiryWarningDays,
                ProductGroupId = _product.ProductGroupId,
                ParentProductId = parentId
            };

            var updateResult = await _apiClient.UpdateProductAsync(_product.Id, request);
            if (!updateResult.Success)
            {
                await DisplayAlert("Error", updateResult.ErrorMessage ?? "Failed to update product", "OK");
                SaveButton.IsEnabled = true;
                return;
            }

            // Mark task complete
            var todo = _pendingTodos[_currentIndex];
            await _apiClient.CompleteTodoItemAsync(todo.Id);
            _completedCount++;

            AdvanceToNext();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnCompleteWithoutChangesClicked(object? sender, EventArgs e)
    {
        if (_currentIndex >= _pendingTodos.Count) return;

        var todo = _pendingTodos[_currentIndex];
        var result = await _apiClient.CompleteTodoItemAsync(todo.Id);
        if (result.Success)
        {
            _completedCount++;
            AdvanceToNext();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to complete task", "OK");
        }
    }

    private void OnSkipClicked(object? sender, EventArgs e)
    {
        if (_currentIndex >= _pendingTodos.Count) return;

        // Move to next without completing
        _currentIndex++;
        _ = ShowCurrentTaskAsync();
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            _ = ShowCurrentTaskAsync();
        }
    }

    private async void OnEditFullDetailsClicked(object? sender, EventArgs e)
    {
        if (_product == null) return;

        await Shell.Current.GoToAsync(nameof(ProductEditPage),
            new Dictionary<string, object> { ["ProductId"] = _product.Id.ToString() });
    }

    private void AdvanceToNext()
    {
        // Remove the completed task from the list
        if (_currentIndex < _pendingTodos.Count)
        {
            _pendingTodos.RemoveAt(_currentIndex);
        }

        // If index is now beyond the list, show celebration
        if (_currentIndex >= _pendingTodos.Count)
        {
            if (_pendingTodos.Count == 0)
            {
                ShowCelebration();
            }
            else
            {
                // Wrap to the end of remaining tasks
                _currentIndex = _pendingTodos.Count - 1;
                _ = ShowCurrentTaskAsync();
            }
        }
        else
        {
            _ = ShowCurrentTaskAsync();
        }
    }

    private async void OnBackToDashboardClicked(object? sender, EventArgs e)
    {
        SendTasksChangedMessage();
        await Shell.Current.GoToAsync("//DashboardPage");
    }

    #endregion

    #region View States

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        WizardContent.IsVisible = false;
        EmptyState.IsVisible = false;
        CelebrationOverlay.IsVisible = false;
    }

    private void ShowWizard()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        WizardContent.IsVisible = true;
        EmptyState.IsVisible = false;
        CelebrationOverlay.IsVisible = false;
    }

    private void ShowEmpty()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        WizardContent.IsVisible = false;
        EmptyState.IsVisible = true;
        CelebrationOverlay.IsVisible = false;
    }

    private void ShowCelebration()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CelebrationCountLabel.Text = _completedCount == 1
                ? "You completed 1 task"
                : $"You completed {_completedCount} tasks";
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            WizardContent.IsVisible = false;
            EmptyState.IsVisible = false;
            CelebrationOverlay.IsVisible = true;
        });

        SendTasksChangedMessage();
    }

    #endregion

    private void SendTasksChangedMessage()
    {
        WeakReferenceMessenger.Default.Send(new TasksChangedMessage(_pendingTodos.Count));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _parentSearchDebounce?.Cancel();
    }
}
