using Syncfusion.Maui.ImageEditor;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfileImageCropPage : ContentPage
{
    private readonly TaskCompletionSource<Stream?> _tcs = new();
    private string? _tempFilePath;

    public Task<Stream?> CropResultTask => _tcs.Task;

    public ProfileImageCropPage(Stream imageStream)
    {
        InitializeComponent();

        // Save to temp file -- SfImageEditor works more reliably with file-based sources
        _tempFilePath = Path.Combine(FileSystem.CacheDirectory, $"crop_{Guid.NewGuid()}.jpg");
        using (var fs = File.Create(_tempFilePath))
        {
            imageStream.CopyTo(fs);
        }

        ImageEditor.Source = ImageSource.FromFile(_tempFilePath);
    }

    private void OnImageLoaded(object? sender, EventArgs e)
    {
        // Enter circle crop mode automatically
        ImageEditor.Crop(ImageCropType.Circle);
    }

    private async void OnUsePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            // Apply the crop
            ImageEditor.SaveEdits();

            // Get the cropped image as a stream
            var stream = await ImageEditor.GetImageStream();
            _tcs.TrySetResult(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProfileImageCropPage] Error saving crop: {ex.Message}");
            _tcs.TrySetResult(null);
        }

        await Navigation.PopModalAsync();
        CleanupTempFile();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
        CleanupTempFile();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        CleanupTempFile();
        return base.OnBackButtonPressed();
    }

    private void CleanupTempFile()
    {
        try { if (_tempFilePath != null) File.Delete(_tempFilePath); } catch { }
    }
}
