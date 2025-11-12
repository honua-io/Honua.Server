// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models.Esri;

/// <summary>
/// Represents metadata from an ArcGIS FeatureServer or MapServer
/// </summary>
public class EsriServiceInfo
{
    /// <summary>
    /// Current version of the service
    /// </summary>
    [JsonPropertyName("currentVersion")]
    public double CurrentVersion { get; set; }

    /// <summary>
    /// Service name
    /// </summary>
    [JsonPropertyName("serviceDescription")]
    public string? ServiceDescription { get; set; }

    /// <summary>
    /// Has versioned data
    /// </summary>
    [JsonPropertyName("hasVersionedData")]
    public bool HasVersionedData { get; set; }

    /// <summary>
    /// Supports disconnected editing
    /// </summary>
    [JsonPropertyName("supportsDisconnectedEditing")]
    public bool SupportsDisconnectedEditing { get; set; }

    /// <summary>
    /// Has static data
    /// </summary>
    [JsonPropertyName("hasStaticData")]
    public bool HasStaticData { get; set; }

    /// <summary>
    /// Max record count
    /// </summary>
    [JsonPropertyName("maxRecordCount")]
    public int MaxRecordCount { get; set; }

    /// <summary>
    /// Supported query formats
    /// </summary>
    [JsonPropertyName("supportedQueryFormats")]
    public string? SupportedQueryFormats { get; set; }

    /// <summary>
    /// Capabilities (Query, Create, Update, Delete, etc.)
    /// </summary>
    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; set; }

    /// <summary>
    /// Service description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Copyright text
    /// </summary>
    [JsonPropertyName("copyrightText")]
    public string? CopyrightText { get; set; }

    /// <summary>
    /// Spatial reference
    /// </summary>
    [JsonPropertyName("spatialReference")]
    public EsriSpatialReference? SpatialReference { get; set; }

    /// <summary>
    /// Initial extent
    /// </summary>
    [JsonPropertyName("initialExtent")]
    public EsriExtent? InitialExtent { get; set; }

    /// <summary>
    /// Full extent
    /// </summary>
    [JsonPropertyName("fullExtent")]
    public EsriExtent? FullExtent { get; set; }

    /// <summary>
    /// Units
    /// </summary>
    [JsonPropertyName("units")]
    public string? Units { get; set; }

    /// <summary>
    /// Layers in the service
    /// </summary>
    [JsonPropertyName("layers")]
    public List<EsriLayerInfo>? Layers { get; set; }

    /// <summary>
    /// Tables in the service
    /// </summary>
    [JsonPropertyName("tables")]
    public List<EsriLayerInfo>? Tables { get; set; }

    /// <summary>
    /// Relationships
    /// </summary>
    [JsonPropertyName("relationships")]
    public List<EsriRelationship>? Relationships { get; set; }

    /// <summary>
    /// Enable Z defaults
    /// </summary>
    [JsonPropertyName("enableZDefaults")]
    public bool EnableZDefaults { get; set; }

    /// <summary>
    /// Allow geometry updates
    /// </summary>
    [JsonPropertyName("allowGeometryUpdates")]
    public bool AllowGeometryUpdates { get; set; }
}

/// <summary>
/// Layer information
/// </summary>
public class EsriLayerInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; set; }

    [JsonPropertyName("minScale")]
    public double MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double MaxScale { get; set; }

    [JsonPropertyName("defaultVisibility")]
    public bool DefaultVisibility { get; set; }
}

/// <summary>
/// Spatial reference information
/// </summary>
public class EsriSpatialReference
{
    [JsonPropertyName("wkid")]
    public int? Wkid { get; set; }

    [JsonPropertyName("latestWkid")]
    public int? LatestWkid { get; set; }

    [JsonPropertyName("wkt")]
    public string? Wkt { get; set; }
}

/// <summary>
/// Extent/bounding box
/// </summary>
public class EsriExtent
{
    [JsonPropertyName("xmin")]
    public double Xmin { get; set; }

    [JsonPropertyName("ymin")]
    public double Ymin { get; set; }

    [JsonPropertyName("xmax")]
    public double Xmax { get; set; }

    [JsonPropertyName("ymax")]
    public double Ymax { get; set; }

    [JsonPropertyName("spatialReference")]
    public EsriSpatialReference? SpatialReference { get; set; }
}

/// <summary>
/// Relationship definition
/// </summary>
public class EsriRelationship
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("relatedTableId")]
    public int RelatedTableId { get; set; }

    [JsonPropertyName("cardinality")]
    public string? Cardinality { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("keyField")]
    public string? KeyField { get; set; }

    [JsonPropertyName("composite")]
    public bool Composite { get; set; }
}

/// <summary>
/// Layer metadata (detailed)
/// </summary>
public class EsriLayerMetadata
{
    [JsonPropertyName("currentVersion")]
    public double CurrentVersion { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; set; }

    [JsonPropertyName("copyrightText")]
    public string? CopyrightText { get; set; }

    [JsonPropertyName("parentLayer")]
    public EsriLayerInfo? ParentLayer { get; set; }

    [JsonPropertyName("subLayers")]
    public List<EsriLayerInfo>? SubLayers { get; set; }

    [JsonPropertyName("minScale")]
    public double MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double MaxScale { get; set; }

    [JsonPropertyName("defaultVisibility")]
    public bool DefaultVisibility { get; set; }

    [JsonPropertyName("extent")]
    public EsriExtent? Extent { get; set; }

    [JsonPropertyName("hasAttachments")]
    public bool HasAttachments { get; set; }

    [JsonPropertyName("htmlPopupType")]
    public string? HtmlPopupType { get; set; }

    [JsonPropertyName("displayField")]
    public string? DisplayField { get; set; }

    [JsonPropertyName("typeIdField")]
    public string? TypeIdField { get; set; }

    [JsonPropertyName("fields")]
    public List<EsriField>? Fields { get; set; }

    [JsonPropertyName("geometryField")]
    public EsriField? GeometryField { get; set; }

    [JsonPropertyName("indexes")]
    public List<EsriIndex>? Indexes { get; set; }

    [JsonPropertyName("types")]
    public List<EsriFeatureType>? Types { get; set; }

    [JsonPropertyName("templates")]
    public List<EsriFeatureTemplate>? Templates { get; set; }

    [JsonPropertyName("maxRecordCount")]
    public int MaxRecordCount { get; set; }

    [JsonPropertyName("supportedQueryFormats")]
    public string? SupportedQueryFormats { get; set; }

    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; set; }

    [JsonPropertyName("useStandardizedQueries")]
    public bool UseStandardizedQueries { get; set; }

    [JsonPropertyName("drawingInfo")]
    public EsriDrawingInfo? DrawingInfo { get; set; }

    [JsonPropertyName("relationships")]
    public List<EsriRelationship>? Relationships { get; set; }
}

/// <summary>
/// Field definition
/// </summary>
public class EsriField
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("sqlType")]
    public string? SqlType { get; set; }

    [JsonPropertyName("length")]
    public int? Length { get; set; }

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; }

    [JsonPropertyName("editable")]
    public bool Editable { get; set; }

    [JsonPropertyName("domain")]
    public EsriDomain? Domain { get; set; }

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Index definition
/// </summary>
public class EsriIndex
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("fields")]
    public string? Fields { get; set; }

    [JsonPropertyName("isAscending")]
    public bool IsAscending { get; set; }

    [JsonPropertyName("isUnique")]
    public bool IsUnique { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Feature type (subtype)
/// </summary>
public class EsriFeatureType
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("domains")]
    public Dictionary<string, EsriDomain>? Domains { get; set; }

    [JsonPropertyName("templates")]
    public List<EsriFeatureTemplate>? Templates { get; set; }
}

/// <summary>
/// Feature template
/// </summary>
public class EsriFeatureTemplate
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("prototype")]
    public EsriFeature? Prototype { get; set; }

    [JsonPropertyName("drawingTool")]
    public string? DrawingTool { get; set; }
}

/// <summary>
/// Drawing/symbology information
/// </summary>
public class EsriDrawingInfo
{
    [JsonPropertyName("renderer")]
    public EsriRenderer? Renderer { get; set; }

    [JsonPropertyName("transparency")]
    public double Transparency { get; set; }

    [JsonPropertyName("labelingInfo")]
    public List<EsriLabelingInfo>? LabelingInfo { get; set; }
}

/// <summary>
/// Renderer (symbology)
/// </summary>
public class EsriRenderer
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("symbol")]
    public EsriSymbol? Symbol { get; set; }

    [JsonPropertyName("field1")]
    public string? Field1 { get; set; }

    [JsonPropertyName("field2")]
    public string? Field2 { get; set; }

    [JsonPropertyName("field3")]
    public string? Field3 { get; set; }

    [JsonPropertyName("fieldDelimiter")]
    public string? FieldDelimiter { get; set; }

    [JsonPropertyName("defaultSymbol")]
    public EsriSymbol? DefaultSymbol { get; set; }

    [JsonPropertyName("defaultLabel")]
    public string? DefaultLabel { get; set; }

    [JsonPropertyName("uniqueValueInfos")]
    public List<EsriUniqueValueInfo>? UniqueValueInfos { get; set; }
}

/// <summary>
/// Symbol definition
/// </summary>
public class EsriSymbol
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("color")]
    public int[]? Color { get; set; }

    [JsonPropertyName("size")]
    public double? Size { get; set; }

    [JsonPropertyName("style")]
    public string? Style { get; set; }

    [JsonPropertyName("outline")]
    public EsriSymbol? Outline { get; set; }

    [JsonPropertyName("width")]
    public double? Width { get; set; }
}

/// <summary>
/// Unique value info for categorized renderer
/// </summary>
public class EsriUniqueValueInfo
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("symbol")]
    public EsriSymbol? Symbol { get; set; }
}

/// <summary>
/// Labeling information
/// </summary>
public class EsriLabelingInfo
{
    [JsonPropertyName("labelExpression")]
    public string? LabelExpression { get; set; }

    [JsonPropertyName("labelPlacement")]
    public string? LabelPlacement { get; set; }

    [JsonPropertyName("symbol")]
    public EsriSymbol? Symbol { get; set; }

    [JsonPropertyName("minScale")]
    public double MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double MaxScale { get; set; }
}
