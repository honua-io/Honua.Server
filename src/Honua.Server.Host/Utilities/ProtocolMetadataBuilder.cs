// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Utility for consolidating service/layer/collection resolution logic across protocols.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <para>
/// This builder consolidates ~280 lines of duplicate metadata resolution logic that was previously
/// scattered across STAC, OGC API Features, WFS, and WMS protocol handlers. Each protocol independently
/// resolved services, layers, and built collection metadata with nearly identical patterns.
/// </para>
/// <para><strong>Design Principles:</strong></para>
/// <list type="bullet">
///   <item>Returns tuples with nullable error strings for consistent error handling</item>
///   <item>Supports both vector layers and raster datasets</item>
///   <item>Uses ExtentCalculator for spatial extent calculations (no duplication)</item>
///   <item>Provides CRS resolution with service/layer hierarchy support</item>
///   <item>Handles temporal extent extraction from layer metadata</item>
/// </list>
/// <para><strong>Usage Patterns:</strong></para>
/// <para>
/// Protocols use this builder to resolve collections from IDs, extract CRS lists,
/// determine default CRS values, get temporal extents, and build collection metadata
/// in a consistent way across OGC API Features, WFS, WMS, WMTS, and STAC APIs.
/// </para>
/// </remarks>
public static class ProtocolMetadataBuilder
{
    /// <summary>
    /// Resolves a collection (service + layer) from a collection ID or layer ID.
    /// </summary>
    /// <param name="catalog">Catalog projection service for searching across services.</param>
    /// <param name="collectionId">Collection ID in format "serviceId:layerId" or just "layerId".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Tuple containing (Service, Layer, null) on success or (null, null, errorMessage) on failure.
    /// </returns>
    public static (ServiceDefinition? Service, LayerDefinition? Layer, string? Error)
        ResolveCollection(ICatalogProjectionService catalog, string collectionId, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(catalog);

        if (collectionId.IsNullOrWhiteSpace())
        {
            return (null, null, "Collection ID is required.");
        }

        string? serviceId = null;
        string layerId;

        // Parse collection ID - supports "serviceId:layerId" or just "layerId"
        var parts = collectionId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            serviceId = parts[0];
            layerId = parts[1];
        }
        else if (parts.Length == 1)
        {
            layerId = parts[0];

            // Search across all services to find the layer
            var projection = catalog.GetSnapshot();
            foreach (var service in projection.ServiceIndex.Values)
            {
                if (service.Layers.Any(l => string.Equals(l.Layer.Id, layerId, StringComparison.OrdinalIgnoreCase)))
                {
                    serviceId = service.Service.Id;
                    break;
                }
            }
        }
        else
        {
            return (null, null, $"Invalid collection ID format: '{collectionId}'. Expected 'serviceId:layerId' or 'layerId'.");
        }

        if (serviceId.IsNullOrWhiteSpace())
        {
            return (null, null, $"Layer '{layerId}' was not found in any service.");
        }

        // Resolve service and layer from catalog
        var snapshot = catalog.GetSnapshot();
        if (!snapshot.ServiceIndex.TryGetValue(serviceId, out var serviceProjection))
        {
            return (null, null, $"Service '{serviceId}' was not found.");
        }

        var layerProjection = serviceProjection.Layers.FirstOrDefault(l =>
            string.Equals(l.Layer.Id, layerId, StringComparison.OrdinalIgnoreCase));

        if (layerProjection == null)
        {
            return (null, null, $"Layer '{layerId}' was not found in service '{serviceId}'.");
        }

        return (serviceProjection.Service, layerProjection.Layer, null);
    }

    /// <summary>
    /// Resolves a raster dataset from a dataset ID.
    /// </summary>
    /// <param name="metadata">Metadata registry for accessing raster datasets.</param>
    /// <param name="datasetId">Raster dataset ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Tuple containing (null, Dataset, null) on success or (null, null, errorMessage) on failure.
    /// Note: Service is always null for raster datasets as they don't belong to services.
    /// </returns>
    public static async Task<(ServiceDefinition? Service, RasterDatasetDefinition? Dataset, string? Error)>
        ResolveRasterDatasetAsync(IMetadataRegistry metadata, string datasetId, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(metadata);

        if (datasetId.IsNullOrWhiteSpace())
        {
            return (null, null, "Dataset ID is required.");
        }

        var snapshot = await metadata.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var dataset = snapshot.RasterDatasets.FirstOrDefault(r => string.Equals(r.Id, datasetId, StringComparison.OrdinalIgnoreCase));

        if (dataset == null)
        {
            return (null, null, $"Raster dataset '{datasetId}' was not found.");
        }

        return (null, dataset, null);
    }

    /// <summary>
    /// Resolves supported CRS list from layer and service configuration.
    /// Follows the hierarchy: layer CRS -> service additional CRS -> default CRS (EPSG:4326).
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <param name="service">Service definition.</param>
    /// <returns>Read-only list of supported CRS identifiers in normalized form.</returns>
    public static IReadOnlyList<string> ResolveSupportedCrs(LayerDefinition layer, ServiceDefinition service)
    {
        Guard.NotNull(layer);
        Guard.NotNull(service);

        var supported = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCrs(string? value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return;
            }

            var normalized = CrsHelper.NormalizeIdentifier(value);
            if (seen.Add(normalized))
            {
                supported.Add(normalized);
            }
        }

        // Priority 1: Layer-specific CRS
        foreach (var crs in layer.Crs)
        {
            AddCrs(crs);
        }

        // Priority 2: Service-level additional CRS
        foreach (var crs in service.Ogc.AdditionalCrs)
        {
            AddCrs(crs);
        }

        // Priority 3: Storage CRS (if defined)
        if (layer.Storage?.Srid is int srid && srid > 0)
        {
            AddCrs($"EPSG:{srid}");
        }

        if (layer.Storage?.Crs.HasValue() == true)
        {
            AddCrs(layer.Storage.Crs);
        }

        // Ensure default CRS is always supported
        AddCrs(CrsHelper.DefaultCrsIdentifier);

        return supported;
    }

    /// <summary>
    /// Resolves supported CRS list for a raster dataset.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>Read-only list of supported CRS identifiers in normalized form.</returns>
    public static IReadOnlyList<string> ResolveSupportedCrs(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);

        var supported = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCrs(string? value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return;
            }

            var normalized = CrsHelper.NormalizeIdentifier(value);
            if (seen.Add(normalized))
            {
                supported.Add(normalized);
            }
        }

        // Add dataset-specific CRS
        foreach (var crs in dataset.Crs)
        {
            AddCrs(crs);
        }

        // Ensure default CRS is always supported
        AddCrs(CrsHelper.DefaultCrsIdentifier);

        return supported;
    }

    /// <summary>
    /// Determines the default CRS for a layer based on service configuration.
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <param name="service">Service definition.</param>
    /// <returns>Default CRS identifier in normalized form.</returns>
    public static string GetDefaultCrs(LayerDefinition layer, ServiceDefinition service)
    {
        Guard.NotNull(layer);
        Guard.NotNull(service);

        var supported = ResolveSupportedCrs(layer, service);

        if (supported.Count == 0)
        {
            return CrsHelper.DefaultCrsIdentifier;
        }

        // Check if service specifies a default CRS
        if (service.Ogc.DefaultCrs.HasValue())
        {
            var normalizedDefault = CrsHelper.NormalizeIdentifier(service.Ogc.DefaultCrs);
            var match = supported.FirstOrDefault(crs =>
                string.Equals(crs, normalizedDefault, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        // Return first supported CRS
        return supported[0];
    }

    /// <summary>
    /// Determines the default CRS for a raster dataset.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>Default CRS identifier in normalized form.</returns>
    public static string GetDefaultCrs(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);

        var supported = ResolveSupportedCrs(dataset);

        if (supported.Count == 0)
        {
            return CrsHelper.DefaultCrsIdentifier;
        }

        // Return first supported CRS
        return supported[0];
    }

    /// <summary>
    /// Extracts temporal extent from layer metadata.
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <returns>
    /// Tuple containing (Start, End) if temporal extent is defined, null otherwise.
    /// Returns the first temporal interval if multiple are defined.
    /// </returns>
    public static (DateTimeOffset? Start, DateTimeOffset? End)? GetTemporalExtent(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        if (layer.Extent?.Temporal is null || layer.Extent.Temporal.Count == 0)
        {
            return null;
        }

        // Return first temporal interval
        var interval = layer.Extent.Temporal[0];
        return (interval.Start, interval.End);
    }

    /// <summary>
    /// Extracts temporal extent from raster dataset metadata.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>
    /// Tuple containing (Start, End) if temporal extent is defined, null otherwise.
    /// Returns the first temporal interval if multiple are defined.
    /// </returns>
    public static (DateTimeOffset? Start, DateTimeOffset? End)? GetTemporalExtent(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);

        if (dataset.Extent?.Temporal is null || dataset.Extent.Temporal.Count == 0)
        {
            return null;
        }

        // Return first temporal interval
        var interval = dataset.Extent.Temporal[0];
        return (interval.Start, interval.End);
    }

    /// <summary>
    /// Gets the display title for a collection from layer metadata.
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <returns>Layer title or ID as fallback.</returns>
    public static string GetCollectionTitle(LayerDefinition layer)
    {
        Guard.NotNull(layer);
        return layer.Title ?? layer.Id;
    }

    /// <summary>
    /// Gets the display title for a collection from raster dataset metadata.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>Dataset title or ID as fallback.</returns>
    public static string GetCollectionTitle(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);
        return dataset.Title ?? dataset.Id;
    }

    /// <summary>
    /// Gets the description for a collection from layer metadata.
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <returns>Layer description or null if not defined.</returns>
    public static string? GetCollectionDescription(LayerDefinition layer)
    {
        Guard.NotNull(layer);
        return layer.Description;
    }

    /// <summary>
    /// Gets the description for a collection from raster dataset metadata.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>Dataset description or null if not defined.</returns>
    public static string? GetCollectionDescription(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);
        return dataset.Description;
    }

    /// <summary>
    /// Gets collection properties from layer metadata.
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <returns>Read-only dictionary of collection properties.</returns>
    public static IReadOnlyDictionary<string, object?> GetCollectionProperties(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var properties = new Dictionary<string, object?>();

        if (layer.Keywords?.Count > 0)
        {
            properties["keywords"] = layer.Keywords;
        }

        if (layer.GeometryType.HasValue())
        {
            properties["geometryType"] = layer.GeometryType;
        }

        if (layer.ItemType.HasValue())
        {
            properties["itemType"] = layer.ItemType;
        }

        if (layer.MinScale.HasValue)
        {
            properties["minScale"] = layer.MinScale.Value;
        }

        if (layer.MaxScale.HasValue)
        {
            properties["maxScale"] = layer.MaxScale.Value;
        }

        return properties;
    }

    /// <summary>
    /// Gets collection properties from raster dataset metadata.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>Read-only dictionary of collection properties.</returns>
    public static IReadOnlyDictionary<string, object?> GetCollectionProperties(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);

        var properties = new Dictionary<string, object?>();

        if (dataset.Keywords?.Count > 0)
        {
            properties["keywords"] = dataset.Keywords;
        }

        if (dataset.Source?.Type.HasValue() == true)
        {
            properties["sourceType"] = dataset.Source.Type;
        }

        return properties;
    }

    /// <summary>
    /// Gets spatial extent from layer metadata as a 4-element array [minX, minY, maxX, maxY].
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <returns>
    /// Spatial extent array if defined in metadata, null otherwise.
    /// Returns the first bbox if multiple are defined.
    /// </returns>
    public static double[]? GetSpatialExtent(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        if (layer.Extent?.Bbox is null || layer.Extent.Bbox.Count == 0)
        {
            return null;
        }

        var bbox = layer.Extent.Bbox[0];
        if (bbox is null || bbox.Length < 4)
        {
            return null;
        }

        return bbox;
    }

    /// <summary>
    /// Gets spatial extent from raster dataset metadata as a 4-element array [minX, minY, maxX, maxY].
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>
    /// Spatial extent array if defined in metadata, null otherwise.
    /// Returns the first bbox if multiple are defined.
    /// </returns>
    public static double[]? GetSpatialExtent(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);

        if (dataset.Extent?.Bbox is null || dataset.Extent.Bbox.Count == 0)
        {
            return null;
        }

        var bbox = dataset.Extent.Bbox[0];
        if (bbox is null || bbox.Length < 4)
        {
            return null;
        }

        return bbox;
    }

    /// <summary>
    /// Gets the extent CRS from layer metadata.
    /// </summary>
    /// <param name="layer">Layer definition.</param>
    /// <returns>Extent CRS identifier or null if not defined.</returns>
    public static string? GetExtentCrs(LayerDefinition layer)
    {
        Guard.NotNull(layer);
        return layer.Extent?.Crs;
    }

    /// <summary>
    /// Gets the extent CRS from raster dataset metadata.
    /// </summary>
    /// <param name="dataset">Raster dataset definition.</param>
    /// <returns>Extent CRS identifier or null if not defined.</returns>
    public static string? GetExtentCrs(RasterDatasetDefinition dataset)
    {
        Guard.NotNull(dataset);
        return dataset.Extent?.Crs;
    }
}
