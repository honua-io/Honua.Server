// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features GeoJSON and feature serialization operations.
/// </summary>
internal interface IOgcFeaturesGeoJsonHandler
{
    /// <summary>
    /// Converts a feature record to a GeoJSON feature object with links and metadata.
    /// </summary>
    object ToFeature(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureRecord record,
        FeatureQuery query,
        FeatureComponents? componentsOverride = null,
        IReadOnlyList<OgcLink>? additionalLinks = null);

    /// <summary>
    /// Builds feature-level links for a GeoJSON feature.
    /// </summary>
    IReadOnlyList<OgcLink> BuildFeatureLinks(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureComponents components,
        IReadOnlyList<OgcLink>? additionalLinks);

    /// <summary>
    /// Parses JSON document from request body with security limits.
    /// Returns null if parsing fails or request is invalid.
    /// </summary>
    Task<JsonDocument?> ParseJsonDocumentAsync(HttpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Enumerates GeoJSON features from a JSON root element.
    /// Supports single features, FeatureCollections, and feature arrays.
    /// </summary>
    IEnumerable<JsonElement> EnumerateGeoJsonFeatures(JsonElement root);

    /// <summary>
    /// Reads attributes from a GeoJSON feature element.
    /// </summary>
    /// <param name="featureElement">The GeoJSON feature element</param>
    /// <param name="layer">The layer definition</param>
    /// <param name="removeId">Whether to remove ID field from attributes</param>
    /// <param name="featureId">Output feature ID extracted from feature</param>
    /// <returns>Dictionary of attributes</returns>
    Dictionary<string, object?> ReadGeoJsonAttributes(
        JsonElement featureElement,
        LayerDefinition layer,
        bool removeId,
        out string? featureId);
}
