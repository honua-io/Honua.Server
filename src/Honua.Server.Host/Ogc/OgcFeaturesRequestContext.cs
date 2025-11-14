// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Encapsulates HTTP request context for feature item queries.
/// Groups request-related parameters for cleaner method signatures.
/// </summary>
public sealed record OgcFeaturesRequestContext
{
    /// <summary>
    /// The HTTP request object with headers, cookies, and connection info.
    /// Used for content negotiation, link building, and authorization.
    /// </summary>
    public required HttpRequest Request { get; init; }

    /// <summary>
    /// Optional query parameter overrides (used for internal routing).
    /// When specified, these take precedence over the request's query string.
    /// Commonly used for search endpoints to inject additional filters.
    /// </summary>
    public IQueryCollection? QueryOverrides { get; init; }
}
