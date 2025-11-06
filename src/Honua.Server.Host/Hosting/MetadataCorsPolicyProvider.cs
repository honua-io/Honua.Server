// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Hosting;

internal sealed class MetadataCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ILogger<MetadataCorsPolicyProvider> _logger;

    public MetadataCorsPolicyProvider(IMetadataRegistry metadataRegistry, ILogger<MetadataCorsPolicyProvider> logger)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _logger = Guard.NotNull(logger);
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        Guard.NotNull(context);

        MetadataSnapshot snapshot;
        try
        {
            snapshot = await _metadataRegistry.GetSnapshotAsync(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve metadata snapshot while building CORS policy.");
            return new CorsPolicyBuilder().Build();
        }

        var cors = snapshot.Server.Cors;
        if (!cors.Enabled)
        {
            return new CorsPolicyBuilder().Build();
        }

        if (!cors.AllowAnyOrigin && cors.AllowedOrigins.Count == 0)
        {
            _logger.LogDebug("CORS is enabled but no allowed origins are defined in metadata. Requests will be rejected.");
            return new CorsPolicyBuilder().Build();
        }

        var builder = new CorsPolicyBuilder();

        if (cors.AllowAnyOrigin)
        {
            // SECURITY: Cannot use AllowAnyOrigin with AllowCredentials - violates CORS spec
            if (cors.AllowCredentials)
            {
                _logger.LogError(
                    "CORS configuration error for collection '{CollectionName}': " +
                    "Cannot use AllowAnyOrigin with AllowCredentials. This violates the CORS specification. " +
                    "Either set AllowAnyOrigin=false and specify allowed origins, or set AllowCredentials=false.",
                    collectionName);
                throw new InvalidOperationException(
                    $"Invalid CORS configuration for collection '{collectionName}': " +
                    "Cannot use AllowAnyOrigin with AllowCredentials. This combination violates the CORS specification.");
            }
            builder.AllowAnyOrigin();
        }
        else if (cors.AllowedOrigins.Any(o => o.Contains("*", StringComparison.Ordinal)))
        {
            // Handle wildcard origins (e.g., "https://*.example.com")
            builder.SetIsOriginAllowed(origin =>
            {
                foreach (var pattern in cors.AllowedOrigins)
                {
                    if (IsWildcardMatch(origin, pattern))
                    {
                        _logger.LogDebug("CORS: Origin {Origin} matched pattern {Pattern}", origin, pattern);
                        return true;
                    }
                }
                _logger.LogDebug("CORS: Origin {Origin} did not match any allowed patterns", origin);
                return false;
            });
        }
        else
        {
            builder.WithOrigins(cors.AllowedOrigins.ToArray());
        }

        if (cors.AllowAnyHeader)
        {
            builder.AllowAnyHeader();
        }
        else if (cors.AllowedHeaders.Count > 0)
        {
            builder.WithHeaders(cors.AllowedHeaders.ToArray());
        }
        else
        {
            builder.AllowAnyHeader();
        }

        if (cors.AllowAnyMethod)
        {
            builder.AllowAnyMethod();
        }
        else if (cors.AllowedMethods.Count > 0)
        {
            builder.WithMethods(cors.AllowedMethods.ToArray());
        }
        else
        {
            builder.AllowAnyMethod();
        }

        if (cors.ExposedHeaders.Count > 0)
        {
            builder.WithExposedHeaders(cors.ExposedHeaders.ToArray());
        }

        if (cors.AllowCredentials)
        {
            builder.AllowCredentials();
        }
        else
        {
            builder.DisallowCredentials();
        }

        if (cors.MaxAge is { } maxAgeSeconds)
        {
            builder.SetPreflightMaxAge(TimeSpan.FromSeconds(maxAgeSeconds));
        }

        return builder.Build();
    }

    /// <summary>
    /// Checks if an origin matches a wildcard pattern.
    /// Supports patterns like "https://*.example.com" or "*".
    /// </summary>
    private static bool IsWildcardMatch(string origin, string pattern)
    {
        if (pattern == "*")
        {
            return true; // Match all origins
        }

        if (!pattern.Contains("*", StringComparison.Ordinal))
        {
            return string.Equals(origin, pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Convert wildcard pattern to regex
        // Escape regex special characters, then replace \* with .*
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                origin,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100));
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            // Timeout occurred - likely a complex pattern or malicious input
            // Reject the match for security reasons
            return false;
        }
    }
}
