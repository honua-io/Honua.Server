// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.Ogc;

/// <summary>
/// Enumeration of OGC resource types for cache header configuration
/// </summary>
public enum OgcResourceType
{
    /// <summary>
    /// Tile resources (immutable, long-lived cache)
    /// </summary>
    Tile,

    /// <summary>
    /// Metadata resources (collections, landing page, conformance)
    /// </summary>
    Metadata,

    /// <summary>
    /// Feature resources (individual features or feature collections)
    /// </summary>
    Feature,

    /// <summary>
    /// Style definitions
    /// </summary>
    Style,

    /// <summary>
    /// Tile matrix set definitions
    /// </summary>
    TileMatrixSet,

    /// <summary>
    /// API definition (OpenAPI spec)
    /// </summary>
    ApiDefinition,

    /// <summary>
    /// Queryables schema
    /// </summary>
    Queryables
}
