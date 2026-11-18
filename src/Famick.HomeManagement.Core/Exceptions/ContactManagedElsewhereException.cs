namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Thrown when someone changes a detail on a contact whose details another household keeps up
/// to date. Notes, tags, relationships and who sees the contact stay theirs to change.
/// </summary>
public class ContactManagedElsewhereException : BusinessRuleViolationException
{
    public string ManagedByHousehold { get; }

    public ContactManagedElsewhereException(string managedByHousehold)
        : base("ContactManagedElsewhere",
            $"{managedByHousehold} keeps these details up to date. You can still change notes, tags, relationships and who sees this contact.")
    {
        ManagedByHousehold = managedByHousehold;
    }
}
