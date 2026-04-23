using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Famick.HomeManagement.Mobile.Models;

public partial class ContactGroupEditFormModel : ObservableObject
{
    [ObservableProperty]
    private int contactType;

    [ObservableProperty]
    [property: Display(Name = "Household Name", Prompt = "e.g. Smith Family")]
    [property: Required(ErrorMessage = "Name is required")]
    private string? groupName;

    [ObservableProperty]
    [property: Display(Name = "Website", Prompt = "https://example.com")]
    private string? website;

    [ObservableProperty]
    [property: Display(Name = "Business Category", Prompt = "e.g. Restaurant, Contractor")]
    private string? businessCategory;

    [ObservableProperty]
    [property: Display(Name = "Notes", Prompt = "Optional notes...")]
    private string? notes;

    [ObservableProperty]
    private string? phoneNumber;

    [ObservableProperty]
    [property: Display(Name = "Address Line 1", Prompt = "Address Line 1")]
    private string? addressLine1;

    [ObservableProperty]
    [property: Display(Name = "Address Line 2", Prompt = "Address Line 2 (optional)")]
    private string? addressLine2;

    [ObservableProperty]
    [property: Display(Name = "City", Prompt = "City")]
    private string? city;

    [ObservableProperty]
    [property: Display(Name = "State/Province", Prompt = "State/Province")]
    private string? stateProvince;

    [ObservableProperty]
    [property: Display(Name = "Postal Code", Prompt = "Postal Code")]
    private string? postalCode;

    [ObservableProperty]
    [property: Display(Name = "Country", Prompt = "Country")]
    private string? country;

    [ObservableProperty]
    [property: Display(Name = "First Name", Prompt = "First Name")]
    private string? memberFirstName;

    [ObservableProperty]
    [property: Display(Name = "Last Name", Prompt = "Last Name")]
    private string? memberLastName;

    [ObservableProperty]
    [property: Display(Name = "Email", Prompt = "Email")]
    private string? memberEmail;

    [ObservableProperty]
    private string? memberPhone;
}
