// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestFeatureServiceInfo
{
    [JsonPropertyName("currentVersion")]
    public double? CurrentVersion { get; init; }

    [JsonPropertyName("serviceDescription")]
    public string? ServiceDescription { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; init; }

    [JsonPropertyName("maxRecordCount")]
    public int? MaxRecordCount { get; init; }

    [JsonPropertyName("supportedQueryFormats")]
    public string? SupportedQueryFormats { get; init; }

    [JsonPropertyName("hasStaticData")]
    public bool? HasStaticData { get; init; }

    [JsonPropertyName("supportsPagination")]
    public bool? SupportsPagination { get; init; }

    [JsonPropertyName("layers")]
    public List<GeoservicesRestLayerSummary> Layers { get; init; } = new();

    [JsonPropertyName("spatialReference")]
    public GeoservicesRestSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("documentInfo")]
    public GeoservicesRestDocumentInfo? DocumentInfo { get; init; }
}

public sealed class GeoservicesRestLayerSummary
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed class GeoservicesRestDocumentInfo
{
    [JsonPropertyName("Title")]
    public string? Title { get; init; }

    [JsonPropertyName("Subject")]
    public string? Subject { get; init; }

    [JsonPropertyName("Category")]
    public string? Category { get; init; }

    [JsonPropertyName("Keywords")]
    public string? Keywords { get; init; }

    [JsonPropertyName("Summary")]
    public string? Summary { get; init; }
}

public sealed class GeoservicesRestSpatialReference
{
    [JsonPropertyName("wkid")]
    public int? Wkid { get; init; }

    [JsonPropertyName("latestWkid")]
    public int? LatestWkid { get; init; }

    [JsonPropertyName("wkt")]
    public string? WellKnownText { get; init; }
}

public sealed class GeoservicesRestLayerInfo
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("objectIdField")]
    public string? ObjectIdField { get; init; }

    [JsonPropertyName("displayField")]
    public string? DisplayField { get; init; }

    [JsonPropertyName("timeField")]
    public string? TimeField { get; init; }

    [JsonPropertyName("supportsPagination")]
    public bool? SupportsPagination { get; init; }

    [JsonPropertyName("maxRecordCount")]
    public int? MaxRecordCount { get; init; }

    [JsonPropertyName("supportsStatistics")]
    public bool? SupportsStatistics { get; init; }

    [JsonPropertyName("extent")]
    public GeoservicesRestExtent? Extent { get; init; }

    [JsonPropertyName("drawingInfo")]
    public JsonElement DrawingInfo { get; init; }

    [JsonPropertyName("fields")]
    public List<GeoservicesRestLayerField> Fields { get; init; } = new();

    [JsonPropertyName("advancedQueryCapabilities")]
    public GeoservicesRestAdvancedQueryCapabilities? AdvancedQueryCapabilities { get; init; }

    [JsonPropertyName("effectiveMinScale")]
    public double? EffectiveMinScale { get; init; }

    [JsonPropertyName("effectiveMaxScale")]
    public double? EffectiveMaxScale { get; init; }

    [JsonPropertyName("defaultVisibility")]
    public bool? DefaultVisibility { get; init; }

    [JsonPropertyName("supportsAttachments")]
    public bool? SupportsAttachments { get; init; }

    [JsonPropertyName("supportsCalculate")]
    public bool? SupportsCalculate { get; init; }

    [JsonPropertyName("supportsCoordinatesQuantization")]
    public bool? SupportsCoordinatesQuantization { get; init; }

    [JsonPropertyName("hasM")]
    public bool? HasM { get; init; }

    [JsonPropertyName("hasZ")]
    public bool? HasZ { get; init; }

    [JsonPropertyName("parentLayer")]
    public JsonElement ParentLayer { get; init; }

    [JsonPropertyName("subLayers")]
    public JsonElement SubLayers { get; init; }

    [JsonPropertyName("minScale")]
    public double? MinScale { get; init; }

    [JsonPropertyName("maxScale")]
    public double? MaxScale { get; init; }

    [JsonPropertyName("supportsRollbackOnFailureParameter")]
    public bool? SupportsRollbackOnFailureParameter { get; init; }

    [JsonPropertyName("supportsAdvancedQueries")]
    public bool? SupportsAdvancedQueries { get; init; }

    [JsonPropertyName("supportsValidateSql")]
    public bool? SupportsValidateSql { get; init; }

    [JsonPropertyName("supportsChanges")]
    public bool? SupportsChanges { get; init; }

    [JsonPropertyName("allowGeometryUpdates")]
    public bool? AllowGeometryUpdates { get; init; }

    [JsonPropertyName("supportsApplyEditsWithGlobalIds")]
    public bool? SupportsApplyEditsWithGlobalIds { get; init; }

    [JsonPropertyName("supportsDatumTransformation")]
    public bool? SupportsDatumTransformation { get; init; }

    [JsonPropertyName("supportsOrderBy")]
    public bool? SupportsOrderBy { get; init; }

    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; init; }

    [JsonPropertyName("standardMaxRecordCount")]
    public int? StandardMaxRecordCount { get; init; }

    [JsonPropertyName("tileMaxRecordCount")]
    public int? TileMaxRecordCount { get; init; }

    [JsonPropertyName("maxRecordCountFactor")]
    public int? MaxRecordCountFactor { get; init; }

    [JsonPropertyName("preferredTimeReference")]
    public JsonElement PreferredTimeReference { get; init; }

    [JsonPropertyName("relationships")]
    public List<GeoservicesRestRelationshipInfo> Relationships { get; init; } = new();
}

public sealed class GeoservicesRestAdvancedQueryCapabilities
{
    [JsonPropertyName("supportsPagination")]
    public bool? SupportsPagination { get; init; }

    [JsonPropertyName("supportsStatistics")]
    public bool? SupportsStatistics { get; init; }

    [JsonPropertyName("supportsOrderBy")]
    public bool? SupportsOrderBy { get; init; }

    [JsonPropertyName("supportsDistinct")]
    public bool? SupportsDistinct { get; init; }

    [JsonPropertyName("supportsQueryWithDistance")]
    public bool? SupportsQueryWithDistance { get; init; }

    [JsonPropertyName("supportsSqlExpression")]
    public bool? SupportsSqlExpression { get; init; }
}

public sealed class GeoservicesRestRelationshipInfo
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("relatedTableId")]
    public int RelatedTableId { get; init; }
}

public sealed class GeoservicesRestLayerField
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("alias")]
    public string? Alias { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sqlType")]
    public string? SqlType { get; init; }

    [JsonPropertyName("domain")]
    public JsonElement Domain { get; init; }

    [JsonPropertyName("nullable")]
    public bool? Nullable { get; init; }

    [JsonPropertyName("editable")]
    public bool? Editable { get; init; }

    [JsonPropertyName("defaultValue")]
    public JsonElement DefaultValue { get; init; }

    [JsonPropertyName("length")]
    public int? Length { get; init; }

    [JsonPropertyName("precision")]
    public int? Precision { get; init; }

    [JsonPropertyName("scale")]
    public int? Scale { get; init; }
}

public sealed class GeoservicesRestExtent
{
    [JsonPropertyName("xmin")]
    public double XMin { get; init; }

    [JsonPropertyName("ymin")]
    public double YMin { get; init; }

    [JsonPropertyName("xmax")]
    public double XMax { get; init; }

    [JsonPropertyName("ymax")]
    public double YMax { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRestSpatialReference? SpatialReference { get; init; }
}

public sealed class GeoservicesRestFeatureRecord
{
    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement> Attributes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("geometry")]
    public JsonElement Geometry { get; init; }
}

public sealed class GeoservicesRestQueryResult
{
    [JsonPropertyName("features")]
    public List<GeoservicesRestFeatureRecord> Features { get; init; } = new();

    [JsonPropertyName("exceededTransferLimit")]
    public bool? ExceededTransferLimit { get; init; }

    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; init; }

    [JsonPropertyName("globalIdFieldName")]
    public string? GlobalIdFieldName { get; init; }
}

public sealed class GeoservicesRestIdQueryResult
{
    [JsonPropertyName("objectIds")]
    public List<int> ObjectIds { get; init; } = new();

    [JsonPropertyName("exceededTransferLimit")]
    public bool? ExceededTransferLimit { get; init; }
}
