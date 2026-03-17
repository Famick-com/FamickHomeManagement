namespace Famick.HomeManagement.Core.Messaging.Messages;

/// <summary>
/// Generic message for CRUD operations on entities.
/// Useful for cross-component cache invalidation without tight coupling.
/// </summary>
public sealed class EntityChangedMessage : Message
{
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required ChangeType ChangeType { get; init; }
}

public enum ChangeType { Created, Updated, Deleted }
