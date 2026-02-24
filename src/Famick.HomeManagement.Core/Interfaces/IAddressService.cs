using Famick.HomeManagement.Core.DTOs.Common;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IAddressService
{
    Task<List<AddressDto>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);
}
