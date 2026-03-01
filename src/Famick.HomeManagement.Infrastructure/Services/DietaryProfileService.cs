using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class DietaryProfileService : IDietaryProfileService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<DietaryProfileService> _logger;

    public DietaryProfileService(
        HomeManagementDbContext context,
        ILogger<DietaryProfileService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DietaryProfileDto> GetAsync(Guid contactId, CancellationToken ct = default)
    {
        var contact = await _context.Contacts
            .Include(c => c.Allergens)
            .Include(c => c.DietaryPreferences)
            .FirstOrDefaultAsync(c => c.Id == contactId, ct)
            ?? throw new KeyNotFoundException($"Contact with ID {contactId} not found");

        return MapToDto(contact);
    }

    public async Task<DietaryProfileDto> UpdateAsync(
        Guid contactId, UpdateDietaryProfileRequest request, CancellationToken ct = default)
    {
        var contact = await _context.Contacts
            .Include(c => c.Allergens)
            .Include(c => c.DietaryPreferences)
            .FirstOrDefaultAsync(c => c.Id == contactId, ct)
            ?? throw new KeyNotFoundException($"Contact with ID {contactId} not found");

        contact.DietaryNotes = request.DietaryNotes;

        // Full replacement of allergens
        _context.ContactAllergens.RemoveRange(contact.Allergens);
        contact.Allergens.Clear();
        foreach (var allergenRequest in request.Allergens)
        {
            contact.Allergens.Add(new ContactAllergen
            {
                ContactId = contactId,
                AllergenType = allergenRequest.AllergenType,
                Severity = allergenRequest.Severity
            });
        }

        // Full replacement of dietary preferences
        _context.ContactDietaryPreferences.RemoveRange(contact.DietaryPreferences);
        contact.DietaryPreferences.Clear();
        foreach (var pref in request.DietaryPreferences)
        {
            contact.DietaryPreferences.Add(new ContactDietaryPreference
            {
                ContactId = contactId,
                DietaryPreference = pref
            });
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated dietary profile for contact {ContactId}", contactId);
        return MapToDto(contact);
    }

    private static DietaryProfileDto MapToDto(Contact contact)
    {
        return new DietaryProfileDto
        {
            ContactId = contact.Id,
            DietaryNotes = contact.DietaryNotes,
            Allergens = contact.Allergens.Select(a => new ContactAllergenDto
            {
                Id = a.Id,
                AllergenType = a.AllergenType,
                Severity = a.Severity
            }).ToList(),
            DietaryPreferences = contact.DietaryPreferences.Select(dp => new ContactDietaryPreferenceDto
            {
                Id = dp.Id,
                DietaryPreference = dp.DietaryPreference
            }).ToList()
        };
    }
}
