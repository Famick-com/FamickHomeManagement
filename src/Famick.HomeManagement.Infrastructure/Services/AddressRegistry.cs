using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// The only writer of the shared <see cref="Address"/> table (issue #119). Every household
/// that enters the same address points at the same row:
/// <list type="bullet">
/// <item>One row per <see cref="Address.NormalizedHash"/>, enforced by a unique index, so
/// two households saving the same address at once still end up on one row.</item>
/// <item>A row is never edited. A change re-points the reference at another row, and the
/// first row written for an address is the one everybody shares.</item>
/// <item>A row holds only what identifies the address, plus provider fields the server
/// fetched itself. Coordinates, place ids and formatted text sent by a client are never
/// stored, so no household can plant them on a row another household will share.</item>
/// <item>A row is never deleted, not when the last reference moves off it and not when a
/// household is deleted.</item>
/// </list>
/// </summary>
public static class AddressRegistry
{
    public const string HashIndexName = "ix_addresses_normalized_hash";

    /// <summary>
    /// The row for an address a person typed: an existing row with the same components, or
    /// a new one holding just those components.
    /// </summary>
    /// <exception cref="ArgumentException">The identity has no components at all.</exception>
    public static async Task<Address> FindOrCreateAsync(
        this HomeManagementDbContext db, IAddressHasher hasher, AddressIdentity identity, CancellationToken ct)
    {
        if (identity.IsEmpty)
            throw new ArgumentException("An address needs at least one component.", nameof(identity));

        var input = identity.ToHashInput();
        var hash = await hasher.ComputeAsync(input, AddressProvenance.Unverified, ct);
        var existing = await FindByHashAsync(db, hash, ct);

        // Text copied from a provider result hashes without canonicalization, so look for the
        // verified row too when the canonicalizer changed the text.
        if (existing == null)
        {
            var verifiedHash = await hasher.ComputeAsync(input, AddressProvenance.Verified, ct);
            if (verifiedHash != hash)
                existing = await FindByHashAsync(db, verifiedHash, ct);
        }

        return existing ?? await InsertAsync(db, new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = identity.AddressLine1,
            City = identity.City,
            StateProvince = identity.StateProvince,
            PostalCode = identity.PostalCode,
            Country = identity.Country,
            NormalizedHash = hash
        }, ct);
    }

    /// <summary>
    /// The row for an address the server just fetched from a provider. A row already holding
    /// the address wins, provider fields or not: rows are never edited, so a hand-entered row
    /// is not filled in afterwards.
    /// </summary>
    /// <exception cref="ArgumentException">The identity has no components at all.</exception>
    public static async Task<Address> FindOrCreateVerifiedAsync(
        this HomeManagementDbContext db, IAddressHasher hasher, VerifiedAddress verified, CancellationToken ct)
    {
        if (verified.Identity.IsEmpty)
            throw new ArgumentException("An address needs at least one component.", nameof(verified));

        // Only a row this provider produced can match on its place id. Every such row was
        // written here from the provider's own output.
        if (!string.IsNullOrWhiteSpace(verified.ProviderPlaceId))
        {
            var byPlace = await db.Addresses.FirstOrDefaultAsync(
                a => a.ProviderPlaceId == verified.ProviderPlaceId && a.ProviderSource == verified.ProviderSource, ct);
            if (byPlace != null)
                return byPlace;
        }

        var hash = await hasher.ComputeAsync(verified.Identity.ToHashInput(), AddressProvenance.Verified, ct);
        return await FindByHashAsync(db, hash, ct) ?? await InsertAsync(db, new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = verified.Identity.AddressLine1,
            City = verified.Identity.City,
            StateProvince = verified.Identity.StateProvince,
            PostalCode = verified.Identity.PostalCode,
            Country = verified.Identity.Country,
            CountryCode = verified.CountryCode,
            Latitude = verified.Latitude,
            Longitude = verified.Longitude,
            ProviderPlaceId = verified.ProviderPlaceId,
            ProviderSource = verified.ProviderSource,
            FormattedAddress = verified.FormattedAddress,
            NormalizedHash = hash
        }, ct);
    }

    // Every caller rejects an empty identity first, and any other identity hashes.
    private static Task<Address?> FindByHashAsync(HomeManagementDbContext db, string? hash, CancellationToken ct) =>
        hash == null
            ? Task.FromResult<Address?>(null)
            : db.Addresses.FirstOrDefaultAsync(a => a.NormalizedHash == hash, ct);

    /// <summary>
    /// Saves a new row straight away, so a concurrent insert of the same address surfaces
    /// here rather than in the caller's save. Losing that race returns the winner's row.
    /// Inside a caller's transaction EF wraps the save in a savepoint, so the conflict does
    /// not abort the transaction.
    /// </summary>
    private static async Task<Address> InsertAsync(HomeManagementDbContext db, Address row, CancellationToken ct)
    {
        db.Addresses.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException ex) when (IsHashConflict(ex))
        {
            db.Entry(row).State = EntityState.Detached;
            return await db.Addresses.FirstAsync(a => a.NormalizedHash == row.NormalizedHash, ct);
        }
    }

    private static bool IsHashConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == HashIndexName;
}

/// <summary>
/// The components that identify an address and make up its hash. Values are trimmed, and
/// blanks become null, so equal addresses are stored identically.
/// </summary>
public sealed record AddressIdentity
{
    public AddressIdentity(string? addressLine1, string? city, string? stateProvince, string? postalCode, string? country)
    {
        AddressLine1 = Clean(addressLine1);
        City = Clean(city);
        StateProvince = Clean(stateProvince);
        PostalCode = Clean(postalCode);
        Country = Clean(country);
    }

    public string? AddressLine1 { get; }
    public string? City { get; }
    public string? StateProvince { get; }
    public string? PostalCode { get; }
    public string? Country { get; }

    public bool IsEmpty =>
        AddressLine1 == null && City == null && StateProvince == null && PostalCode == null && Country == null;

    internal AddressComponentsInput ToHashInput() =>
        new(AddressLine1, City, StateProvince, PostalCode, Country);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// An address as a provider returned it to the server. Never build one from client input:
/// its coordinates, place id and formatted text are shared with every household.
/// </summary>
public sealed record VerifiedAddress(
    AddressIdentity Identity,
    string ProviderSource,
    string? ProviderPlaceId,
    string? CountryCode,
    double? Latitude,
    double? Longitude,
    string? FormattedAddress);
