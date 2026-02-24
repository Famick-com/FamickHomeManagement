using Famick.HomeManagement.Core.DTOs.Wizard;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing the onboarding wizard state and household members
/// </summary>
public interface IWizardService
{
    /// <summary>
    /// Gets the complete wizard state for all 5 pages
    /// </summary>
    Task<WizardStateDto> GetWizardStateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets household members for the current tenant
    /// </summary>
    Task<List<HouseholdMemberDto>> GetHouseholdMembersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the current user's contact record
    /// </summary>
    Task<HouseholdMemberDto> SaveCurrentUserContactAsync(
        SaveCurrentUserContactRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a household member
    /// </summary>
    Task<HouseholdMemberDto> AddHouseholdMemberAsync(
        AddHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a household member's relationship
    /// </summary>
    Task<HouseholdMemberDto> UpdateHouseholdMemberAsync(
        Guid contactId,
        UpdateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from the household (unlinks, does not delete)
    /// </summary>
    Task RemoveHouseholdMemberAsync(
        Guid contactId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for duplicate contacts by name
    /// </summary>
    Task<DuplicateContactResultDto> CheckDuplicateContactAsync(
        CheckDuplicateContactRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves household info (page 1: tenant name + address)
    /// </summary>
    Task SaveHouseholdInfoAsync(
        HouseholdInfoDto info,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves home statistics (page 3: property basics + HOA)
    /// </summary>
    Task SaveHomeStatisticsAsync(
        HomeStatisticsDto stats,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves maintenance items (page 4: HVAC + water + safety)
    /// </summary>
    Task SaveMaintenanceItemsAsync(
        MaintenanceItemsDto items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the wizard as complete
    /// </summary>
    Task CompleteWizardAsync(
        CancellationToken cancellationToken = default);
}
