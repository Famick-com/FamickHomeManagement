using Microsoft.Maui.Handlers;
using UIKit;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

public static class DisableAutoFillHandler
{
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("DisableAutoFill", (handler, view) =>
        {
            if (handler.PlatformView is UITextField textField)
            {
                // Disable password autofill by setting content type to empty
                textField.TextContentType = new Foundation.NSString(string.Empty);
            }
        });
    }
}
