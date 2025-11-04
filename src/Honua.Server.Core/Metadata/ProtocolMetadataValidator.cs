// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates that metadata contains required fields for each API protocol.
/// Ensures semantic metadata completeness across different output formats.
/// </summary>
public sealed class ProtocolMetadataValidator
{
    /// <summary>
    /// Validates metadata requirements for OGC API Features protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForOgcApiFeatures(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (layer.Id.IsNullOrWhiteSpace())
        {
            errors.Add("layer.id is required for OGC API Features");
        }

        if (layer.Title.IsNullOrWhiteSpace())
        {
            errors.Add("layer.title is required for OGC API Features");
        }

        if (layer.GeometryType.IsNullOrWhiteSpace())
        {
            errors.Add("layer.geometryType is required for OGC API Features");
        }

        if (layer.GeometryField.IsNullOrWhiteSpace())
        {
            errors.Add("layer.geometryField is required for OGC API Features");
        }

        if (layer.Extent?.Bbox == null || layer.Extent.Bbox.Count == 0)
        {
            errors.Add("layer.extent.bbox is required for OGC API Features");
        }

        // Recommended fields
        if (layer.Description.IsNullOrWhiteSpace())
        {
            warnings.Add("layer.description is recommended for OGC API Features (used in collection metadata)");
        }

        if (layer.Keywords.Count == 0)
        {
            warnings.Add("layer.keywords is recommended for OGC API Features (used for discovery)");
        }

        if (layer.Fields.Count == 0)
        {
            warnings.Add("layer.fields is recommended for OGC API Features (used for property schema)");
        }

        if (layer.Crs.Count == 0)
        {
            warnings.Add("layer.crs is recommended for OGC API Features (defaults to CRS84 if omitted)");
        }

        return new ProtocolValidationResult("OGC API Features", errors, warnings);
    }

    /// <summary>
    /// Validates metadata requirements for Esri REST API (FeatureServer) protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForEsriRest(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (layer.Id.IsNullOrWhiteSpace())
        {
            errors.Add("layer.id is required for Esri REST API");
        }

        if (layer.Title.IsNullOrWhiteSpace())
        {
            errors.Add("layer.title is required for Esri REST API");
        }

        if (layer.GeometryType.IsNullOrWhiteSpace())
        {
            errors.Add("layer.geometryType is required for Esri REST API");
        }

        if (layer.IdField.IsNullOrWhiteSpace())
        {
            errors.Add("layer.idField is required for Esri REST API (used as objectIdField)");
        }

        if (layer.GeometryField.IsNullOrWhiteSpace())
        {
            errors.Add("layer.geometryField is required for Esri REST API");
        }

        if (layer.Extent?.Bbox == null || layer.Extent.Bbox.Count == 0)
        {
            errors.Add("layer.extent.bbox is required for Esri REST API");
        }

        if (layer.Storage?.Srid == null)
        {
            errors.Add("layer.storage.srid is required for Esri REST API (used for spatialReference)");
        }

        // Recommended fields
        if (layer.DisplayField.IsNullOrWhiteSpace())
        {
            warnings.Add("layer.displayField is recommended for Esri REST API (used for default label field)");
        }

        if (layer.Fields.Count == 0)
        {
            warnings.Add("layer.fields is recommended for Esri REST API (provides full field schema)");
        }

        if (layer.Editing.Capabilities.AllowAdd || layer.Editing.Capabilities.AllowUpdate || layer.Editing.Capabilities.AllowDelete)
        {
            if (layer.Editing.Constraints.RequiredFields.Count == 0 && layer.Fields.Any(f => !f.Nullable))
            {
                warnings.Add("layer.editing.constraints.requiredFields should list non-nullable fields when editing is enabled");
            }
        }

        return new ProtocolValidationResult("Esri REST API", errors, warnings);
    }

    /// <summary>
    /// Validates metadata requirements for WMS 1.3 protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForWms(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (layer.Id.IsNullOrWhiteSpace())
        {
            errors.Add("layer.id is required for WMS (used as Layer/Name)");
        }

        if (layer.Title.IsNullOrWhiteSpace())
        {
            errors.Add("layer.title is required for WMS (used as Layer/Title)");
        }

        if (layer.Extent?.Bbox == null || layer.Extent.Bbox.Count == 0)
        {
            errors.Add("layer.extent.bbox is required for WMS (used as Layer/BoundingBox)");
        }

        if (layer.Crs.Count == 0)
        {
            errors.Add("layer.crs is required for WMS (used as Layer/CRS list)");
        }

        // Recommended fields
        if (layer.Description.IsNullOrWhiteSpace())
        {
            warnings.Add("layer.description is recommended for WMS (used as Layer/Abstract)");
        }

        if (layer.Keywords.Count == 0)
        {
            warnings.Add("layer.keywords is recommended for WMS (used as Layer/KeywordList)");
        }

        if (layer.MinScale == null || layer.MaxScale == null)
        {
            warnings.Add("layer.minScale and layer.maxScale are recommended for WMS (used as scale denominators)");
        }

        if (layer.DefaultStyleId.IsNullOrWhiteSpace())
        {
            warnings.Add("layer.defaultStyleId is recommended for WMS (used as default Layer/Style)");
        }

        return new ProtocolValidationResult("WMS 1.3", errors, warnings);
    }

    /// <summary>
    /// Validates metadata requirements for WFS 2.0 protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForWfs(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (layer.Id.IsNullOrWhiteSpace())
        {
            errors.Add("layer.id is required for WFS (used as FeatureType/Name)");
        }

        if (layer.Title.IsNullOrWhiteSpace())
        {
            errors.Add("layer.title is required for WFS (used as FeatureType/Title)");
        }

        if (layer.GeometryType.IsNullOrWhiteSpace())
        {
            errors.Add("layer.geometryType is required for WFS (used in XSD schema)");
        }

        if (layer.GeometryField.IsNullOrWhiteSpace())
        {
            errors.Add("layer.geometryField is required for WFS (used as geometry property name)");
        }

        if (layer.Extent?.Bbox == null || layer.Extent.Bbox.Count == 0)
        {
            errors.Add("layer.extent.bbox is required for WFS (used as WGS84BoundingBox)");
        }

        if (layer.Fields.Count == 0)
        {
            errors.Add("layer.fields is required for WFS (used to generate XSD schema)");
        }

        // Recommended fields
        if (layer.Crs.Count == 0)
        {
            warnings.Add("layer.crs is recommended for WFS (used as DefaultCRS and OtherCRS)");
        }

        if (layer.Description.IsNullOrWhiteSpace())
        {
            warnings.Add("layer.description is recommended for WFS (used as FeatureType/Abstract)");
        }

        return new ProtocolValidationResult("WFS 2.0", errors, warnings);
    }

    /// <summary>
    /// Validates metadata requirements for CSW 2.0.2 (Catalog Service for the Web) protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForCsw(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (layer.Id.IsNullOrWhiteSpace())
        {
            errors.Add("layer.id is required for CSW (used as dc:identifier)");
        }

        if (layer.Title.IsNullOrWhiteSpace())
        {
            errors.Add("layer.title is required for CSW (used as dc:title)");
        }

        // Recommended fields
        if (layer.Description.IsNullOrWhiteSpace() && layer.Catalog.Summary.IsNullOrWhiteSpace())
        {
            warnings.Add("layer.description or layer.catalog.summary is recommended for CSW (used as dct:abstract)");
        }

        if (layer.Keywords.Count == 0 && layer.Catalog.Keywords.Count == 0)
        {
            warnings.Add("layer.keywords or layer.catalog.keywords is recommended for CSW (used as dc:subject)");
        }

        if (layer.Extent?.Bbox == null || layer.Extent.Bbox.Count == 0)
        {
            warnings.Add("layer.extent.bbox is recommended for CSW (used as ows:BoundingBox)");
        }

        if (layer.Links.Count == 0 && layer.Catalog.Links.Count == 0)
        {
            warnings.Add("layer.links or layer.catalog.links is recommended for CSW (used as dct:references)");
        }

        return new ProtocolValidationResult("CSW 2.0.2", errors, warnings);
    }

    /// <summary>
    /// Validates metadata requirements for WCS 2.0.1 protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForWcs(RasterDatasetDefinition raster)
    {
        Guard.NotNull(raster);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (raster.Id.IsNullOrWhiteSpace())
        {
            errors.Add("raster.id is required for WCS (used as coverageId)");
        }

        if (raster.Title.IsNullOrWhiteSpace())
        {
            errors.Add("raster.title is required for WCS");
        }

        if (raster.Source == null || raster.Source.Uri.IsNullOrWhiteSpace())
        {
            errors.Add("raster.source.uri is required for WCS (coverage file path)");
        }

        if (raster.Extent?.Bbox == null || raster.Extent.Bbox.Count == 0)
        {
            errors.Add("raster.extent.bbox is required for WCS");
        }

        // Recommended fields
        if (raster.Crs.Count == 0)
        {
            warnings.Add("raster.crs is recommended for WCS (used for CRS metadata)");
        }

        if (raster.Description.IsNullOrWhiteSpace())
        {
            warnings.Add("raster.description is recommended for WCS");
        }

        return new ProtocolValidationResult("WCS 2.0.1", errors, warnings);
    }

    /// <summary>
    /// Validates metadata requirements for STAC 1.0 protocol.
    /// </summary>
    public static ProtocolValidationResult ValidateForStac(RasterDatasetDefinition raster)
    {
        Guard.NotNull(raster);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (raster.Id.IsNullOrWhiteSpace())
        {
            errors.Add("raster.id is required for STAC (used as item.id)");
        }

        if (raster.Title.IsNullOrWhiteSpace())
        {
            errors.Add("raster.title is required for STAC");
        }

        if (raster.Extent?.Bbox == null || raster.Extent.Bbox.Count == 0)
        {
            errors.Add("raster.extent.bbox is required for STAC (used as item.bbox)");
        }

        if (raster.Source == null || raster.Source.Uri.IsNullOrWhiteSpace())
        {
            errors.Add("raster.source.uri is required for STAC (used as asset href)");
        }

        // Recommended fields
        if (raster.Description.IsNullOrWhiteSpace())
        {
            warnings.Add("raster.description is recommended for STAC (used as item.properties.description)");
        }

        if (raster.Keywords.Count == 0)
        {
            warnings.Add("raster.keywords is recommended for STAC (used for discovery)");
        }

        if (!raster.Temporal.Enabled)
        {
            warnings.Add("raster.temporal is recommended for STAC (used as datetime properties)");
        }

        if (raster.Catalog.Thumbnail.IsNullOrWhiteSpace())
        {
            warnings.Add("raster.catalog.thumbnail is recommended for STAC (used as item preview)");
        }

        return new ProtocolValidationResult("STAC 1.0", errors, warnings);
    }

    /// <summary>
    /// Validates metadata for all enabled protocols based on service configuration.
    /// </summary>
    public static IReadOnlyList<ProtocolValidationResult> ValidateLayer(
        LayerDefinition layer,
        ServiceDefinition service,
        bool includeWarnings = true)
    {
        Guard.NotNull(layer);
        Guard.NotNull(service);

        var results = new List<ProtocolValidationResult>();

        // Always validate OGC API Features (core protocol)
        if (service.Ogc.CollectionsEnabled)
        {
            results.Add(ValidateForOgcApiFeatures(layer));
        }

        // Always validate Esri REST API (core protocol)
        results.Add(ValidateForEsriRest(layer));

        // WMS validation (if layer has styling or is meant for visualization)
        if (layer.DefaultStyleId.HasValue() || layer.StyleIds.Count > 0)
        {
            results.Add(ValidateForWms(layer));
        }

        // WFS validation (vector data with schema)
        if (layer.Fields.Count > 0)
        {
            results.Add(ValidateForWfs(layer));
        }

        // CSW validation (for catalog discovery)
        results.Add(ValidateForCsw(layer));

        if (!includeWarnings)
        {
            // Filter out warnings if not requested
            results = results
                .Select(r => new ProtocolValidationResult(r.Protocol, r.Errors, Array.Empty<string>()))
                .ToList();
        }

        return results;
    }

    /// <summary>
    /// Validates raster dataset metadata for all relevant protocols.
    /// </summary>
    public static IReadOnlyList<ProtocolValidationResult> ValidateRasterDataset(
        RasterDatasetDefinition raster,
        bool includeWarnings = true)
    {
        Guard.NotNull(raster);

        var results = new List<ProtocolValidationResult>
        {
            ValidateForWcs(raster),
            ValidateForStac(raster)
        };

        if (!includeWarnings)
        {
            results = results
                .Select(r => new ProtocolValidationResult(r.Protocol, r.Errors, Array.Empty<string>()))
                .ToList();
        }

        return results;
    }
}

/// <summary>
/// Result of protocol-specific metadata validation.
/// </summary>
public sealed record ProtocolValidationResult
{
    public ProtocolValidationResult(string protocol, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        Errors = errors ?? Array.Empty<string>();
        Warnings = warnings ?? Array.Empty<string>();
    }

    /// <summary>
    /// The protocol being validated (e.g., "OGC API Features", "Esri REST API").
    /// </summary>
    public string Protocol { get; }

    /// <summary>
    /// Critical validation errors that prevent the layer from being served via this protocol.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Non-critical warnings about recommended metadata fields.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// True if there are no errors (warnings are acceptable).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// True if there are no errors or warnings.
    /// </summary>
    public bool IsPerfect => Errors.Count == 0 && Warnings.Count == 0;

    /// <summary>
    /// Total number of issues (errors + warnings).
    /// </summary>
    public int IssueCount => Errors.Count + Warnings.Count;
}
