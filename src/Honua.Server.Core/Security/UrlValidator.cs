// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Net;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Validates URLs to prevent Server-Side Request Forgery (SSRF) attacks.
/// Blocks access to private IP ranges, localhost, and other internal resources.
/// </summary>
public static class UrlValidator
{
    private static readonly string[] AllowedSchemes = { "http", "https" };

    /// <summary>
    /// Validates that a URL is safe to fetch from external sources.
    /// Blocks private IPs, localhost, internal domains, and non-HTTP(S) schemes.
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>True if the URL is safe to use, false otherwise</returns>
    public static bool IsUrlSafe(string? url)
    {
        if (url.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Only allow HTTP/HTTPS
        if (!AllowedSchemes.Contains(uri.Scheme.ToLowerInvariant()))
        {
            return false;
        }

        // Block private IP ranges (RFC 1918, RFC 4193, loopback)
        var host = uri.Host;
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsPrivateOrReservedIp(ip))
            {
                return false;
            }
        }

        // Block localhost, internal domains
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if an IP address is in a private or reserved range.
    /// </summary>
    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        // Convert to bytes for comparison
        var addressBytes = ip.GetAddressBytes();

        // IPv4 checks
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8 (RFC 1918)
            if (addressBytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12 (RFC 1918)
            if (addressBytes[0] == 172 && (addressBytes[1] >= 16 && addressBytes[1] <= 31))
            {
                return true;
            }

            // 192.168.0.0/16 (RFC 1918)
            if (addressBytes[0] == 192 && addressBytes[1] == 168)
            {
                return true;
            }

            // 127.0.0.0/8 (Loopback)
            if (addressBytes[0] == 127)
            {
                return true;
            }

            // 169.254.0.0/16 (Link-local / APIPA)
            if (addressBytes[0] == 169 && addressBytes[1] == 254)
            {
                return true;
            }

            // 0.0.0.0/8 (Current network)
            if (addressBytes[0] == 0)
            {
                return true;
            }

            // 100.64.0.0/10 (Shared Address Space / Carrier-grade NAT - RFC 6598)
            if (addressBytes[0] == 100 && (addressBytes[1] >= 64 && addressBytes[1] <= 127))
            {
                return true;
            }

            // 192.0.0.0/24 (IETF Protocol Assignments)
            if (addressBytes[0] == 192 && addressBytes[1] == 0 && addressBytes[2] == 0)
            {
                return true;
            }

            // 192.0.2.0/24 (Documentation - TEST-NET-1)
            if (addressBytes[0] == 192 && addressBytes[1] == 0 && addressBytes[2] == 2)
            {
                return true;
            }

            // 198.18.0.0/15 (Benchmarking)
            if (addressBytes[0] == 198 && (addressBytes[1] == 18 || addressBytes[1] == 19))
            {
                return true;
            }

            // 198.51.100.0/24 (Documentation - TEST-NET-2)
            if (addressBytes[0] == 198 && addressBytes[1] == 51 && addressBytes[2] == 100)
            {
                return true;
            }

            // 203.0.113.0/24 (Documentation - TEST-NET-3)
            if (addressBytes[0] == 203 && addressBytes[1] == 0 && addressBytes[2] == 113)
            {
                return true;
            }

            // 224.0.0.0/4 (Multicast)
            if (addressBytes[0] >= 224 && addressBytes[0] <= 239)
            {
                return true;
            }

            // 240.0.0.0/4 (Reserved for future use)
            if (addressBytes[0] >= 240)
            {
                return true;
            }
        }

        // IPv6 checks
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // ::1 (Loopback)
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            // fe80::/10 (Link-local)
            if (addressBytes[0] == 0xfe && (addressBytes[1] & 0xc0) == 0x80)
            {
                return true;
            }

            // fc00::/7 (Unique Local Addresses - RFC 4193)
            if ((addressBytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }

            // ff00::/8 (Multicast)
            if (addressBytes[0] == 0xff)
            {
                return true;
            }

            // :: (Unspecified)
            if (ip.Equals(IPAddress.IPv6Any))
            {
                return true;
            }
        }

        return false;
    }
}
