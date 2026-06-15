using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Resolves a Home Assistant ingress identity to a local Famick <see cref="User"/>,
/// creating one (and a linking <c>UserExternalLogin</c> row) on first contact.
/// </summary>
public interface IHaIngressUserResolver
{
    Task<User> ResolveAsync(HaIngressIdentity identity, CancellationToken cancellationToken = default);
}
