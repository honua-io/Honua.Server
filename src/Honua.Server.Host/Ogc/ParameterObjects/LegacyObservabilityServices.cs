// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Observability;

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// Observability services for legacy endpoints.
/// </summary>
/// <remarks>
/// Groups cross-cutting concerns related to monitoring, performance tracking, and caching
/// for legacy API endpoints. These services provide:
/// - API usage metrics collection and reporting
/// - HTTP cache header generation and ETag management
///
/// Separating observability concerns into a dedicated parameter object makes it easier to
/// apply consistent monitoring across all legacy endpoints and simplifies testing scenarios
/// where observability may need to be mocked or disabled.
/// </remarks>
public sealed record LegacyObservabilityServices
{
    /// <summary>
    /// Collects and reports API usage metrics.
    /// Tracks request counts, response times, error rates, and other performance indicators.
    /// </summary>
    public required IApiMetrics Metrics { get; init; }

    /// <summary>
    /// Generates cache control headers and ETags for HTTP responses.
    /// Enables efficient caching strategies to reduce server load and improve client performance.
    /// </summary>
    public required OgcCacheHeaderService CacheHeaders { get; init; }
}
