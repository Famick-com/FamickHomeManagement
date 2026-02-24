using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a task item in the TODO system.
/// Used for tracking follow-up actions like setting up new products from shopping,
/// completing product details, equipment maintenance, etc.
/// </summary>
public class TodoItem : BaseEntity, ITenantEntity
{
    /// <summary>
    /// Tenant ID for multi-tenancy support
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The type/category of this task
    /// </summary>
    public TaskType TaskType { get; set; }

    /// <summary>
    /// Date when this TODO item was entered/created
    /// </summary>
    public DateTime DateEntered { get; set; }

    /// <summary>
    /// Reason for creating this TODO item (e.g., "Added to inventory", "New product created")
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// ID of the related entity (ProductId, StockEntryId, EquipmentId, etc.)
    /// </summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>
    /// Type name of the related entity (e.g., "Product", "StockEntry", "Equipment")
    /// </summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// Human-readable description of the task
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// JSON serialized additional context data
    /// </summary>
    public string? AdditionalData { get; set; }

    /// <summary>
    /// Whether this TODO item has been completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Date/time when the item was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Username or identifier of who completed the item
    /// </summary>
    public string? CompletedBy { get; set; }
}
