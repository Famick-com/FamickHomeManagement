using System.Net;
using System.Net.Sockets;

namespace Famick.HomeManagement.Infrastructure.Net;

/// <summary>
/// Decides which IP addresses a user-supplied URL may reach.
/// </summary>
/// <remarks>
/// Two tiers. Loopback, link-local (which holds the EC2 and ECS metadata endpoints),
/// multicast, reserved and documentation ranges are refused always. Private ranges
/// (RFC 1918, CGNAT, IPv6 ULA) are refused unless <c>allowPrivateNetworks</c> is set,
/// which a self-hosted server does by default so a household can subscribe to a
/// calendar on its own LAN. Addresses embedded in IPv4-mapped, NAT64 and 6to4 IPv6
/// forms are unwrapped and judged as IPv4.
/// </remarks>
public static class OutboundAddressPolicy
{
    // AWS's IPv6 instance metadata endpoint sits inside fc00::/7, so it would otherwise
    // pass whenever private networks are allowed.
    private static readonly IPAddress AwsMetadataV6 = IPAddress.Parse("fd00:ec2::254");

    // Alibaba Cloud's metadata endpoint sits inside CGNAT 100.64/10.
    private static readonly IPAddress AlibabaMetadata = IPAddress.Parse("100.100.100.200");

    public static bool IsAllowed(IPAddress address, bool allowPrivateNetworks)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
                return IsAllowedV4(address.MapToIPv4(), allowPrivateNetworks);

            var b = address.GetAddressBytes();

            // NAT64 well-known prefix 64:ff9b::/96 carries the IPv4 address in the last four bytes.
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b
                && b.AsSpan(4, 8).IndexOfAnyExcept((byte)0) < 0)
                return IsAllowedV4(new IPAddress(b.AsSpan(12, 4)), allowPrivateNetworks);

            // 6to4 2002::/16 carries it in bytes 2-5.
            if (b[0] == 0x20 && b[1] == 0x02)
                return IsAllowedV4(new IPAddress(b.AsSpan(2, 4)), allowPrivateNetworks);

            return IsAllowedV6(address, b, allowPrivateNetworks);
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsAllowedV4(address, allowPrivateNetworks);

        return false;
    }

    private static bool IsAllowedV4(IPAddress address, bool allowPrivateNetworks)
    {
        if (address.Equals(AlibabaMetadata)) return false;

        var b = address.GetAddressBytes();
        switch (b[0])
        {
            case 0:                                    // "this network"
            case 127:                                  // loopback
            case >= 224:                               // multicast, reserved, broadcast
                return false;
            case 169 when b[1] == 254:                 // link-local: EC2/ECS metadata
                return false;
            case 192 when b[1] == 0 && b[2] is 0 or 2: // IETF protocol assignments, TEST-NET-1
                return false;
            case 198 when b[1] is 18 or 19:            // benchmarking
                return false;
            case 198 when b[1] == 51 && b[2] == 100:   // TEST-NET-2
                return false;
            case 203 when b[1] == 0 && b[2] == 113:    // TEST-NET-3
                return false;
        }

        var isPrivate =
            b[0] == 10 ||
            (b[0] == 172 && b[1] is >= 16 and <= 31) ||
            (b[0] == 192 && b[1] == 168) ||
            (b[0] == 100 && b[1] is >= 64 and <= 127); // CGNAT, also Tailscale

        return !isPrivate || allowPrivateNetworks;
    }

    private static bool IsAllowedV6(IPAddress address, byte[] b, bool allowPrivateNetworks)
    {
        if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6Loopback)) return false;
        if (address.Equals(AwsMetadataV6)) return false;
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;

        // Any other address in ::/96 is an old IPv4-compatible form; nothing legitimate uses it.
        if (b.AsSpan(0, 12).IndexOfAnyExcept((byte)0) < 0) return false;

        // Teredo 2001::/32 tunnels to an address we cannot see; documentation 2001:db8::/32.
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00) return false;
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false;

        var isPrivate = (b[0] & 0xfe) == 0xfc;         // unique local fc00::/7
        return !isPrivate || allowPrivateNetworks;
    }
}
