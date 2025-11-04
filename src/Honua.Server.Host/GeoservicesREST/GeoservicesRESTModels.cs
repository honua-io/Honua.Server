// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace Honua.Server.Host.GeoservicesREST;

public sealed record ServicesDirectoryResponse
{
    public double CurrentVersion { get; init; }
    public IReadOnlyList<string> Folders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ServiceDirectoryEntry> Services { get; init; } = Array.Empty<ServiceDirectoryEntry>();
}

public sealed record ServiceDirectoryEntry
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed record FolderDirectoryResponse
{
    public double CurrentVersion { get; init; }
    public string FolderName { get; init; } = string.Empty;
    public IReadOnlyList<ServiceDirectoryEntry> Services { get; init; } = Array.Empty<ServiceDirectoryEntry>();
}

public sealed record GeoservicesRESTFeatureServiceSummary
{
    public double CurrentVersion { get; init; }
    public string ServiceDescription { get; init; } = string.Empty;
    public bool HasVersionedData { get; init; }
    public bool SupportsDisconnectedEditing { get; init; }
    public bool SupportsRelationshipsResource { get; init; }
    public bool SupportsTrueCurves { get; init; }
    public bool SupportsDatumTransformation { get; init; }
    public int MaxRecordCount { get; init; }
    public string SupportedQueryFormats { get; init; } = string.Empty;
    public string SupportedImageFormatTypes { get; init; } = string.Empty;
    public bool SingleFusedMapCache { get; init; }
    public string Capabilities { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CopyrightText { get; init; } = string.Empty;
    public bool SupportsDynamicLayers { get; init; }
    public bool AllowGeometryUpdates { get; init; }
    public bool HasStaticData { get; init; }
    public bool SupportsStatistics { get; init; }
    public IReadOnlyList<GeoservicesRESTLayerInfo> Layers { get; init; } = Array.Empty<GeoservicesRESTLayerInfo>();
    public IReadOnlyList<object> Tables { get; init; } = Array.Empty<object>();
}

public sealed record GeoservicesRESTImageServiceSummary
{
    public double CurrentVersion { get; init; }
    public string ServiceDescription { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Capabilities { get; init; } = "Image";
    public string SupportedImageFormatTypes { get; init; } = string.Empty;
    public bool SingleFusedMapCache { get; init; }
    public int MaxImageHeight { get; init; }
    public int MaxImageWidth { get; init; }
    public int DefaultCompressionQuality { get; init; }
    public GeoservicesRESTExtent? Extent { get; init; }
    public IReadOnlyList<string> Rasters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GeoservicesRESTImageDatasetInfo> Datasets { get; init; } = Array.Empty<GeoservicesRESTImageDatasetInfo>();
}

public sealed record GeoservicesRESTImageDatasetInfo
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? DefaultStyleId { get; init; }
    public IReadOnlyList<string> StyleIds { get; init; } = Array.Empty<string>();
}

public sealed record GeoservicesRESTLayerInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string GeometryType { get; init; } = string.Empty;
    public bool DefaultVisibility { get; init; }
    public int ParentLayerId { get; init; }
    public int[]? SubLayerIds { get; init; }
    public double MinScale { get; init; }
    public double MaxScale { get; init; }
    public GeoservicesRESTExtent? Extent { get; init; }
    public GeoservicesRESTTimeInfo? TimeInfo { get; init; }
}

public sealed record GeoservicesRESTExtent
{
    public double Xmin { get; init; }
    public double Ymin { get; init; }
    public double Xmax { get; init; }
    public double Ymax { get; init; }
    public GeoservicesRESTSpatialReference SpatialReference { get; init; } = new();
}

public sealed record GeoservicesRESTSpatialReference
{
    public int Wkid { get; init; }
    public int? LatestWkid { get; init; }
}

public sealed record GeoservicesRESTFieldInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Alias { get; init; } = string.Empty;
    public bool Nullable { get; init; }
    public bool Editable { get; init; }
    public GeoservicesRESTDomain? Domain { get; init; }
    public object? DefaultValue { get; init; }
    public int? Length { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
}

public sealed record GeoservicesRESTDomain
{
    public string Type { get; init; } = string.Empty; // "codedValue" or "range"
    public string? Name { get; init; }
    public IReadOnlyList<GeoservicesRESTCodedValue>? CodedValues { get; init; }
    public double[]? Range { get; init; } // [min, max] for range domains
}

public sealed record GeoservicesRESTCodedValue
{
    public string Name { get; init; } = string.Empty;
    public object Code { get; init; } = default!;
}

public sealed record GeoservicesRESTAdvancedQueryCapabilities
{
    public bool SupportsPagination { get; init; }
    public bool SupportsTrueCurve { get; init; }
    public bool SupportsQueryWithDistance { get; init; }
    public bool SupportsReturningQueryExtent { get; init; }
    public bool SupportsStatistics { get; init; }
    public bool SupportsOrderBy { get; init; }
    public bool SupportsDistinct { get; init; }
}

public sealed record GeoservicesRESTLayerDetailResponse
{
    public double CurrentVersion { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "Feature Layer";
    public string ObjectIdField { get; init; } = string.Empty;
    public string? GlobalIdField { get; init; }
    public string GeometryType { get; init; } = string.Empty;
    public string? DisplayField { get; init; }
    public string? Description { get; init; }
    public bool HasM { get; init; }
    public bool HasZ { get; init; }
    public bool HasAttachments { get; init; }
    public bool SupportsStatistics { get; init; }
    public bool SupportsAdvancedQueries { get; init; }
    public bool SupportsTrueCurves { get; init; }
    public bool SupportsCoordinatesQuantization { get; init; }
    public bool SupportsReturningQueryExtent { get; init; }
    public bool SupportsPagination { get; init; }
    public bool SupportsOrderBy { get; init; }
    public bool SupportsDistinct { get; init; }
    public bool AllowGeometryUpdates { get; init; }
    public bool SupportsRollbackOnFailureParameter { get; init; }
    public int MaxRecordCount { get; init; }
    public GeoservicesRESTSpatialReference? SourceSpatialReference { get; init; }
    public GeoservicesRESTExtent? Extent { get; init; }
    public IReadOnlyList<GeoservicesRESTFieldInfo> Fields { get; init; } = Array.Empty<GeoservicesRESTFieldInfo>();
    public GeoservicesRESTAdvancedQueryCapabilities AdvancedQueryCapabilities { get; init; } = new();
    public string Capabilities { get; init; } = string.Empty;
    public JsonObject? DrawingInfo { get; init; }
    public GeoservicesRESTTimeInfo? TimeInfo { get; init; }
    public double MinScale { get; init; }
    public double MaxScale { get; init; }
    public IReadOnlyList<GeoservicesRESTLayerRelationshipInfo> Relationships { get; init; } = Array.Empty<GeoservicesRESTLayerRelationshipInfo>();
}

public sealed record GeoservicesRESTLayerRelationshipInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Cardinality { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int RelatedTableId { get; init; } = -1;
}

public sealed record GeoservicesRESTTimeInfo
{
    public string? StartTimeField { get; init; }
    public string? EndTimeField { get; init; }
    public string TrackIdField { get; init; } = string.Empty;
    public IReadOnlyList<long?>? TimeExtent { get; init; }
    public GeoservicesRESTTimeReference? TimeReference { get; init; }
    public int? TimeInterval { get; init; }
    public string? TimeIntervalUnits { get; init; }
}

public sealed record GeoservicesRESTTimeReference
{
    public string TimeZone { get; init; } = "UTC";
    public bool RespectsDaylightSaving { get; init; }
}

public sealed record GeoservicesRESTFeature
{
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    public JsonObject? Geometry { get; init; }
}

public sealed record GeoservicesRESTIdentifyResponse
{
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }
    public IReadOnlyList<GeoservicesRESTIdentifyResult> Results { get; init; } = Array.Empty<GeoservicesRESTIdentifyResult>();
}

public sealed record GeoservicesRESTIdentifyResult
{
    public int LayerId { get; init; }
    public string LayerName { get; init; } = string.Empty;
    public string DisplayFieldName { get; init; } = string.Empty;
    public string GeometryType { get; init; } = string.Empty;
    public object? Value { get; init; }
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    public JsonObject? Geometry { get; init; }
}

public sealed record GeoservicesRESTFindResponse
{
    public IReadOnlyList<GeoservicesRESTFindResult> Results { get; init; } = Array.Empty<GeoservicesRESTFindResult>();
}

public sealed record GeoservicesRESTFindResult
{
    public int LayerId { get; init; }
    public string LayerName { get; init; } = string.Empty;
    public string DisplayFieldName { get; init; } = string.Empty;
    public string FoundFieldName { get; init; } = string.Empty;
    public object? Value { get; init; }
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    public JsonObject? Geometry { get; init; }
}

public sealed record GeoservicesRESTFeatureSetResponse
{
    public string ObjectIdFieldName { get; init; } = string.Empty;
    public string? DisplayFieldName { get; init; }
    public string GeometryType { get; init; } = string.Empty;
    public GeoservicesRESTSpatialReference SpatialReference { get; init; } = new();
    public IReadOnlyList<GeoservicesRESTFieldInfo> Fields { get; init; } = Array.Empty<GeoservicesRESTFieldInfo>();
    public IReadOnlyList<GeoservicesRESTFeature> Features { get; init; } = Array.Empty<GeoservicesRESTFeature>();
    public bool HasZ { get; init; }
    public bool HasM { get; init; }
    public bool ExceededTransferLimit { get; init; }
}

public sealed record GeoservicesRESTRelatedRecordsResponse
{
    public IReadOnlyList<GeoservicesRESTRelatedRecordGroup> RelatedRecordGroups { get; init; } = Array.Empty<GeoservicesRESTRelatedRecordGroup>();
    public IReadOnlyList<GeoservicesRESTFieldInfo> Fields { get; init; } = Array.Empty<GeoservicesRESTFieldInfo>();
    public string GeometryType { get; init; } = "esriGeometryNull";
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }
    public bool HasZ { get; init; }
    public bool HasM { get; init; }
    public bool ExceededTransferLimit { get; init; }
}

public sealed record GeoservicesRESTRelatedRecordGroup
{
    public object ObjectId { get; init; } = default!;
    public IReadOnlyList<GeoservicesRESTFeature> RelatedRecords { get; init; } = Array.Empty<GeoservicesRESTFeature>();
    public long? Count { get; init; }
}

public sealed record GeoservicesRESTQueryExtentResponse
{
    public long? Count { get; init; }
    public GeoservicesRESTExtent? Extent { get; init; }
    public GeoservicesRESTSpatialReference SpatialReference { get; init; } = new();
}

public sealed record GeoservicesRESTIdsResponse
{
    public string ObjectIdFieldName { get; init; } = string.Empty;
    public GeoservicesRESTUniqueIdField UniqueIdField { get; init; } = new();
    public IReadOnlyList<object> ObjectIds { get; init; } = Array.Empty<object>();
    public bool ExceededTransferLimit { get; init; }
}

public sealed record GeoservicesRESTUniqueIdField
{
    public string Name { get; init; } = string.Empty;
    public bool IsSystemMaintained { get; init; }
}

public sealed record GeoservicesRESTCountResponse
{
    public long Count { get; init; }
}

public sealed record GeoservicesRESTLayersResponse
{
    public double CurrentVersion { get; init; }
    public IReadOnlyList<GeoservicesRESTLayerDetailResponse> Layers { get; init; } = Array.Empty<GeoservicesRESTLayerDetailResponse>();
    public IReadOnlyList<object> Tables { get; init; } = Array.Empty<object>();
}

public sealed record GeoservicesRESTLegendResponse
{
    public double CurrentVersion { get; init; }
    public IReadOnlyList<GeoservicesRESTLegendLayer> Layers { get; init; } = Array.Empty<GeoservicesRESTLegendLayer>();
}

public sealed record GeoservicesRESTLegendLayer
{
    public int LayerId { get; init; }
    public string LayerName { get; init; } = string.Empty;
    public IReadOnlyList<GeoservicesRESTLegendEntry> Legend { get; init; } = Array.Empty<GeoservicesRESTLegendEntry>();
}

public sealed record GeoservicesRESTLegendEntry
{
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ContentType { get; init; } = "image/png";
    public string ImageData { get; init; } = string.Empty;
    public int Height { get; init; }
    public int Width { get; init; }
}
