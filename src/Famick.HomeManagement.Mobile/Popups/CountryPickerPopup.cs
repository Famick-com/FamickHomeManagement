using Famick.HomeManagement.Shared.PhoneFormatting;
using Syncfusion.Maui.Popup;

namespace Famick.HomeManagement.Mobile.Popups;

public static class CountryPickerPopup
{
    public static Task<CountryPhoneFormat?> ShowAsync(Page page)
    {
        var tcs = new TaskCompletionSource<CountryPhoneFormat?>();
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var bg = isDark ? Color.FromArgb("#1E1E1E") : Colors.White;
        var fg = isDark ? Colors.White : Colors.Black;

        var popup = new SfPopup
        {
            ShowHeader = false,
            ShowFooter = false,
            ShowCloseButton = false,
            StaysOpen = true,
            AutoSizeMode = PopupAutoSizeMode.None,
            WidthRequest = 320,
            HeightRequest = 480,
            PopupStyle = new PopupStyle
            {
                CornerRadius = 16,
                PopupBackground = bg,
                MessageBackground = bg
            }
        };

        popup.ContentTemplate = new DataTemplate(() =>
        {
            var listView = new CollectionView
            {
                ItemsSource = CountryPhoneFormats.All,
                SelectionMode = SelectionMode.Single,
                ItemTemplate = new DataTemplate(() =>
                {
                    var label = new Label
                    {
                        FontSize = 16,
                        VerticalOptions = LayoutOptions.Center,
                        Padding = new Thickness(16, 14),
                        TextColor = fg
                    };
                    label.SetBinding(Label.TextProperty, nameof(CountryPhoneFormat.DisplayName));
                    return label;
                })
            };
            listView.SelectionChanged += (_, args) =>
            {
                if (args.CurrentSelection.FirstOrDefault() is CountryPhoneFormat chosen)
                {
                    tcs.TrySetResult(chosen);
                    popup.IsOpen = false;
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0"),
                TextColor = fg,
                CornerRadius = 20,
                HeightRequest = 44,
                Margin = new Thickness(16, 8, 16, 16)
            };
            cancelButton.Clicked += (_, _) =>
            {
                tcs.TrySetResult(null);
                popup.IsOpen = false;
            };

            var header = new Label
            {
                Text = "Select Country",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                Padding = new Thickness(0, 16, 0, 8),
                TextColor = fg
            };

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto }
                },
                BackgroundColor = bg
            };
            grid.Add(header, 0, 0);
            grid.Add(listView, 0, 1);
            grid.Add(cancelButton, 0, 2);
            return grid;
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
        if (page is ContentPage contentPage && contentPage.Content is Layout layout)
        {
            layout.Children.Add(popup);
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
