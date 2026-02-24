namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when a circular dependency is detected (e.g., Recipe A includes Recipe B which includes Recipe A)
/// </summary>
public class CircularDependencyException : BusinessRuleViolationException
{
    public CircularDependencyException(string entityType, Guid entityId)
        : base("CircularDependency",
               $"Circular dependency detected for {entityType} with ID {entityId}")
    {
    }
}
