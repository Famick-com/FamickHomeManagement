namespace Famick.HomeManagement.Domain.Interfaces;

/// <summary>
/// Base interface for all entities with GUID primary key
/// </summary>
public interface IEntity
{
    Guid Id { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
