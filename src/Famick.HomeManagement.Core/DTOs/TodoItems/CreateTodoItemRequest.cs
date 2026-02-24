using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.TodoItems;

public class CreateTodoItemRequest
{
    public TaskType TaskType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? Description { get; set; }
    public string? AdditionalData { get; set; }
}
