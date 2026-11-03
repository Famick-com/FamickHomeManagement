using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.QuantityUnits;

/// <summary>
/// Data transfer object for quantity unit lookups
/// </summary>
public class QuantityUnitDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NamePlural { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>The standard measure this unit is, so amounts in it convert; null when it has no fixed size.</summary>
    public StandardUnit? StandardUnit { get; set; }
}
