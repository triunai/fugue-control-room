using System.Net;
using System.Net.Sockets;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// The address rules of the SSRF guard (ADR-011): which destinations a webhook may never reach. Pure functions over <see cref="IPAddress"/>, so the
/// subscribe-time check and the connect-time check (DNS rebinding) share one definition. Loopback and private ranges can be allowed for local
/// development; link-local (the cloud metadata address 169.254.169.254 among them), carrier-grade NAT, multicast, reserved and documentation
/// ranges are blocked unconditionally, and so is anything that embeds a blocked IPv4 address (IPv4-mapped, NAT64, 6to4).
/// </summary>
internal static class NetworkGuard
{
    public static bool IsBlocked(IPAddress address, bool allowLocal)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        byte[] b = address.GetAddressBytes();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedV4(b, allowLocal),
            AddressFamily.InterNetworkV6 => IsBlockedV6(b, allowLocal),
            _ => true,
        };
    }

    private static bool IsBlockedV4(byte[] b, bool allowLocal)
    {
        bool loopback = b[0] == 127;
        bool isPrivate = b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
        if (loopback || isPrivate)
        {
            return !allowLocal;
        }

        return b[0] == 0                                    // "this network", 0.0.0.0
            || (b[0] == 169 && b[1] == 254)                 // link-local incl. cloud metadata
            || (b[0] == 100 && b[1] is >= 64 and <= 127)    // carrier-grade NAT
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)      // IETF protocol assignments
            || (b[0] == 192 && b[1] == 0 && b[2] == 2)      // documentation
            || (b[0] == 198 && b[1] is 18 or 19)            // benchmarking
            || (b[0] == 198 && b[1] == 51 && b[2] == 100)   // documentation
            || (b[0] == 203 && b[1] == 0 && b[2] == 113)    // documentation
            || b[0] >= 224;                                 // multicast, reserved, broadcast
    }

    private static bool IsBlockedV6(byte[] b, bool allowLocal)
    {
        if (b[..15].All(x => x == 0) && b[15] == 1)
        {
            return !allowLocal; // ::1
        }

        if ((b[0] & 0xFE) == 0xFC)
        {
            return !allowLocal; // fc00::/7 unique local
        }

        if (b.All(x => x == 0))
        {
            return true; // ::
        }

        if (b[0] == 0xFE && (b[1] & 0xC0) is 0x80 or 0xC0)
        {
            return true; // fe80::/10 link-local, fec0::/10 site-local
        }

        if (b[0] == 0xFF)
        {
            return true; // multicast
        }

        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8)
        {
            return true; // documentation 2001:db8::/32
        }

        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00)
        {
            return true; // Teredo
        }

        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return IsBlockedV4([b[2], b[3], b[4], b[5]], allowLocal: false); // 6to4 embeds an IPv4 address
        }

        bool nat64 = b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && b[4..12].All(x => x == 0);
        bool compatible = b[..12].All(x => x == 0); // deprecated IPv4-compatible ::a.b.c.d
        return (nat64 || compatible) && IsBlockedV4([b[12], b[13], b[14], b[15]], allowLocal: false);
    }
}
