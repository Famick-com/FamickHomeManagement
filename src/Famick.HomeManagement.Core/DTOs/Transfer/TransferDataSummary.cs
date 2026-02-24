namespace Famick.HomeManagement.Core.DTOs.Transfer;

/// <summary>
/// Summary of local data counts per category, shown before starting a transfer.
/// </summary>
public class TransferDataSummary
{
    public int Locations { get; set; }
    public int QuantityUnits { get; set; }
    public int ProductGroups { get; set; }
    public int ShoppingLocations { get; set; }
    public int EquipmentCategories { get; set; }
    public int ContactTags { get; set; }
    public int Contacts { get; set; }
    public int Products { get; set; }
    public int Equipment { get; set; }
    public int Vehicles { get; set; }
    public int Recipes { get; set; }
    public int Chores { get; set; }
    public int ChoreLogs { get; set; }
    public int TodoItems { get; set; }
    public int ShoppingLists { get; set; }
    public int StorageBins { get; set; }
    public int CalendarEvents { get; set; }
    public int StockEntries { get; set; }
}
