using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// Maps relationship types to their inverses based on the target's gender
/// </summary>
public static class RelationshipMapper
{
    /// <summary>
    /// Gets the inverse relationship type for a given relationship
    /// </summary>
    /// <param name="type">The source relationship type</param>
    /// <param name="targetGender">The gender of the target contact (who will have the inverse relationship)</param>
    /// <returns>The inverse relationship type, or null if no inverse exists</returns>
    public static RelationshipType? GetInverse(RelationshipType type, Gender targetGender)
    {
        return type switch
        {
            // Parent-Child relationships
            RelationshipType.Mother => targetGender switch
            {
                Gender.Male => RelationshipType.Son,
                Gender.Female => RelationshipType.Daughter,
                _ => RelationshipType.Child
            },
            RelationshipType.Father => targetGender switch
            {
                Gender.Male => RelationshipType.Son,
                Gender.Female => RelationshipType.Daughter,
                _ => RelationshipType.Child
            },
            RelationshipType.Parent => targetGender switch
            {
                Gender.Male => RelationshipType.Son,
                Gender.Female => RelationshipType.Daughter,
                _ => RelationshipType.Child
            },
            RelationshipType.Son => targetGender switch
            {
                Gender.Male => RelationshipType.Father,
                Gender.Female => RelationshipType.Mother,
                _ => RelationshipType.Parent
            },
            RelationshipType.Daughter => targetGender switch
            {
                Gender.Male => RelationshipType.Father,
                Gender.Female => RelationshipType.Mother,
                _ => RelationshipType.Parent
            },
            RelationshipType.Child => targetGender switch
            {
                Gender.Male => RelationshipType.Father,
                Gender.Female => RelationshipType.Mother,
                _ => RelationshipType.Parent
            },

            // Sibling relationships
            RelationshipType.Brother => targetGender switch
            {
                Gender.Male => RelationshipType.Brother,
                Gender.Female => RelationshipType.Sister,
                _ => RelationshipType.Sibling
            },
            RelationshipType.Sister => targetGender switch
            {
                Gender.Male => RelationshipType.Brother,
                Gender.Female => RelationshipType.Sister,
                _ => RelationshipType.Sibling
            },
            RelationshipType.Sibling => targetGender switch
            {
                Gender.Male => RelationshipType.Brother,
                Gender.Female => RelationshipType.Sister,
                _ => RelationshipType.Sibling
            },

            // Grandparent-Grandchild relationships
            RelationshipType.Grandfather => targetGender switch
            {
                Gender.Male => RelationshipType.Grandson,
                Gender.Female => RelationshipType.Granddaughter,
                _ => RelationshipType.Grandchild
            },
            RelationshipType.Grandmother => targetGender switch
            {
                Gender.Male => RelationshipType.Grandson,
                Gender.Female => RelationshipType.Granddaughter,
                _ => RelationshipType.Grandchild
            },
            RelationshipType.Grandparent => targetGender switch
            {
                Gender.Male => RelationshipType.Grandson,
                Gender.Female => RelationshipType.Granddaughter,
                _ => RelationshipType.Grandchild
            },
            RelationshipType.Grandson => targetGender switch
            {
                Gender.Male => RelationshipType.Grandfather,
                Gender.Female => RelationshipType.Grandmother,
                _ => RelationshipType.Grandparent
            },
            RelationshipType.Granddaughter => targetGender switch
            {
                Gender.Male => RelationshipType.Grandfather,
                Gender.Female => RelationshipType.Grandmother,
                _ => RelationshipType.Grandparent
            },
            RelationshipType.Grandchild => targetGender switch
            {
                Gender.Male => RelationshipType.Grandfather,
                Gender.Female => RelationshipType.Grandmother,
                _ => RelationshipType.Grandparent
            },

            // Aunt/Uncle - Niece/Nephew relationships
            RelationshipType.Uncle => targetGender switch
            {
                Gender.Male => RelationshipType.Nephew,
                Gender.Female => RelationshipType.Niece,
                _ => RelationshipType.Nephew
            },
            RelationshipType.Aunt => targetGender switch
            {
                Gender.Male => RelationshipType.Nephew,
                Gender.Female => RelationshipType.Niece,
                _ => RelationshipType.Niece
            },
            RelationshipType.Nephew => targetGender switch
            {
                Gender.Male => RelationshipType.Uncle,
                Gender.Female => RelationshipType.Aunt,
                _ => RelationshipType.Uncle
            },
            RelationshipType.Niece => targetGender switch
            {
                Gender.Male => RelationshipType.Uncle,
                Gender.Female => RelationshipType.Aunt,
                _ => RelationshipType.Aunt
            },

            // Cousin relationship (symmetric)
            RelationshipType.Cousin => RelationshipType.Cousin,

            // Spouse/Partner relationships (symmetric)
            RelationshipType.Spouse => RelationshipType.Spouse,
            RelationshipType.Partner => RelationshipType.Partner,
            RelationshipType.ExSpouse => RelationshipType.ExSpouse,
            RelationshipType.ExPartner => RelationshipType.ExPartner,

            // Social relationships (symmetric)
            RelationshipType.Friend => RelationshipType.Friend,
            RelationshipType.Colleague => RelationshipType.Colleague,
            RelationshipType.Neighbor => RelationshipType.Neighbor,
            RelationshipType.Roommate => RelationshipType.Roommate,
            RelationshipType.Classmate => RelationshipType.Classmate,

            // Professional relationships
            RelationshipType.Manager => RelationshipType.Employee,
            RelationshipType.Boss => RelationshipType.Employee,
            RelationshipType.Employee => RelationshipType.Manager,
            RelationshipType.Client => RelationshipType.Vendor,
            RelationshipType.Vendor => RelationshipType.Client,

            // In-law relationships
            RelationshipType.FatherInLaw => targetGender switch
            {
                Gender.Male => RelationshipType.SonInLaw,
                Gender.Female => RelationshipType.DaughterInLaw,
                _ => RelationshipType.SonInLaw
            },
            RelationshipType.MotherInLaw => targetGender switch
            {
                Gender.Male => RelationshipType.SonInLaw,
                Gender.Female => RelationshipType.DaughterInLaw,
                _ => RelationshipType.DaughterInLaw
            },
            RelationshipType.SonInLaw => targetGender switch
            {
                Gender.Male => RelationshipType.FatherInLaw,
                Gender.Female => RelationshipType.MotherInLaw,
                _ => RelationshipType.FatherInLaw
            },
            RelationshipType.DaughterInLaw => targetGender switch
            {
                Gender.Male => RelationshipType.FatherInLaw,
                Gender.Female => RelationshipType.MotherInLaw,
                _ => RelationshipType.MotherInLaw
            },
            RelationshipType.BrotherInLaw => targetGender switch
            {
                Gender.Male => RelationshipType.BrotherInLaw,
                Gender.Female => RelationshipType.SisterInLaw,
                _ => RelationshipType.BrotherInLaw
            },
            RelationshipType.SisterInLaw => targetGender switch
            {
                Gender.Male => RelationshipType.BrotherInLaw,
                Gender.Female => RelationshipType.SisterInLaw,
                _ => RelationshipType.SisterInLaw
            },

            // Stepfamily relationships
            RelationshipType.Stepfather => targetGender switch
            {
                Gender.Male => RelationshipType.Stepson,
                Gender.Female => RelationshipType.Stepdaughter,
                _ => RelationshipType.Stepson
            },
            RelationshipType.Stepmother => targetGender switch
            {
                Gender.Male => RelationshipType.Stepson,
                Gender.Female => RelationshipType.Stepdaughter,
                _ => RelationshipType.Stepdaughter
            },
            RelationshipType.Stepson => targetGender switch
            {
                Gender.Male => RelationshipType.Stepfather,
                Gender.Female => RelationshipType.Stepmother,
                _ => RelationshipType.Stepfather
            },
            RelationshipType.Stepdaughter => targetGender switch
            {
                Gender.Male => RelationshipType.Stepfather,
                Gender.Female => RelationshipType.Stepmother,
                _ => RelationshipType.Stepmother
            },
            RelationshipType.Stepbrother => targetGender switch
            {
                Gender.Male => RelationshipType.Stepbrother,
                Gender.Female => RelationshipType.Stepsister,
                _ => RelationshipType.Stepbrother
            },
            RelationshipType.Stepsister => targetGender switch
            {
                Gender.Male => RelationshipType.Stepbrother,
                Gender.Female => RelationshipType.Stepsister,
                _ => RelationshipType.Stepsister
            },

            // Other relationship (no automatic inverse)
            RelationshipType.Other => null,

            _ => null
        };
    }

    /// <summary>
    /// Infers gender from a relationship type where the type implies a specific gender.
    /// </summary>
    public static Gender InferGender(RelationshipType type)
    {
        return type switch
        {
            RelationshipType.Mother or RelationshipType.Daughter or RelationshipType.Sister or
            RelationshipType.Grandmother or RelationshipType.Granddaughter or RelationshipType.Aunt or
            RelationshipType.Niece or RelationshipType.MotherInLaw or RelationshipType.SisterInLaw or
            RelationshipType.DaughterInLaw or RelationshipType.Stepmother or RelationshipType.Stepdaughter or
            RelationshipType.Stepsister => Gender.Female,

            RelationshipType.Father or RelationshipType.Son or RelationshipType.Brother or
            RelationshipType.Grandfather or RelationshipType.Grandson or RelationshipType.Uncle or
            RelationshipType.Nephew or RelationshipType.FatherInLaw or RelationshipType.BrotherInLaw or
            RelationshipType.SonInLaw or RelationshipType.Stepfather or RelationshipType.Stepson or
            RelationshipType.Stepbrother => Gender.Male,

            _ => Gender.Unknown
        };
    }
}
