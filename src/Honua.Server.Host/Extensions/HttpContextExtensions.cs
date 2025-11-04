// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Provides extension methods for HttpContext operations.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the client IP address from the HTTP context.
    /// Checks X-Forwarded-For and X-Real-IP headers before falling back to the connection remote IP.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The client IP address as a string, or "unknown" if not available.</returns>
    /// <remarks>
    /// SECURITY NOTE: This method does NOT validate whether proxy headers come from trusted sources.
    /// For security-critical scenarios (rate limiting, IP-based auth), use TrustedProxyValidator.GetClientIpAddress()
    /// which validates proxy headers against configured trusted proxies to prevent header injection attacks.
    ///
    /// This method is suitable for logging and non-security-critical scenarios where you want to capture
    /// the forwarded IP address regardless of the source.
    /// </remarks>
    public static string GetClientIpAddress(this HttpContext context)
    {
        if (context == null)
        {
            return "unknown";
        }

        // Check X-Forwarded-For header first (for reverse proxies)
        // Format: "client, proxy1, proxy2" - leftmost is the original client
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var forwardedIps = forwardedFor.ToString().Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            if (forwardedIps.Length > 0)
            {
                var firstIp = forwardedIps[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    return firstIp;
                }
            }
        }

        // Check X-Real-IP header (nginx proxy)
        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            var ip = realIp.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
