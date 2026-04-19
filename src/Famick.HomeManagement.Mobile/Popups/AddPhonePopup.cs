using Famick.HomeManagement.Mobile.Models;
using Syncfusion.Maui.Popup;

namespace Famick.HomeManagement.Mobile.Popups;

public static class AddPhonePopup
{
    public static Task<AddPhoneResult?> ShowAsync(Page page)
    {
        var tcs = new TaskCompletionSource<AddPhoneResult?>();
        AddPhoneForm? capturedForm = null;

        var popup = new SfPopup
        {
            ShowHeader = false,
            ShowFooter = false,
            ShowCloseButton = false,
            StaysOpen = true,
            AutoSizeMode = Syncfusion.Maui.Popup.PopupAutoSizeMode.Both,
            WidthRequest = 360,
            PopupStyle = new PopupStyle
            {
                CornerRadius = 16,
                PopupBackground = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1E1E1E")
                    : Colors.White,
                MessageBackground = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1E1E1E")
                    : Colors.White
            }
        };

        popup.ContentTemplate = new DataTemplate(() =>
        {
            var form = new AddPhoneForm();
            capturedForm = form;

            form.Submitted += (_, result) =>
            {
                tcs.TrySetResult(result);
                popup.IsOpen = false;
            };
            form.Cancelled += (_, _) =>
            {
                tcs.TrySetResult(null);
                popup.IsOpen = false;
            };

            return form;
        });

        popup.Closed += (_, _) =>
        {
            tcs.TrySetResult(null);
            RemoveFromParent(popup);
        };

        AddToPage(page, popup);
        popup.IsOpen = true;

        return tcs.Task;
    }

    private static void AddToPage(Page page, SfPopup popup)
    {
        if (page is ContentPage contentPage)
        {
            if (contentPage.Content is Layout layout)
            {
                layout.Children.Add(popup);
                return;
            }

            var wrapper = new Grid();
            var existing = contentPage.Content;
            contentPage.Content = wrapper;
            if (existing != null) wrapper.Children.Add(existing);
            wrapper.Children.Add(popup);
        }
    }

    private static void RemoveFromParent(SfPopup popup)
    {
        if (popup.Parent is Layout layout)
        {
            layout.Children.Remove(popup);
        }
    }
}
