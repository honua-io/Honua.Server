// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Aggregates cross-cutting concerns for request handling.
/// Groups observability, caching, and logging services.
/// </summary>
public sealed record OgcFeatureObservabilityServices
{
    /// <summary>
    /// Collects and reports API usage metrics.
    /// Tracks feature counts, request rates, and performance.
    /// </summary>
    public required IApiMetrics Metrics { get; init; }

    /// <summary>
    /// Generates cache control headers and ETags.
    /// Enables HTTP caching for improved performance.
    /// </summary>
    public required OgcCacheHeaderService CacheHeaders { get; init; }

    /// <summary>
    /// Logs diagnostic information during request processing.
    /// Used for debugging, monitoring, and audit trails.
    /// </summary>
    public required ILogger Logger { get; init; }
}
