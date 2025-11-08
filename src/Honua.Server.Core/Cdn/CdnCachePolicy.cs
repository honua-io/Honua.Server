// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Cdn;

/// <summary>
/// Defines CDN caching behavior for HTTP responses
/// </summary>
public sealed record CdnCachePolicy
{
    /// <summary>
    /// No caching - always fetch from origin
    /// </summary>
    public static readonly CdnCachePolicy NoCache = new(0, false, mustRevalidate: true);

    /// <summary>
    /// Short-lived cache (5 minutes) - for frequently changing data
    /// </summary>
    public static readonly CdnCachePolicy ShortLived = new(300, true);

    /// <summary>
    /// Medium-lived cache (1 hour) - for moderately dynamic data
    /// </summary>
    public static readonly CdnCachePolicy MediumLived = new(3600, true);

    /// <summary>
    /// Long-lived cache (1 day) - for static or slowly changing data
    /// </summary>
    public static readonly CdnCachePolicy LongLived = new(86400, true);

    /// <summary>
    /// Very long cache (30 days) - for stable datasets
    /// </summary>
    public static readonly CdnCachePolicy VeryLongLived = new(2592000, true);

    /// <summary>
    /// Immutable cache (1 year) - for content that never changes
    /// </summary>
    public static readonly CdnCachePolicy ImmutableContent = new(31536000, true, immutable: true);

    public int MaxAge { get; init; }
    public int? SharedMaxAge { get; init; }
    public bool Public { get; init; }
    public bool Immutable { get; init; }
    public bool MustRevalidate { get; init; }
    public bool NoStore { get; init; }
    public bool NoTransform { get; init; }
    public int? StaleWhileRevalidate { get; init; }
    public int? StaleIfError { get; init; }

    public CdnCachePolicy(
        int maxAge,
        bool isPublic,
        int? sharedMaxAge = null,
        bool immutable = false,
        bool mustRevalidate = false,
        bool noStore = false,
        bool noTransform = true,
        int? staleWhileRevalidate = null,
        int? staleIfError = null)
    {
        MaxAge = maxAge;
        SharedMaxAge = sharedMaxAge;
        Public = isPublic;
        Immutable = immutable;
        MustRevalidate = mustRevalidate;
        NoStore = noStore;
        NoTransform = noTransform;
        StaleWhileRevalidate = staleWhileRevalidate;
        StaleIfError = staleIfError;
    }

    /// <summary>
    /// Converts the policy to a Cache-Control header value
    /// </summary>
    public string ToCacheControlHeader()
    {
        if (NoStore)
        {
            return "no-store, no-cache, must-revalidate";
        }

        var parts = new List<string>();

        if (Public)
        {
            parts.Add("public");
        }
        else
        {
            parts.Add("private");
        }

        parts.Add($"max-age={MaxAge}");

        if (SharedMaxAge.HasValue)
        {
            parts.Add($"s-maxage={SharedMaxAge.Value}");
        }

        if (Immutable)
        {
            parts.Add("immutable");
        }

        if (MustRevalidate)
        {
            parts.Add("must-revalidate");
        }

        if (NoTransform)
        {
            parts.Add("no-transform");
        }

        if (StaleWhileRevalidate.HasValue)
        {
            parts.Add($"stale-while-revalidate={StaleWhileRevalidate.Value}");
        }

        if (StaleIfError.HasValue)
        {
            parts.Add($"stale-if-error={StaleIfError.Value}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Creates a policy from configuration values
    /// </summary>
    public static CdnCachePolicy FromConfiguration(CdnCachePolicyConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Enabled)
        {
            return NoCache;
        }

        return new CdnCachePolicy(
            config.MaxAge,
            config.Public,
            config.SharedMaxAge,
            config.Immutable,
            config.MustRevalidate,
            config.NoStore,
            config.NoTransform,
            config.StaleWhileRevalidate,
            config.StaleIfError
        );
    }

    /// <summary>
    /// Resolves CDN policy from raster dataset definition
    /// </summary>
    public static CdnCachePolicy FromRasterDefinition(Metadata.RasterCdnDefinition cdn)
    {
        ArgumentNullException.ThrowIfNull(cdn);

        if (!cdn.Enabled)
        {
            return NoCache;
        }

        // Use named policy if specified
        if (cdn.Policy.HasValue())
        {
            return cdn.Policy.ToLowerInvariant() switch
            {
                "nocache" => NoCache,
                "shortlived" => ShortLived,
                "mediumlived" => MediumLived,
                "longlived" => LongLived,
                "verylonglived" => VeryLongLived,
                "immutable" => ImmutableContent,
                _ => LongLived // Default for tiles
            };
        }

        // Build custom policy from individual properties
        return new CdnCachePolicy(
            cdn.MaxAge ?? 86400,
            cdn.Public ?? true,
            cdn.SharedMaxAge,
            cdn.Immutable ?? false,
            cdn.MustRevalidate ?? false,
            cdn.NoStore ?? false,
            cdn.NoTransform ?? true,
            cdn.StaleWhileRevalidate,
            cdn.StaleIfError
        );
    }
}

/// <summary>
/// Configuration for CDN cache policy
/// </summary>
public sealed record CdnCachePolicyConfiguration
{
    public bool Enabled { get; init; } = true;
    public int MaxAge { get; init; } = 3600;
    public int? SharedMaxAge { get; init; }
    public bool Public { get; init; } = true;
    public bool Immutable { get; init; }
    public bool MustRevalidate { get; init; }
    public bool NoStore { get; init; }
    public bool NoTransform { get; init; } = true;
    public int? StaleWhileRevalidate { get; init; }
    public int? StaleIfError { get; init; }
}
