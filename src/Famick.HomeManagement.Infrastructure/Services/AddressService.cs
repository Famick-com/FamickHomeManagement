using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class AddressService : IAddressService
{
    private readonly HomeManagementDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAddressAutocompleteProvider _provider;
    private readonly IAddressSuggestionCache _suggestionCache;
    private readonly IAddressHasher _hasher;
    private readonly ILogger<AddressService> _logger;

    public AddressService(
        HomeManagementDbContext db,
        ITenantProvider tenantProvider,
        IAddressAutocompleteProvider provider,
        IAddressSuggestionCache suggestionCache,
        IAddressHasher hasher,
        ILogger<AddressService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _provider = provider;
        _suggestionCache = suggestionCache;
        _hasher = hasher;
        _logger = logger;
    }

    public virtual async Task<List<AddressDto>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        var searchTerm = $"%{query.Trim()}%";
        limit = Math.Clamp(limit, 1, 25);

        var contactAddressIds = _db.ContactAddresses.Select(ca => ca.AddressId);

        var tenantAddressId = await _db.Set<Tenant>()
            .Where(t => t.Id == _tenantProvider.TenantId)
            .Select(t => t.AddressId)
            .FirstOrDefaultAsync(ct);

        var addressQuery = _db.Addresses
            .Where(a => contactAddressIds.Contains(a.Id)
                        || (tenantAddressId != null && a.Id == tenantAddressId));

        var results = await addressQuery
            .Where(a =>
                EF.Functions.ILike(a.AddressLine1 ?? "", searchTerm) ||
                EF.Functions.ILike(a.City ?? "", searchTerm) ||
                EF.Functions.ILike(a.StateProvince ?? "", searchTerm) ||
                EF.Functions.ILike(a.FormattedAddress ?? "", searchTerm))
            .OrderBy(a => a.AddressLine1)
            .Take(limit)
            .ToListAsync(ct);

        return results.Select(TenantMapper.ToAddressDto).ToList();
    }

    public async Task<List<AddressSuggestionDto>> AutocompleteAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        limit = Math.Clamp(limit, 1, 25);

        // Fan out: local DB search and the configured provider run in parallel.
        var localTask = SearchAsync(query, limit, ct);
        var providerTask = SafeProviderAutocompleteAsync(query, limit, ct);

        await Task.WhenAll(localTask, providerTask);

        var local = localTask.Result;
        var external = providerTask.Result;

        var suggestions = new List<AddressSuggestionDto>(local.Count + external.Count);

        // Local hits: ephemeral SuggestionId + real AddressId. Local rows
        // were verified at insert time, so the hasher skips libpostal.
        var localHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in local)
        {
            var hash = await _hasher.ComputeAsync(
                new AddressComponentsInput(a.AddressLine1, a.City, a.StateProvince, a.PostalCode, a.Country),
                AddressProvenance.Verified, ct);
            if (hash != null) localHashes.Add(hash);

            suggestions.Add(new AddressSuggestionDto
            {
                SuggestionId = Guid.NewGuid(),
                AddressId = a.Id,
                Source = "Local",
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                City = a.City,
                StateProvince = a.StateProvince,
                PostalCode = a.PostalCode,
                Country = a.Country,
                FormattedAddress = a.FormattedAddress ?? a.DisplayAddress,
                SecondaryCount = 0
            });
        }

        // External hits: cache each and return the cache GUID as SuggestionId.
        // Provider output is canonical, so hash as Verified.
        foreach (var s in external)
        {
            var hash = await _hasher.ComputeAsync(
                new AddressComponentsInput(s.Line1, s.City, s.State, s.PostalCode, s.Country),
                AddressProvenance.Verified, ct);
            if (hash != null && localHashes.Contains(hash))
                continue; // Deduped against a local result.

            var id = _suggestionCache.Store(s);
            suggestions.Add(new AddressSuggestionDto
            {
                SuggestionId = id,
                AddressId = null,
                Source = _provider.ProviderName,
                AddressLine1 = s.Line1,
                AddressLine2 = s.Line2,
                City = s.City,
                StateProvince = s.State,
                PostalCode = s.PostalCode,
                Country = s.Country,
                FormattedAddress = s.FormattedText,
                SecondaryCount = s.SecondaryCount
            });
        }

        return suggestions;
    }

    public async Task<List<AddressSuggestionDto>?> ExpandSuggestionSecondariesAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var parent = _suggestionCache.TryGet(suggestionId);
        if (parent == null)
        {
            _logger.LogInformation("Address suggestion {SuggestionId} not in cache for secondary expansion", suggestionId);
            return null;
        }

        List<ExternalAddressSuggestion> children;
        try
        {
            children = await _provider.ExpandSecondariesAsync(parent, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider {Provider} secondary expansion failed; returning empty list", _provider.ProviderName);
            children = new();
        }

        var result = new List<AddressSuggestionDto>(children.Count);
        foreach (var child in children)
        {
            var id = _suggestionCache.Store(child);
            result.Add(new AddressSuggestionDto
            {
                SuggestionId = id,
                AddressId = null,
                Source = _provider.ProviderName,
                AddressLine1 = child.Line1,
                AddressLine2 = child.Line2,
                City = child.City,
                StateProvince = child.State,
                PostalCode = child.PostalCode,
                Country = child.Country,
                FormattedAddress = child.FormattedText,
                SecondaryCount = 0
            });
        }
        return result;
    }

    public async Task<AddressDto?> ResolveSuggestionAsync(ResolveAddressSuggestionRequest request, CancellationToken ct = default)
    {
        // 1. First check if the suggestion refers to an address that already exists locally.
        var local = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == request.SuggestionId, ct);
        if (local != null)
        {
            var dto = TenantMapper.ToAddressDto(local);
            // Line 2 is per-contact, never patched onto the shared row. Echo
            // the caller's override back as a UI hint.
            if (!string.IsNullOrWhiteSpace(request.AddressLine2))
                dto.SuggestedLine2 = request.AddressLine2.Trim();
            return dto;
        }

        // 2. Otherwise it should be a cached external suggestion.
        var cached = _suggestionCache.TryGet(request.SuggestionId);
        if (cached == null)
        {
            _logger.LogInformation("Address suggestion {SuggestionId} not found in cache", request.SuggestionId);
            return null;
        }

        var standardizeInput = new ExternalStandardizeInput
        {
            Line1 = cached.Line1,
            Line2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? cached.Line2 : request.AddressLine2,
            City = cached.City,
            State = cached.State,
            PostalCode = cached.PostalCode,
            Country = cached.Country,
            ProviderPlaceId = cached.ProviderPlaceId
        };

        var standardized = await _provider.StandardizeAsync(standardizeInput, ct);
        var fields = BuildFieldsFromSuggestion(cached, request.AddressLine2, standardized);

        // Strip the apt/suite before persistence — Address rows represent
        // the building, not the unit. Surface it on the returned DTO as a
        // UI hint so the mobile control can pre-populate its Apt/Suite
        // combo box.
        var suggestedLine2 = fields.AddressLine2;
        fields.AddressLine2 = null;

        var resolved = await PersistOrReuseAsync(fields, ct);
        resolved.SuggestedLine2 = suggestedLine2;
        return resolved;
    }

    public async Task<AddressDto> StandardizeAndCreateAsync(StandardizeAddressRequest request, CancellationToken ct = default)
    {
        var standardized = await _provider.StandardizeAsync(new ExternalStandardizeInput
        {
            Line1 = request.AddressLine1,
            Line2 = request.AddressLine2,
            City = request.City,
            State = request.StateProvince,
            PostalCode = request.PostalCode,
            Country = request.Country
        }, ct);

        var fields = BuildFieldsFromManual(request, standardized);
        var suggestedLine2 = fields.AddressLine2;
        fields.AddressLine2 = null;

        var resolved = await PersistOrReuseAsync(fields, ct);
        resolved.SuggestedLine2 = suggestedLine2;
        return resolved;
    }

    public async Task<RehashAddressesResult> RehashAddressesAsync(int batchSize, Guid? continueToken, CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 5_000);

        var query = _db.Addresses.OrderBy(a => a.Id).AsQueryable();
        if (continueToken.HasValue)
        {
            query = query.Where(a => a.Id.CompareTo(continueToken.Value) > 0).OrderBy(a => a.Id);
        }

        // Fetch one extra row to detect "has more" without a separate count.
        var rows = await query.Take(batchSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > batchSize;
        var batch = hasMore ? rows.Take(batchSize).ToList() : rows;

        foreach (var addr in batch)
        {
            // Provenance follows the row's ProviderSource — verified rows
            // skip the canonicalizer (already canonical), unverified rows
            // go through libpostal when configured. Falls back to checking
            // ProviderPlaceId for legacy rows written before ProviderSource
            // existed.
            var provenance = string.IsNullOrEmpty(addr.ProviderSource)
                                && string.IsNullOrEmpty(addr.ProviderPlaceId)
                ? AddressProvenance.Unverified
                : AddressProvenance.Verified;
            addr.NormalizedHash = await _hasher.ComputeAsync(
                new AddressComponentsInput(addr.AddressLine1, addr.City, addr.StateProvince, addr.PostalCode, addr.Country),
                provenance, ct);
        }

        await _db.SaveChangesAsync(ct);

        return new RehashAddressesResult(
            Processed: batch.Count,
            NextContinueToken: hasMore ? batch[^1].Id : null,
            HasMore: hasMore);
    }

    private async Task<List<ExternalAddressSuggestion>> SafeProviderAutocompleteAsync(string query, int limit, CancellationToken ct)
    {
        try
        {
            return await _provider.AutocompleteAsync(query, limit, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider {Provider} autocomplete failed; returning local-only suggestions", _provider.ProviderName);
            return new();
        }
    }

    private async Task<AddressDto> PersistOrReuseAsync(AddressFields fields, CancellationToken ct)
    {
        // Components arrived via Smarty/Geoapify resolution — already
        // canonical, so the hasher skips libpostal.
        var hash = await _hasher.ComputeAsync(
            new AddressComponentsInput(fields.AddressLine1, fields.City, fields.StateProvince, fields.PostalCode, fields.Country),
            AddressProvenance.Verified, ct);

        Address? existing = null;
        if (!string.IsNullOrWhiteSpace(fields.ProviderPlaceId))
        {
            existing = await _db.Addresses.FirstOrDefaultAsync(a => a.ProviderPlaceId == fields.ProviderPlaceId, ct);
        }
        if (existing == null && hash != null)
        {
            existing = await _db.Addresses.FirstOrDefaultAsync(a => a.NormalizedHash == hash, ct);
        }

        if (existing != null)
        {
            // Apt/Suite is per-contact; never patch onto the shared row.
            return TenantMapper.ToAddressDto(existing);
        }

        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = fields.AddressLine1,
            AddressLine2 = null, // Building-only by design; apt/suite lives on ContactAddress.
            City = fields.City,
            StateProvince = fields.StateProvince,
            PostalCode = fields.PostalCode,
            Country = fields.Country,
            CountryCode = fields.CountryCode,
            Latitude = fields.Latitude,
            Longitude = fields.Longitude,
            ProviderPlaceId = fields.ProviderPlaceId,
            // PersistOrReuseAsync only runs after Smarty/Geoapify resolution,
            // so the row is verified by the configured provider. Records the
            // provider name so downstream rehash + future smarty_key work
            // can tell who verified each row.
            ProviderSource = _provider.ProviderName,
            FormattedAddress = fields.FormattedAddress,
            NormalizedHash = hash
        };

        _db.Addresses.Add(address);
        await _db.SaveChangesAsync(ct);
        return TenantMapper.ToAddressDto(address);
    }

    private static AddressFields BuildFieldsFromSuggestion(
        ExternalAddressSuggestion cached,
        string? line2Override,
        ExternalStandardizedAddress? standardized)
    {
        var line2 = !string.IsNullOrWhiteSpace(line2Override) ? line2Override.Trim()
                    : !string.IsNullOrWhiteSpace(standardized?.Line2) ? standardized!.Line2
                    : cached.Line2;

        if (standardized != null)
        {
            return new AddressFields
            {
                AddressLine1 = standardized.Line1 ?? cached.Line1,
                AddressLine2 = line2,
                City = standardized.City ?? cached.City,
                StateProvince = standardized.State ?? cached.State,
                PostalCode = standardized.PostalCode ?? cached.PostalCode,
                Country = standardized.Country ?? cached.Country,
                CountryCode = standardized.CountryCode ?? cached.CountryCode,
                Latitude = standardized.Latitude ?? cached.Latitude,
                Longitude = standardized.Longitude ?? cached.Longitude,
                ProviderPlaceId = standardized.ProviderPlaceId ?? cached.ProviderPlaceId,
                FormattedAddress = standardized.FormattedAddress
            };
        }

        return new AddressFields
        {
            AddressLine1 = cached.Line1,
            AddressLine2 = line2,
            City = cached.City,
            StateProvince = cached.State,
            PostalCode = cached.PostalCode,
            Country = cached.Country,
            CountryCode = cached.CountryCode,
            Latitude = cached.Latitude,
            Longitude = cached.Longitude,
            ProviderPlaceId = cached.ProviderPlaceId,
            FormattedAddress = cached.FormattedText
        };
    }

    private static AddressFields BuildFieldsFromManual(StandardizeAddressRequest request, ExternalStandardizedAddress? standardized)
    {
        if (standardized != null)
        {
            return new AddressFields
            {
                AddressLine1 = standardized.Line1 ?? request.AddressLine1?.Trim(),
                AddressLine2 = string.IsNullOrWhiteSpace(standardized.Line2)
                    ? request.AddressLine2?.Trim()
                    : standardized.Line2,
                City = standardized.City ?? request.City?.Trim(),
                StateProvince = standardized.State ?? request.StateProvince?.Trim(),
                PostalCode = standardized.PostalCode ?? request.PostalCode?.Trim(),
                Country = standardized.Country ?? request.Country?.Trim(),
                CountryCode = standardized.CountryCode,
                Latitude = standardized.Latitude,
                Longitude = standardized.Longitude,
                ProviderPlaceId = standardized.ProviderPlaceId,
                FormattedAddress = standardized.FormattedAddress
            };
        }

        var fields = new AddressFields
        {
            AddressLine1 = request.AddressLine1?.Trim(),
            AddressLine2 = request.AddressLine2?.Trim(),
            City = request.City?.Trim(),
            StateProvince = request.StateProvince?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = request.Country?.Trim()
        };
        fields.FormattedAddress = BuildFallbackFormatted(fields);
        return fields;
    }

    private static string? BuildFallbackFormatted(AddressFields f)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.AddressLine1)) parts.Add(f.AddressLine1!);
        if (!string.IsNullOrWhiteSpace(f.AddressLine2)) parts.Add(f.AddressLine2!);

        var csz = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.City)) csz.Add(f.City!);
        if (!string.IsNullOrWhiteSpace(f.StateProvince)) csz.Add(f.StateProvince!);
        if (!string.IsNullOrWhiteSpace(f.PostalCode)) csz.Add(f.PostalCode!);
        if (csz.Count > 0) parts.Add(string.Join(", ", csz));
        if (!string.IsNullOrWhiteSpace(f.Country)) parts.Add(f.Country!);

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private class AddressFields
    {
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? StateProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? ProviderPlaceId { get; set; }
        public string? FormattedAddress { get; set; }
    }
}
