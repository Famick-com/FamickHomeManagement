namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class BarcodeScanResultDto
{
    public bool Found { get; set; }
    public Guid? ItemId { get; set; }
    public string? ProductName { get; set; }
    public bool IsChildProduct { get; set; }
    public Guid? ChildProductId { get; set; }
    public string? ChildProductName { get; set; }
    public bool NeedsChildSelection { get; set; }
}
