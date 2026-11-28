namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// A barcode scanned in a shopping session, for shoppinglists/{id}/scan.
/// </summary>
public class ScanToListRequest
{
    /// <summary>
    /// The scanned barcode, as read
    /// </summary>
    public string Barcode { get; set; } = string.Empty;
}
