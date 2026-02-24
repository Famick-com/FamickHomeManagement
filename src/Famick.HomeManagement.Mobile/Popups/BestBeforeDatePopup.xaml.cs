using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

/// <summary>
/// Result returned by BestBeforeDatePopup.
/// Null result = cancelled. HasDate=false = skipped (no explicit date).
/// </summary>
public sealed record BestBeforeDateResult(bool HasDate, DateTime? Date);

public partial class BestBeforeDatePopup : Popup<BestBeforeDateResult>
{
    private DateTime _selectedDate;

    public BestBeforeDatePopup(string productName, int defaultBestBeforeDays)
    {
        InitializeComponent();

        ProductNameLabel.Text = productName;

        _selectedDate = defaultBestBeforeDays > 0
            ? DateTime.Today.AddDays(defaultBestBeforeDays)
            : DateTime.Today.AddDays(7);

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        MonthLabel.Text = _selectedDate.ToString("MMM");
        DayLabel.Text = _selectedDate.Day.ToString();
        YearLabel.Text = _selectedDate.Year.ToString();
    }

    private void SetDateClamped(DateTime candidate)
    {
        var min = DateTime.Today;
        var max = DateTime.Today.AddYears(10);
        if (candidate < min) candidate = min;
        if (candidate > max) candidate = max;
        _selectedDate = candidate;
        UpdateLabels();
    }

    private void OnMonthPlus(object? sender, EventArgs e) => SetDateClamped(_selectedDate.AddMonths(1));
    private void OnMonthMinus(object? sender, EventArgs e) => SetDateClamped(_selectedDate.AddMonths(-1));
    private void OnDayPlus(object? sender, EventArgs e) => SetDateClamped(_selectedDate.AddDays(1));
    private void OnDayMinus(object? sender, EventArgs e) => SetDateClamped(_selectedDate.AddDays(-1));
    private void OnYearPlus(object? sender, EventArgs e) => SetDateClamped(_selectedDate.AddYears(1));
    private void OnYearMinus(object? sender, EventArgs e) => SetDateClamped(_selectedDate.AddYears(-1));

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnSkipClicked(object? sender, EventArgs e)
        => await CloseAsync(new BestBeforeDateResult(HasDate: false, Date: null));

    private async void OnConfirmClicked(object? sender, EventArgs e)
        => await CloseAsync(new BestBeforeDateResult(HasDate: true, Date: _selectedDate));
}
