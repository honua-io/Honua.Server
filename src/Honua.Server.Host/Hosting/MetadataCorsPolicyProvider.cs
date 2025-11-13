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
    private readonly IMetadataRegistry metadataRegistry;
    private readonly ILogger<MetadataCorsPolicyProvider> logger;

    public MetadataCorsPolicyProvider(IMetadataRegistry metadataRegistry, ILogger<MetadataCorsPolicyProvider> logger)
    {
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.logger = Guard.NotNull(logger);
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        Guard.NotNull(context);

        MetadataSnapshot snapshot;
        try
        {
            snapshot = await this.metadataRegistry.GetSnapshotAsync(context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to resolve metadata snapshot while building CORS policy.");
            return new CorsPolicyBuilder().Build();
        }

        var cors = snapshot.Server.Cors;
        if (!cors.Enabled)
        {
            return new CorsPolicyBuilder().Build();
        }

        if (!cors.AllowAnyOrigin && cors.AllowedOrigins.Count == 0)
        {
            this.logger.LogDebug("CORS is enabled but no allowed origins are defined in metadata. Requests will be rejected.");
            return new CorsPolicyBuilder().Build();
        }

        var builder = new CorsPolicyBuilder();

        if (cors.AllowAnyOrigin)
        {
            // SECURITY: Cannot use AllowAnyOrigin with AllowCredentials - violates CORS spec
            if (cors.AllowCredentials)
            {
                this.logger.LogError(
                    "CORS configuration error: " +
                    "Cannot use AllowAnyOrigin with AllowCredentials. This violates the CORS specification. " +
                    "Either set AllowAnyOrigin=false and specify allowed origins, or set AllowCredentials=false.");
                throw new InvalidOperationException(
                    "Invalid CORS configuration: " +
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
                        this.logger.LogDebug("CORS: Origin {Origin} matched pattern {Pattern}", origin, pattern);
                        return true;
                    }
                }
                this.logger.LogDebug("CORS: Origin {Origin} did not match any allowed patterns", origin);
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
