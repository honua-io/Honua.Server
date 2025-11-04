// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Provides centralized JSON serialization security configuration to prevent DoS attacks.
///
/// Security Protections:
/// - MaxDepth: Prevents stack overflow from deeply nested JSON (default: 64)
/// - Request body size limits: Prevents memory exhaustion (configured separately in Kestrel)
///
/// These limits protect against:
/// - Stack overflow attacks via deeply nested structures
/// - Memory exhaustion via extremely large payloads
/// - CPU exhaustion via complex deserialization
/// </summary>
public static class JsonSecurityOptions
{
    /// <summary>
    /// Maximum depth for JSON deserialization.
    /// Default is 64 to prevent stack overflow attacks while supporting legitimate use cases.
    /// STAC collections and complex geospatial metadata should not exceed this depth.
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// Creates JSON serializer options with security limits for web APIs.
    /// Applies standard web defaults with added security protections.
    /// </summary>
    /// <param name="writeIndented">Whether to format JSON with indentation (default: false for performance).</param>
    /// <returns>Configured JsonSerializerOptions with security limits.</returns>
    public static JsonSerializerOptions CreateSecureWebOptions(bool writeIndented = false)
    {
        return writeIndented ? JsonSerializerOptionsRegistry.WebIndented : JsonSerializerOptionsRegistry.Web;
    }

    /// <summary>
    /// Creates JSON serializer options with security limits for STAC API endpoints.
    /// Optimized for STAC collections, items, and catalog structures.
    /// </summary>
    /// <returns>Configured JsonSerializerOptions for STAC with security limits.</returns>
    public static JsonSerializerOptions CreateSecureStacOptions()
    {
        // Web already has camelCase from JsonSerializerDefaults.Web
        return JsonSerializerOptionsRegistry.Web;
    }

    /// <summary>
    /// Applies security limits to existing JsonSerializerOptions.
    /// Use this to retrofit security to existing options objects.
    /// </summary>
    /// <param name="options">The options to secure.</param>
    public static void ApplySecurityLimits(JsonSerializerOptions options)
    {
        Guard.NotNull(options);
        options.MaxDepth = MaxDepth;
    }
}
