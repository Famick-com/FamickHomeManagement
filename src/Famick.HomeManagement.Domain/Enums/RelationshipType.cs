namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Types of relationships between contacts
/// </summary>
public enum RelationshipType
{
    // Family - Parent/Child
    Mother = 1,
    Father = 2,
    Parent = 3,
    Daughter = 4,
    Son = 5,
    Child = 6,

    // Family - Siblings
    Sister = 10,
    Brother = 11,
    Sibling = 12,

    // Family - Extended
    Grandmother = 20,
    Grandfather = 21,
    Grandparent = 22,
    Granddaughter = 23,
    Grandson = 24,
    Grandchild = 25,

    Aunt = 30,
    Uncle = 31,
    Niece = 32,
    Nephew = 33,
    Cousin = 34,

    // In-Laws
    MotherInLaw = 40,
    FatherInLaw = 41,
    SisterInLaw = 42,
    BrotherInLaw = 43,
    DaughterInLaw = 44,
    SonInLaw = 45,
    SiblingInLaw = 46,

    // Partners
    Spouse = 50,
    Partner = 51,
    ExSpouse = 52,
    ExPartner = 53,

    // Step-Family
    Stepmother = 60,
    Stepfather = 61,
    Stepparent = 62,
    Stepdaughter = 63,
    Stepson = 64,
    Stepchild = 65,
    Stepsister = 66,
    Stepbrother = 67,
    Stepsibling = 68,

    // Professional
    Colleague = 70,
    Boss = 71,
    Manager = 72,
    Employee = 73,
    Client = 74,
    Vendor = 75,

    // Social
    Friend = 80,
    Neighbor = 81,
    Roommate = 82,
    Classmate = 83,

    // Other
    Other = 99
}
