using System.Globalization;
using Syncfusion.Maui.Core;

namespace Famick.HomeManagement.Mobile.Converters;

/// <summary>
/// Returns AvatarContentType.Custom when the bound ImageSource is non-null/non-empty,
/// otherwise AvatarContentType.Initials. Used to drive SfAvatarView fallback behavior.
/// </summary>
public class ImageSourceToContentTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ImageSource source && !source.IsEmpty)
            return ContentType.Custom;
        return ContentType.Initials;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
