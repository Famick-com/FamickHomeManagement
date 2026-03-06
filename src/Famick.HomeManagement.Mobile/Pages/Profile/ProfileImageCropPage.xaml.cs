using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfileImageCropPage : ContentPage
{
    private readonly SKBitmap _bitmap;
    private readonly TaskCompletionSource<Stream?> _tcs = new();

    // Image transform state
    private float _scale = 1f;
    private float _translateX;
    private float _translateY;
    private bool _initialized;

    // Gesture tracking
    private float _lastPanX;
    private float _lastPanY;
    private float _startScale;
    private float _startTranslateX;
    private float _startTranslateY;

    // Circle dimensions (set during paint)
    private float _circleRadius;
    private float _circleCenterX;
    private float _circleCenterY;

    public Task<Stream?> CropResultTask => _tcs.Task;

    public ProfileImageCropPage(Stream imageStream)
    {
        InitializeComponent();

        _bitmap = LoadBitmapWithOrientation(imageStream);

        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += OnPanUpdated;

        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;

        CanvasView.GestureRecognizers.Add(panGesture);
        CanvasView.GestureRecognizers.Add(pinchGesture);
    }

    private static SKBitmap LoadBitmapWithOrientation(Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        var bitmap = SKBitmap.Decode(codec);

        var origin = codec.EncodedOrigin;
        if (origin == SKEncodedOrigin.Default || origin == SKEncodedOrigin.TopLeft)
            return bitmap;

        var rotated = new SKBitmap(
            origin is SKEncodedOrigin.LeftBottom or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightBottom
                ? bitmap.Height : bitmap.Width,
            origin is SKEncodedOrigin.LeftBottom or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightBottom
                ? bitmap.Width : bitmap.Height);

        using var canvas = new SKCanvas(rotated);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // Mirror horizontal
                canvas.Scale(-1, 1, rotated.Width / 2f, 0);
                break;
            case SKEncodedOrigin.BottomRight: // Rotate 180
                canvas.RotateDegrees(180, rotated.Width / 2f, rotated.Height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft: // Mirror vertical
                canvas.Scale(1, -1, 0, rotated.Height / 2f);
                break;
            case SKEncodedOrigin.LeftTop: // Transpose
                canvas.Translate(0, 0);
                canvas.Scale(1, -1);
                canvas.RotateDegrees(-90);
                break;
            case SKEncodedOrigin.RightTop: // Rotate 90 CW
                canvas.Translate(rotated.Width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // Transverse
                canvas.Translate(rotated.Width, rotated.Height);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftBottom: // Rotate 90 CCW
                canvas.Translate(0, rotated.Height);
                canvas.RotateDegrees(-90);
                break;
        }

        canvas.DrawBitmap(bitmap, 0, 0);
        bitmap.Dispose();
        return rotated;
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lastPanX = 0;
                _lastPanY = 0;
                break;

            case GestureStatus.Running:
                var density = (float)DeviceDisplay.MainDisplayInfo.Density;
                _translateX += ((float)e.TotalX - _lastPanX) * density;
                _translateY += ((float)e.TotalY - _lastPanY) * density;
                _lastPanX = (float)e.TotalX;
                _lastPanY = (float)e.TotalY;
                CanvasView.InvalidateSurface();
                break;
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _startScale = _scale;
                _startTranslateX = _translateX;
                _startTranslateY = _translateY;
                break;

            case GestureStatus.Running:
                var newScale = _startScale * (float)e.Scale;
                newScale = Math.Clamp(newScale, 0.5f, 5f);

                // Zoom toward the circle center
                var scaleRatio = newScale / _scale;
                _translateX = _circleCenterX - scaleRatio * (_circleCenterX - _translateX);
                _translateY = _circleCenterY - scaleRatio * (_circleCenterY - _translateY);
                _scale = newScale;

                CanvasView.InvalidateSurface();
                break;
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Black);

        _circleCenterX = info.Width / 2f;
        _circleCenterY = info.Height / 2f;
        _circleRadius = Math.Min(info.Width, info.Height) * 0.42f;

        // On first paint, fit the image to fill the circle
        if (!_initialized)
        {
            _initialized = true;
            var fillScale = (_circleRadius * 2f) / Math.Min(_bitmap.Width, _bitmap.Height);
            _scale = fillScale;
            _translateX = _circleCenterX - (_bitmap.Width * _scale / 2f);
            _translateY = _circleCenterY - (_bitmap.Height * _scale / 2f);
        }

        // Draw the image
        var destRect = new SKRect(
            _translateX,
            _translateY,
            _translateX + _bitmap.Width * _scale,
            _translateY + _bitmap.Height * _scale);

        canvas.DrawBitmap(_bitmap, destRect);

        // Draw dark overlay outside the circle
        using var overlayPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 160),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.Save();
        var circlePath = new SKPath();
        circlePath.AddCircle(_circleCenterX, _circleCenterY, _circleRadius);
        canvas.ClipPath(circlePath, SKClipOperation.Difference);
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), overlayPaint);
        canvas.Restore();

        // Draw circle border
        using var borderPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(_circleCenterX, _circleCenterY, _circleRadius, borderPaint);
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        Navigation.PopModalAsync();
    }

    private void OnUsePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var croppedStream = CropToCircle();
            _tcs.TrySetResult(croppedStream);
        }
        catch (Exception ex)
        {
            _tcs.TrySetResult(null);
            Console.WriteLine($"[ProfileImageCrop] Crop error: {ex.Message}");
        }

        Navigation.PopModalAsync();
    }

    private Stream CropToCircle()
    {
        const int outputSize = 512;
        var outputRadius = outputSize / 2f;

        using var surface = SKSurface.Create(new SKImageInfo(outputSize, outputSize, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Clip to circle
        var circlePath = new SKPath();
        circlePath.AddCircle(outputRadius, outputRadius, outputRadius);
        canvas.ClipPath(circlePath);

        // Map the visible circle region to the output
        var circleLeft = _circleCenterX - _circleRadius;
        var circleTop = _circleCenterY - _circleRadius;
        var circleDiameter = _circleRadius * 2f;

        // Source rect in bitmap coordinates
        var srcLeft = (circleLeft - _translateX) / _scale;
        var srcTop = (circleTop - _translateY) / _scale;
        var srcSize = circleDiameter / _scale;

        var srcRect = new SKRect(srcLeft, srcTop, srcLeft + srcSize, srcTop + srcSize);
        var dstRect = new SKRect(0, 0, outputSize, outputSize);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        canvas.DrawBitmap(_bitmap, srcRect, dstRect, paint);

        // Encode to PNG
        using var image = surface.Snapshot();
        var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
