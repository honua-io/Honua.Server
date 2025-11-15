// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// Identifies a collection using legacy service/layer identifiers.
/// Used for backward compatibility with pre-OGC API endpoints.
/// </summary>
/// <remarks>
/// Legacy endpoints use the pattern /{serviceId}/collections/{layerId} instead of the
/// OGC-compliant /collections/{collectionId} pattern. This record encapsulates the
/// legacy identifiers to maintain backward compatibility while transitioning to OGC standards.
/// </remarks>
public sealed record LegacyCollectionIdentity
{
    /// <summary>
    /// Legacy service identifier.
    /// Maps to a service in the catalog registry.
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Legacy layer identifier within the service.
    /// Maps to a specific layer/collection within the service.
    /// </summary>
    public required string LayerId { get; init; }
}
