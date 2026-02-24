using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.TodoItems;

public class TodoItemDto
{
    public Guid Id { get; set; }
    public TaskType TaskType { get; set; }
    public string TaskTypeName => TaskType.ToString();
    public DateTime DateEntered { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? Description { get; set; }
    public string? AdditionalData { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
