using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Equipment;

[QueryProperty(nameof(EquipmentId), "EquipmentId")]
public partial class EquipmentDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private EquipmentDetailItem? _equipment;

    public string EquipmentId { get; set; } = string.Empty;

    public EquipmentDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadEquipmentAsync();
    }

    private async Task LoadEquipmentAsync()
    {
        if (!Guid.TryParse(EquipmentId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid equipment ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var equipResult = await _apiClient.GetEquipmentAsync(id);
            if (equipResult.Success && equipResult.Data != null)
            {
                _equipment = equipResult.Data;

                // Load sub-data in parallel
                var docsTask = _apiClient.GetEquipmentDocumentsAsync(id);
                var maintenanceTask = _apiClient.GetEquipmentMaintenanceRecordsAsync(id);
                var usageTask = !string.IsNullOrEmpty(_equipment.UsageUnit)
                    ? _apiClient.GetEquipmentUsageLogsAsync(id)
                    : Task.FromResult(ApiResult<List<EquipmentUsageLogItem>>.Ok(new List<EquipmentUsageLogItem>()));

                await Task.WhenAll(docsTask, maintenanceTask, usageTask);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderEquipment();
                    RenderDocuments(docsTask.Result.Success ? docsTask.Result.Data : null);
                    RenderMaintenanceRecords(maintenanceTask.Result.Success ? maintenanceTask.Result.Data : null);

                    if (!string.IsNullOrEmpty(_equipment.UsageUnit))
                    {
                        UsageSection.IsVisible = true;
                        UsageSectionTitle.Text = $"Usage History ({_equipment.UsageUnit})";
                        RenderUsageLogs(usageTask.Result.Success ? usageTask.Result.Data : null);
                    }
                    else
                    {
                        UsageSection.IsVisible = false;
                    }

                    ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(equipResult.ErrorMessage ?? "Failed to load equipment"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderEquipment()
    {
        if (_equipment == null) return;

        TitleLabel.Text = _equipment.Name;

        // Category
        if (!string.IsNullOrEmpty(_equipment.CategoryName))
        {
            CategoryLabel.Text = _equipment.CategoryName;
            CategoryLabel.IsVisible = true;
        }
        else
        {
            CategoryLabel.IsVisible = false;
        }

        // Warranty badge
        if (_equipment.IsWarrantyExpired)
        {
            WarrantyBadge.BackgroundColor = Color.FromArgb("#FFEBEE");
            WarrantyBadgeLabel.Text = "WARRANTY EXPIRED";
            WarrantyBadgeLabel.TextColor = Color.FromArgb("#D32F2F");
            WarrantyBadge.IsVisible = true;
        }
        else if (_equipment.WarrantyExpiringSoon)
        {
            WarrantyBadge.BackgroundColor = Color.FromArgb("#FFF3E0");
            WarrantyBadgeLabel.Text = $"WARRANTY EXPIRES IN {_equipment.DaysUntilWarrantyExpires}d";
            WarrantyBadgeLabel.TextColor = Color.FromArgb("#F57C00");
            WarrantyBadge.IsVisible = true;
        }
        else
        {
            WarrantyBadge.IsVisible = false;
        }

        // Description
        if (!string.IsNullOrEmpty(_equipment.Description))
        {
            DescriptionLabel.Text = _equipment.Description;
            DescriptionSection.IsVisible = true;
        }
        else
        {
            DescriptionSection.IsVisible = false;
        }

        // Details card
        DetailsStack.Children.Clear();
        AddDetailRow("Location", _equipment.Location);
        AddDetailRow("Manufacturer", _equipment.Manufacturer);
        AddDetailRow("Model", _equipment.ModelNumber);
        AddDetailRow("Serial #", _equipment.SerialNumber);
        AddDetailRow("Purchase Date", _equipment.PurchaseDate?.ToLocalTime().ToString("MMM d, yyyy"));
        AddDetailRow("Purchase Location", _equipment.PurchaseLocation);
        if (!string.IsNullOrEmpty(_equipment.UsageUnit))
            AddDetailRow("Usage Unit", _equipment.UsageUnit);

        // Warranty card
        var hasWarrantyInfo = _equipment.WarrantyExpirationDate.HasValue || !string.IsNullOrEmpty(_equipment.WarrantyContactInfo);
        WarrantySection.IsVisible = hasWarrantyInfo;
        if (hasWarrantyInfo)
        {
            // Clear all except the title label
            while (WarrantyStack.Children.Count > 1)
                WarrantyStack.Children.RemoveAt(WarrantyStack.Children.Count - 1);

            if (_equipment.WarrantyExpirationDate.HasValue)
                AddDetailRowTo(WarrantyStack, "Expires", _equipment.WarrantyExpirationDate.Value.ToLocalTime().ToString("MMM d, yyyy"));
            if (!string.IsNullOrEmpty(_equipment.WarrantyContactInfo))
                AddDetailRowTo(WarrantyStack, "Contact", _equipment.WarrantyContactInfo);
        }

        // Notes
        if (!string.IsNullOrEmpty(_equipment.Notes))
        {
            NotesLabel.Text = _equipment.Notes;
            NotesSection.IsVisible = true;
        }
        else
        {
            NotesSection.IsVisible = false;
        }
    }

    private void AddDetailRow(string label, string? value)
    {
        AddDetailRowTo(DetailsStack, label, value);
    }

    private static void AddDetailRowTo(VerticalStackLayout stack, string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var row = new HorizontalStackLayout { Spacing = 8 };
        row.Children.Add(new Label
        {
            Text = label,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        });
        row.Children.Add(new Label
        {
            Text = value,
            FontSize = 14,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
            VerticalOptions = LayoutOptions.Center
        });
        stack.Children.Add(row);
    }

    private void RenderDocuments(List<EquipmentDocumentItem>? docs)
    {
        DocumentsList.Children.Clear();

        if (docs == null || docs.Count == 0)
        {
            NoDocumentsLabel.IsVisible = true;
            return;
        }

        NoDocumentsLabel.IsVisible = false;

        foreach (var doc in docs)
        {
            var card = CreateCard();
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var leftStack = new VerticalStackLayout { Spacing = 2 };
            leftStack.Children.Add(new Label
            {
                Text = doc.DisplayLabel,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
            });

            var subtitle = new List<string>();
            if (!string.IsNullOrEmpty(doc.TagName)) subtitle.Add(doc.TagName);
            if (!string.IsNullOrEmpty(doc.FormattedFileSize)) subtitle.Add(doc.FormattedFileSize);
            if (subtitle.Count > 0)
            {
                leftStack.Children.Add(new Label
                {
                    Text = string.Join(" | ", subtitle),
                    FontSize = 11,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#999999") : Color.FromArgb("#888888")
                });
            }

            row.Children.Add(leftStack);
            Grid.SetColumn(leftStack, 0);

            var deleteBtn = new Button
            {
                Text = "X",
                FontSize = 12,
                Padding = new Thickness(8, 2),
                CornerRadius = 12,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#D32F2F"),
                VerticalOptions = LayoutOptions.Center,
                BindingContext = doc
            };
            deleteBtn.Clicked += OnDeleteDocumentClicked;
            row.Children.Add(deleteBtn);
            Grid.SetColumn(deleteBtn, 1);

            card.Content = row;

            var swipe = new SwipeView { Content = card };
            DocumentsList.Children.Add(swipe);
        }
    }

    private void RenderUsageLogs(List<EquipmentUsageLogItem>? logs)
    {
        UsageLogsList.Children.Clear();

        if (logs == null || logs.Count == 0)
        {
            NoUsageLogsLabel.IsVisible = true;
            return;
        }

        NoUsageLogsLabel.IsVisible = false;

        foreach (var log in logs)
        {
            var card = CreateCard();
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            var leftStack = new VerticalStackLayout { Spacing = 2 };
            leftStack.Children.Add(new Label
            {
                Text = log.DateDisplay,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
            });
            if (!string.IsNullOrEmpty(log.Notes))
            {
                leftStack.Children.Add(new Label
                {
                    Text = log.Notes,
                    FontSize = 11,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#999999") : Color.FromArgb("#888888")
                });
            }

            row.Children.Add(leftStack);
            Grid.SetColumn(leftStack, 0);

            var readingLabel = new Label
            {
                Text = log.Reading.ToString("N1"),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1976D2"),
                VerticalOptions = LayoutOptions.Center
            };
            row.Children.Add(readingLabel);
            Grid.SetColumn(readingLabel, 1);

            var deleteBtn = new Button
            {
                Text = "X",
                FontSize = 12,
                Padding = new Thickness(8, 2),
                CornerRadius = 12,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#D32F2F"),
                VerticalOptions = LayoutOptions.Center,
                BindingContext = log
            };
            deleteBtn.Clicked += OnDeleteUsageLogClicked;
            row.Children.Add(deleteBtn);
            Grid.SetColumn(deleteBtn, 2);

            card.Content = row;
            UsageLogsList.Children.Add(card);
        }
    }

    private void RenderMaintenanceRecords(List<EquipmentMaintenanceRecordItem>? records)
    {
        MaintenanceList.Children.Clear();

        if (records == null || records.Count == 0)
        {
            NoMaintenanceLabel.IsVisible = true;
            return;
        }

        NoMaintenanceLabel.IsVisible = false;

        foreach (var record in records)
        {
            var card = CreateCard();
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var leftStack = new VerticalStackLayout { Spacing = 2 };
            leftStack.Children.Add(new Label
            {
                Text = record.Description,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
            });

            var subtitle = record.DateDisplay;
            if (record.UsageAtCompletion.HasValue)
                subtitle += $" | {record.UsageAtCompletion.Value:N1} {_equipment?.UsageUnit ?? "units"}";
            leftStack.Children.Add(new Label
            {
                Text = subtitle,
                FontSize = 11,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#999999") : Color.FromArgb("#888888")
            });

            if (!string.IsNullOrEmpty(record.Notes))
            {
                leftStack.Children.Add(new Label
                {
                    Text = record.Notes,
                    FontSize = 11,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#999999") : Color.FromArgb("#888888"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 2
                });
            }

            row.Children.Add(leftStack);
            Grid.SetColumn(leftStack, 0);

            var deleteBtn = new Button
            {
                Text = "X",
                FontSize = 12,
                Padding = new Thickness(8, 2),
                CornerRadius = 12,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#D32F2F"),
                VerticalOptions = LayoutOptions.Center,
                BindingContext = record
            };
            deleteBtn.Clicked += OnDeleteMaintenanceClicked;
            row.Children.Add(deleteBtn);
            Grid.SetColumn(deleteBtn, 1);

            card.Content = row;
            MaintenanceList.Children.Add(card);
        }
    }

    private static Border CreateCard() => new()
    {
        Padding = new Thickness(12, 8),
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
        Stroke = Colors.Transparent,
        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
    };

    #region Event Handlers

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_equipment == null) return;
        await Shell.Current.GoToAsync(nameof(EquipmentEditPage),
            new Dictionary<string, object> { ["EquipmentId"] = _equipment.Id.ToString() });
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_equipment == null) return;

        var confirmed = await DisplayAlert("Delete Equipment",
            $"Are you sure you want to delete \"{_equipment.Name}\"? This will also delete all associated documents.", "Delete", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DeleteEquipmentAsync(_equipment.Id);
        if (result.Success)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete equipment", "OK");
        }
    }

    private async void OnAddDocumentClicked(object? sender, EventArgs e)
    {
        if (_equipment == null) return;

        try
        {
            var fileResult = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a document",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.pdf", "public.image", "public.plain-text", "com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document" } },
                    { DevicePlatform.Android, new[] { "application/pdf", "image/*", "text/plain", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" } }
                })
            });

            if (fileResult == null) return;

            var stream = await fileResult.OpenReadAsync();
            var result = await _apiClient.UploadEquipmentDocumentAsync(
                _equipment.Id, stream, fileResult.FileName, fileResult.ContentType ?? "application/octet-stream");

            if (result.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to upload document", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to pick file: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteDocumentClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: EquipmentDocumentItem doc })
        {
            var confirmed = await DisplayAlert("Delete Document",
                $"Delete \"{doc.DisplayLabel}\"?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteEquipmentDocumentAsync(doc.Id);
            if (result.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete document", "OK");
            }
        }
    }

    private async void OnAddUsageLogClicked(object? sender, EventArgs e)
    {
        if (_equipment == null) return;

        var popup = new EquipmentUsageLogPopup(_equipment.UsageUnit ?? "units");
        var popupResult = await this.ShowPopupAsync<EquipmentUsageLogPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var logResult = popupResult.Result;

        {
            var apiResult = await _apiClient.AddEquipmentUsageLogAsync(_equipment.Id, new CreateEquipmentUsageLogMobileRequest
            {
                Date = logResult.Date,
                Reading = logResult.Reading,
                Notes = logResult.Notes
            });

            if (apiResult.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add usage log", "OK");
            }
        }
    }

    private async void OnDeleteUsageLogClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: EquipmentUsageLogItem log })
        {
            var confirmed = await DisplayAlert("Delete Reading",
                "Delete this usage reading?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteEquipmentUsageLogAsync(log.Id);
            if (result.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete usage log", "OK");
            }
        }
    }

    private async void OnAddMaintenanceClicked(object? sender, EventArgs e)
    {
        if (_equipment == null) return;

        var popup = new EquipmentMaintenancePopup(_equipment.UsageUnit);
        var popupResult = await this.ShowPopupAsync<EquipmentMaintenancePopupResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var maintenanceResult = popupResult.Result;

        {
            var apiResult = await _apiClient.AddEquipmentMaintenanceRecordAsync(_equipment.Id, new CreateEquipmentMaintenanceRecordMobileRequest
            {
                Description = maintenanceResult.Description,
                CompletedDate = maintenanceResult.CompletedDate,
                UsageAtCompletion = maintenanceResult.UsageAtCompletion,
                Notes = maintenanceResult.Notes,
                CreateReminder = maintenanceResult.CreateReminder,
                ReminderName = maintenanceResult.ReminderName,
                ReminderDueDate = maintenanceResult.ReminderDueDate
            });

            if (apiResult.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add maintenance record", "OK");
            }
        }
    }

    private async void OnDeleteMaintenanceClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: EquipmentMaintenanceRecordItem record })
        {
            var confirmed = await DisplayAlert("Delete Record",
                $"Delete \"{record.Description}\"?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteEquipmentMaintenanceRecordAsync(record.Id);
            if (result.Success)
            {
                await LoadEquipmentAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete maintenance record", "OK");
            }
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadEquipmentAsync();
    }

    #endregion

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
        ErrorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }
}
