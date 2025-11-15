// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// HTTP request context for legacy endpoints.
/// </summary>
/// <remarks>
/// Encapsulates the HTTP request information needed for legacy API endpoint handling.
/// This includes request headers, query parameters, connection information, and user context.
/// </remarks>
public sealed record LegacyRequestContext
{
    /// <summary>
    /// The HTTP request object containing headers, query parameters, and connection info.
    /// </summary>
    public required HttpRequest Request { get; init; }
}
