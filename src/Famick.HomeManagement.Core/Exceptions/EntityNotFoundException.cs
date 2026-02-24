namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when an entity is not found
/// </summary>
public class EntityNotFoundException : DomainException
{
    public string EntityType { get; }
    public Guid EntityId { get; }

    public EntityNotFoundException(string entityType, Guid entityId)
        : base($"{entityType} with ID {entityId} not found")
    {
        EntityType = entityType;
        EntityId = entityId;
    }
}
