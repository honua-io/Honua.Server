// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Catalog;

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// Services for legacy catalog/service discovery.
/// </summary>
/// <remarks>
/// Provides access to the catalog projection service used by legacy endpoints to resolve
/// service and layer identifiers. The catalog maintains mappings between legacy service/layer
/// IDs and modern OGC-compliant collection identifiers.
/// </remarks>
public sealed record LegacyCatalogServices
{
    /// <summary>
    /// Service for resolving catalog projections.
    /// Used for legacy service/layer lookups to map old-style identifiers
    /// to OGC API-compliant collection identifiers.
    /// </summary>
    public required ICatalogProjectionService Catalog { get; init; }
}
