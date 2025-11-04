// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Security;

/// <summary>
/// Validates that X-Forwarded-For and other proxy headers come from trusted proxies to prevent header injection attacks.
/// </summary>
/// <remarks>
/// SECURITY: Protects against Host Header Injection and X-Forwarded-For spoofing attacks.
///
/// Attack Scenarios:
/// 1. Rate Limit Bypass: Attacker sets X-Forwarded-For to random IPs to bypass per-IP rate limiting
/// 2. Authentication Bypass: Applications that trust X-Forwarded-For for IP-based authentication
/// 3. Logging Poisoning: Injected headers pollute security logs, hiding attacker's real IP
/// 4. Cache Poisoning: Malicious X-Forwarded-Host headers can poison caches or redirect users
/// 5. SSRF (Server-Side Request Forgery): Manipulated Host headers trick the app into making malicious requests
///
/// Without validation, ANY client can inject these headers. This validator ensures headers are only
/// accepted when the request originates from a configured trusted proxy (e.g., load balancer, CDN).
///
/// Related CWE:
/// - CWE-290: Authentication Bypass by Spoofing
/// - CWE-20: Improper Input Validation
/// - CWE-918: Server-Side Request Forgery (SSRF)
///
/// References:
/// - https://owasp.org/www-project-web-security-testing-guide/latest/4-Web_Application_Security_Testing/07-Input_Validation_Testing/17-Testing_for_Host_Header_Injection
/// - https://portswigger.net/web-security/host-header
/// </remarks>
public sealed class TrustedProxyValidator
{
    private readonly HashSet<IPAddress> _trustedProxies;
    private readonly HashSet<IPNetwork> _trustedNetworks;
    private readonly ILogger<TrustedProxyValidator> _logger;
    private readonly bool _enabled;

    /// <summary>
    /// Creates a new TrustedProxyValidator.
    /// </summary>
    /// <param name="configuration">Application configuration containing trusted proxy settings.</param>
    /// <param name="logger">Logger for audit events.</param>
    public TrustedProxyValidator(IConfiguration configuration, ILogger<TrustedProxyValidator> logger)
    {
        _logger = Guard.NotNull(logger);
        _trustedProxies = new HashSet<IPAddress>();
        _trustedNetworks = new HashSet<IPNetwork>();

        // Load trusted proxies from configuration
        var trustedProxiesConfig = configuration.GetSection("TrustedProxies").Get<string[]>() ?? Array.Empty<string>();
        var trustedNetworksConfig = configuration.GetSection("TrustedProxyNetworks").Get<string[]>() ?? Array.Empty<string>();

        _enabled = trustedProxiesConfig.Length > 0 || trustedNetworksConfig.Length > 0;

        if (!_enabled)
        {
            _logger.LogWarning(
                "SECURITY: No trusted proxies configured. X-Forwarded-For headers will NOT be trusted. " +
                "Configure TrustedProxies or TrustedProxyNetworks in appsettings.json if running behind a reverse proxy.");
            return;
        }

        // Parse individual IP addresses
        foreach (var proxyIp in trustedProxiesConfig)
        {
            if (proxyIp.IsNullOrWhiteSpace())
                continue;

            if (IPAddress.TryParse(proxyIp, out var ipAddress))
            {
                _trustedProxies.Add(ipAddress);
                _logger.LogInformation("Trusted proxy IP registered: {ProxyIP}", ipAddress);
            }
            else
            {
                _logger.LogWarning("Invalid trusted proxy IP address in configuration: {ProxyIP}", proxyIp);
            }
        }

        // Parse CIDR networks
        foreach (var network in trustedNetworksConfig)
        {
            if (network.IsNullOrWhiteSpace())
                continue;

            if (TryParseIPNetwork(network, out var ipNetwork))
            {
                _trustedNetworks.Add(ipNetwork);
                _logger.LogInformation("Trusted proxy network registered: {Network}", network);
            }
            else
            {
                _logger.LogWarning("Invalid trusted proxy network in configuration: {Network}", network);
            }
        }

        if (_trustedProxies.Count == 0 && _trustedNetworks.Count == 0)
        {
            _logger.LogWarning(
                "SECURITY: Trusted proxies configured but all values are invalid. " +
                "X-Forwarded-For headers will NOT be trusted.");
            _enabled = false;
        }
        else
        {
            _logger.LogInformation(
                "Trusted proxy validation enabled: {ProxyCount} IPs, {NetworkCount} networks",
                _trustedProxies.Count,
                _trustedNetworks.Count);
        }
    }

    /// <summary>
    /// Gets whether proxy header validation is enabled.
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// Validates whether a request comes from a trusted proxy.
    /// </summary>
    /// <param name="remoteIpAddress">The remote IP address of the connection.</param>
    /// <returns>True if the connection is from a trusted proxy; otherwise, false.</returns>
    public bool IsTrustedProxy(IPAddress? remoteIpAddress)
    {
        if (!_enabled || remoteIpAddress == null)
            return false;

        // Check exact IP match
        if (_trustedProxies.Contains(remoteIpAddress))
            return true;

        // Check network range match
        foreach (var network in _trustedNetworks)
        {
            if (network.Contains(remoteIpAddress))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Safely extracts the client IP address from X-Forwarded-For header, validating the proxy chain.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The validated client IP address, or the connection IP if validation fails.</returns>
    public string GetClientIpAddress(HttpContext context)
    {
        Guard.NotNull(context);

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            _logger.LogWarning("SECURITY: Remote IP address is null, returning 'unknown'");
            return "unknown";
        }

        // If not from a trusted proxy, return the connection IP directly
        if (!IsTrustedProxy(remoteIp))
        {
            // Log suspicious attempts to inject headers from untrusted sources
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!forwardedFor.IsNullOrEmpty())
            {
                _logger.LogWarning(
                    "SECURITY: X-Forwarded-For header received from untrusted IP {RemoteIP}. " +
                    "Header value '{ForwardedFor}' is IGNORED. This may indicate a header injection attack. " +
                    "Configure TrustedProxies if running behind a reverse proxy.",
                    remoteIp,
                    forwardedFor);
            }

            return remoteIp.ToString();
        }

        // Request comes from trusted proxy - safely parse X-Forwarded-For
        var forwardedForHeader = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (forwardedForHeader.IsNullOrEmpty())
        {
            // No forwarded header from trusted proxy - use proxy IP
            return remoteIp.ToString();
        }

        // X-Forwarded-For can contain multiple IPs: "client, proxy1, proxy2"
        // The leftmost IP is the original client (if the chain is trusted)
        var ips = forwardedForHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ips.Length == 0)
        {
            return remoteIp.ToString();
        }

        // Take the first (leftmost) IP as the client IP
        var clientIpString = ips[0];

        // Validate that it's a valid IP address
        if (!IPAddress.TryParse(clientIpString, out var clientIp))
        {
            _logger.LogWarning(
                "SECURITY: Invalid IP address in X-Forwarded-For header from trusted proxy {TrustedProxy}. " +
                "Header value: '{ForwardedFor}'. Falling back to proxy IP.",
                remoteIp,
                forwardedForHeader);
            return remoteIp.ToString();
        }

        _logger.LogDebug(
            "Client IP extracted from X-Forwarded-For: {ClientIP} (via trusted proxy {ProxyIP})",
            clientIp,
            remoteIp);

        return clientIp.ToString();
    }

    /// <summary>
    /// Validates and extracts the X-Forwarded-Host header from trusted proxies.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The validated host header, or null if not from a trusted proxy.</returns>
    public string? GetForwardedHost(HttpContext context)
    {
        Guard.NotNull(context);

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null || !IsTrustedProxy(remoteIp))
        {
            var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            if (!forwardedHost.IsNullOrEmpty())
            {
                _logger.LogWarning(
                    "SECURITY: X-Forwarded-Host header '{ForwardedHost}' received from untrusted IP {RemoteIP} is IGNORED. " +
                    "This may indicate a host header injection attack.",
                    forwardedHost,
                    remoteIp);
            }
            return null;
        }

        return context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
    }

    private static bool TryParseIPNetwork(string cidr, out IPNetwork network)
    {
        network = default;

        if (cidr.IsNullOrWhiteSpace())
            return false;

        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out var baseAddress))
            return false;

        if (!int.TryParse(parts[1], out var prefixLength))
            return false;

        if (prefixLength < 0 || prefixLength > 128)
            return false;

        // Validate prefix length for address family
        if (baseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && prefixLength > 32)
            return false;

        network = new IPNetwork(baseAddress, prefixLength);
        return true;
    }
}

/// <summary>
/// Represents an IP network with CIDR notation.
/// </summary>
public readonly struct IPNetwork : IEquatable<IPNetwork>
{
    private readonly byte[] _baseAddressBytes;
    private readonly int _prefixLength;

    public IPNetwork(IPAddress baseAddress, int prefixLength)
    {
        Guard.NotNull(baseAddress);

        if (prefixLength < 0 || prefixLength > 128)
            throw new ArgumentOutOfRangeException(nameof(prefixLength), "Prefix length must be between 0 and 128.");

        _baseAddressBytes = baseAddress.GetAddressBytes();
        _prefixLength = prefixLength;
    }

    public bool Contains(IPAddress address)
    {
        Guard.NotNull(address);

        var addressBytes = address.GetAddressBytes();

        // Address families must match
        if (addressBytes.Length != _baseAddressBytes.Length)
            return false;

        // Calculate number of bytes and bits to compare
        var fullBytes = _prefixLength / 8;
        var remainingBits = _prefixLength % 8;

        // Compare full bytes
        for (var i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != _baseAddressBytes[i])
                return false;
        }

        // Compare remaining bits if any
        if (remainingBits > 0 && fullBytes < _baseAddressBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((addressBytes[fullBytes] & mask) != (_baseAddressBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    public bool Equals(IPNetwork other)
    {
        if (_prefixLength != other._prefixLength)
            return false;

        if (_baseAddressBytes.Length != other._baseAddressBytes.Length)
            return false;

        for (var i = 0; i < _baseAddressBytes.Length; i++)
        {
            if (_baseAddressBytes[i] != other._baseAddressBytes[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is IPNetwork other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_prefixLength);
        foreach (var b in _baseAddressBytes)
        {
            hash.Add(b);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(IPNetwork left, IPNetwork right) => left.Equals(right);
    public static bool operator !=(IPNetwork left, IPNetwork right) => !left.Equals(right);
}
