using System.Net;
using Famick.HomeManagement.Infrastructure.Net;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Net;

public class OutboundAddressPolicyTests
{
    [Theory]
    // Loopback, unspecified, link-local (EC2 + ECS metadata), multicast, broadcast, reserved
    [InlineData("127.0.0.1")]
    [InlineData("127.1.2.3")]
    [InlineData("0.0.0.0")]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.170.2")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("192.0.2.10")]
    [InlineData("198.18.0.1")]
    [InlineData("100.100.100.200")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")]
    // Embedded IPv4 forms of the above
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("2002:7f00:0001::1")]
    [InlineData("::127.0.0.1")]
    public void Refuses_always(string address)
    {
        var ip = IPAddress.Parse(address);

        OutboundAddressPolicy.IsAllowed(ip, allowPrivateNetworks: false).Should().BeFalse();
        OutboundAddressPolicy.IsAllowed(ip, allowPrivateNetworks: true).Should().BeFalse(
            "a self-hosted server allows its LAN, never loopback or metadata");
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.20")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    [InlineData("fd12:3456::1")]
    [InlineData("::ffff:10.0.0.5")]
    [InlineData("64:ff9b::c0a8:0114")]
    public void Refuses_private_networks_unless_they_are_allowed(string address)
    {
        var ip = IPAddress.Parse(address);

        OutboundAddressPolicy.IsAllowed(ip, allowPrivateNetworks: false).Should().BeFalse();
        OutboundAddressPolicy.IsAllowed(ip, allowPrivateNetworks: true).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("142.250.64.78")]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2607:f8b0:4004:800::200e")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("64:ff9b::808:808")]
    public void Allows_public_addresses(string address)
    {
        OutboundAddressPolicy.IsAllowed(IPAddress.Parse(address), allowPrivateNetworks: false).Should().BeTrue();
    }
}
