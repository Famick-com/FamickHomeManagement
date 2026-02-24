using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.TodoItems;

public class UpdateTodoItemRequest
{
    public TaskType? TaskType { get; set; }
    public string? Reason { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? Description { get; set; }
    public string? AdditionalData { get; set; }
}
