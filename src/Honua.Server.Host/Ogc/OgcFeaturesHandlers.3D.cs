// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Elevation;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Converts a FeatureRecord to a GeoJSON feature with optional 3D elevation enrichment.
    /// </summary>
    internal static async Task<object> ToFeature3DAsync(
        HttpRequest request,
        string collectionId,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        FeatureQuery query,
        IElevationService elevationService,
        FeatureComponents? componentsOverride = null,
        IReadOnlyList<OgcLink>? additionalLinks = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);
        Guard.NotNull(query);
        Guard.NotNull(elevationService);

        var components = componentsOverride ?? FeatureComponentBuilder.BuildComponents(layer, record, query);

        // If 3D is not requested, use standard 2D feature conversion
        if (!query.Include3D)
        {
            return OgcSharedHandlers.ToFeature(request, collectionId, layer, record, query, components, additionalLinks);
        }

        // Enrich geometry with elevation if 3D is requested
        var enrichedGeometry = components.GeometryNode;
        var enrichedProperties = components.Properties;

        if (components.GeometryNode is not null)
        {
            var elevationConfig = GetElevationConfiguration(layer);
            var context = new ElevationContext
            {
                ServiceId = service.Id,
                LayerId = layer.Id,
                FeatureAttributes = record.Attributes,
                Configuration = elevationConfig
            };

            // Enrich geometry with Z coordinates
            enrichedGeometry = await GeoJsonElevationEnricher.EnrichGeometryAsync(
                components.GeometryNode,
                elevationService,
                context,
                cancellationToken).ConfigureAwait(false);

            // Add building height if configured
            if (elevationConfig?.IncludeHeight == true && components.Properties is Dictionary<string, object?> propsDict)
            {
                var height = AttributeElevationService.GetBuildingHeight(context);
                if (height.HasValue)
                {
                    var mutableProps = new Dictionary<string, object?>(propsDict, StringComparer.OrdinalIgnoreCase);
                    mutableProps["height"] = height.Value;
                    enrichedProperties = mutableProps;
                }
            }
        }

        // Build feature with enriched geometry
        var links = OgcSharedHandlers.BuildFeatureLinks(request, collectionId, layer, components, additionalLinks);
        var properties = new Dictionary<string, object?>(enrichedProperties, StringComparer.OrdinalIgnoreCase);
        OgcSharedHandlers.AppendStyleMetadata(properties, layer);

        return new
        {
            type = "Feature",
            id = components.RawId,
            geometry = enrichedGeometry,
            properties,
            links
        };
    }

    /// <summary>
    /// Extracts elevation configuration from layer metadata or extensions.
    /// </summary>
    private static ElevationConfiguration? GetElevationConfiguration(LayerDefinition layer)
    {
        // TODO: Check if layer has elevation configuration in extensions when Extensions property is added
        // For now, we'll skip the extensions check and go directly to fallback logic
        // The extensions feature will be implemented in a future update

        // Fallback: check for common elevation attribute names
        // This provides zero-config support if the layer has standard elevation columns
        var commonElevationNames = new[] { "elevation", "elev", "height", "z", "altitude" };
        foreach (var attrName in commonElevationNames)
        {
            if (layer.Fields?.Any(f => string.Equals(f.Name, attrName, StringComparison.OrdinalIgnoreCase)) == true)
            {
                return new ElevationConfiguration
                {
                    Source = "attribute",
                    ElevationAttribute = attrName,
                    DefaultElevation = 0,
                    VerticalOffset = 0,
                    IncludeHeight = false
                };
            }
        }

        // No elevation configuration found
        return null;
    }
}
