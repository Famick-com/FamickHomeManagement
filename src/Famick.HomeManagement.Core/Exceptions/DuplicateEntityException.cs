namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a duplicate entity
/// </summary>
public class DuplicateEntityException : DomainException
{
    public string EntityType { get; }
    public string FieldName { get; }
    public string FieldValue { get; }

    public DuplicateEntityException(string entityType, string fieldName, string fieldValue)
        : base($"{entityType} with {fieldName} '{fieldValue}' already exists")
    {
        EntityType = entityType;
        FieldName = fieldName;
        FieldValue = fieldValue;
    }
}
