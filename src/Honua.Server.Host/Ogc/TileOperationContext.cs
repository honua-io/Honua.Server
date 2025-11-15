// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// HTTP context for tile operations.
/// </summary>
public sealed record TileOperationContext
{
    /// <summary>
    /// The HTTP request with headers and content negotiation.
    /// </summary>
    public required HttpRequest Request { get; init; }
}
