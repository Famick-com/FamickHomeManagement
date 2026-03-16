using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.StorageBins;

[QueryProperty(nameof(StorageBinId), "StorageBinId")]
public partial class StorageBinDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private StorageBinDetailItem? _bin;
    private byte[]? _qrCodeBytes;

    public string StorageBinId { get; set; } = string.Empty;

    public StorageBinDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBinAsync();
    }

    private async Task LoadBinAsync()
    {
        if (!Guid.TryParse(StorageBinId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid storage bin ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var binTask = _apiClient.GetStorageBinAsync(id);
            var photosTask = _apiClient.GetStorageBinPhotosAsync(id);
            var qrTask = _apiClient.GetStorageBinQrCodeAsync(id);

            await Task.WhenAll(binTask, photosTask, qrTask);

            if (binTask.Result.Success && binTask.Result.Data != null)
            {
                _bin = binTask.Result.Data;

                var photos = photosTask.Result.Success ? photosTask.Result.Data : null;
                _qrCodeBytes = qrTask.Result.Success ? qrTask.Result.Data : null;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderBin();
                    RenderPhotos(photos);
                    RenderQrCode();
                    ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(binTask.Result.ErrorMessage ?? "Failed to load storage bin"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderBin()
    {
        if (_bin == null) return;

        ShortCodeLabel.Text = _bin.ShortCode;

        if (!string.IsNullOrEmpty(_bin.Category))
        {
            CategoryLabel.Text = _bin.Category;
            CategoryLabel.IsVisible = true;
        }
        else
        {
            CategoryLabel.IsVisible = false;
        }

        if (!string.IsNullOrEmpty(_bin.Description))
        {
            DescriptionLabel.Text = _bin.Description;
            DescriptionSection.IsVisible = true;
        }
        else
        {
            DescriptionSection.IsVisible = false;
        }

        DetailsStack.Children.Clear();
        AddDetailRow("Location", _bin.LocationName);
        AddDetailRow("Created", _bin.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"));
        AddDetailRow("Updated", _bin.UpdatedAt.ToLocalTime().ToString("MMM d, yyyy"));
        AddDetailRow("Photos", _bin.PhotoCount.ToString());
    }

    private void RenderQrCode()
    {
        if (_qrCodeBytes != null && _qrCodeBytes.Length > 0)
        {
            QrCodeImage.Source = ImageSource.FromStream(() => new MemoryStream(_qrCodeBytes));
        }
    }

    private void RenderPhotos(List<StorageBinPhotoItem>? photos)
    {
        PhotosLayout.Children.Clear();

        if (photos == null || photos.Count == 0)
        {
            NoPhotosLabel.IsVisible = true;
            return;
        }

        NoPhotosLabel.IsVisible = false;

        foreach (var photo in photos)
        {
            var container = new Grid
            {
                WidthRequest = 100,
                HeightRequest = 100,
                Margin = new Thickness(4)
            };

            var image = new Image
            {
                Aspect = Aspect.AspectFill,
                WidthRequest = 100,
                HeightRequest = 100
            };

            // Load image via authenticated HTTP
            _ = LoadPhotoAsync(image, photo);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (_, _) => await OnPhotoTapped(photo);
            image.GestureRecognizers.Add(tapGesture);

            container.Children.Add(image);

            var deleteBtn = new Button
            {
                Text = "X",
                FontSize = 10,
                Padding = new Thickness(4, 1),
                CornerRadius = 10,
                BackgroundColor = Color.FromArgb("#E53935"),
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                BindingContext = photo
            };
            deleteBtn.Clicked += OnDeletePhotoClicked;
            container.Children.Add(deleteBtn);

            PhotosLayout.Children.Add(container);
        }
    }

    private async Task LoadPhotoAsync(Image imageView, StorageBinPhotoItem photo)
    {
        if (string.IsNullOrEmpty(photo.Url)) return;

        var imageSource = await _apiClient.LoadImageAsync(photo.Url);
        if (imageSource != null)
        {
            MainThread.BeginInvokeOnMainThread(() => imageView.Source = imageSource);
        }
    }

    private async Task OnPhotoTapped(StorageBinPhotoItem photo)
    {
        if (string.IsNullOrEmpty(photo.Url)) return;

        try
        {
            var bytes = await _apiClient.DownloadBytesAsync(photo.Url);
            if (bytes == null) return;

            var tempPath = Path.Combine(FileSystem.CacheDirectory, photo.OriginalFileName);
            await File.WriteAllBytesAsync(tempPath, bytes);
            await Launcher.Default.OpenAsync(new OpenFileRequest(
                photo.OriginalFileName,
                new ReadOnlyFile(tempPath, photo.ContentType)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open photo: {ex.Message}", "OK");
        }
    }

    private void AddDetailRow(string label, string? value)
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
        DetailsStack.Children.Add(row);
    }

    #region Event Handlers

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_bin == null) return;
        await Shell.Current.GoToAsync(nameof(StorageBinEditPage),
            new Dictionary<string, object> { ["StorageBinId"] = _bin.Id.ToString() });
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_bin == null) return;

        var confirmed = await DisplayAlert("Delete Storage Bin",
            $"Are you sure you want to delete bin \"{_bin.ShortCode}\"? This will also delete all photos.", "Delete", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DeleteStorageBinAsync(_bin.Id);
        if (result.Success)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete storage bin", "OK");
        }
    }

    private async void OnAddPhotoClicked(object? sender, EventArgs e)
    {
        if (_bin == null) return;

        var action = await DisplayActionSheet("Add Photo", "Cancel", null, "Take Photo", "Choose from Gallery");

        FileResult? fileResult = null;
        try
        {
            if (action == "Take Photo")
            {
                fileResult = await MediaPicker.Default.CapturePhotoAsync();
            }
            else if (action == "Choose from Gallery")
            {
                fileResult = await MediaPicker.Default.PickPhotoAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to access camera/gallery: {ex.Message}", "OK");
            return;
        }

        if (fileResult == null) return;

        try
        {
            var stream = await fileResult.OpenReadAsync();
            var result = await _apiClient.UploadStorageBinPhotoAsync(
                _bin.Id, stream, fileResult.FileName, fileResult.ContentType ?? "image/jpeg");

            if (result.Success)
            {
                await LoadBinAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to upload photo", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to upload photo: {ex.Message}", "OK");
        }
    }

    private async void OnDeletePhotoClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: StorageBinPhotoItem photo })
        {
            var confirmed = await DisplayAlert("Delete Photo",
                "Delete this photo?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteStorageBinPhotoAsync(photo.Id);
            if (result.Success)
            {
                await LoadBinAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete photo", "OK");
            }
        }
    }

    private async void OnSaveQrClicked(object? sender, EventArgs e)
    {
        if (_qrCodeBytes == null || _bin == null) return;

        try
        {
            var fileName = $"qr-{_bin.ShortCode}.png";
            var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(tempPath, _qrCodeBytes);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"QR Code for {_bin.ShortCode}",
                File = new ShareFile(tempPath, "image/png")
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save QR code: {ex.Message}", "OK");
        }
    }

    private async void OnPrintLabelsClicked(object? sender, EventArgs e)
    {
        if (_bin == null) return;

        try
        {
            var popup = new StorageBinLabelPopup(new List<Guid> { _bin.Id });
            var popupResult = await this.ShowPopupAsync<StorageBinLabelPopupResult>(
                popup, PopupOptions.Empty, CancellationToken.None);

            if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null)
                return;

            var labelResult = popupResult.Result;

            var request = new GenerateLabelSheetMobileRequest
            {
                SheetCount = labelResult.SheetCount,
                LabelFormat = labelResult.LabelFormat,
                RepeatToFill = labelResult.RepeatToFill,
                BinIds = new List<Guid> { _bin.Id }
            };

            var apiResult = await _apiClient.GenerateStorageBinLabelSheetAsync(request);
            if (apiResult.Success && apiResult.Data != null)
            {
                var path = Path.Combine(FileSystem.CacheDirectory, $"label-{_bin.ShortCode}.pdf");
                await File.WriteAllBytesAsync(path, apiResult.Data);
                await Launcher.Default.OpenAsync(new OpenFileRequest(
                    "Storage Bin Labels",
                    new ReadOnlyFile(path, "application/pdf")));
            }
            else
            {
                await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to generate labels", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to print labels: {ex.Message}", "OK");
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadBinAsync();
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
