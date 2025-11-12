// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Cql2;

namespace Honua.Server.Core.Performance;

/// <summary>
/// JSON source generation context for high-performance serialization.
/// Eliminates reflection overhead for known types using compile-time code generation.
/// </summary>
/// <remarks>
/// Benefits:
/// - ~2-3x faster serialization/deserialization
/// - Zero reflection at runtime
/// - Reduced memory allocations
/// - Trim-friendly for AOT compilation
///
/// Usage:
/// var json = JsonSerializer.Serialize(snapshot, JsonSourceGenerationContext.Default.MetadataSnapshot);
/// var obj = JsonSerializer.Deserialize(json, JsonSourceGenerationContext.Default.MetadataSnapshot);
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
// Metadata
[JsonSerializable(typeof(MetadataSnapshot))]
[JsonSerializable(typeof(ServiceDefinition))]
[JsonSerializable(typeof(LayerDefinition))]
[JsonSerializable(typeof(FolderDefinition))]
[JsonSerializable(typeof(LinkDefinition))]
[JsonSerializable(typeof(CatalogContactDefinition))]
[JsonSerializable(typeof(CatalogSpatialExtentDefinition))]
[JsonSerializable(typeof(CatalogTemporalExtentDefinition))]
[JsonSerializable(typeof(LayerRelationshipDefinition))]
// Catalog API responses (frequently serialized)
[JsonSerializable(typeof(CatalogProjectionSnapshot))]
[JsonSerializable(typeof(CatalogGroupView))]
[JsonSerializable(typeof(CatalogServiceView))]
[JsonSerializable(typeof(CatalogLayerView))]
[JsonSerializable(typeof(CatalogDiscoveryRecord))]
// STAC API - Core types (frequently serialized)
[JsonSerializable(typeof(StacAsset))]
[JsonSerializable(typeof(StacCollectionListResult))]
[JsonSerializable(typeof(StacCollectionRecord))]
[JsonSerializable(typeof(StacExtent))]
[JsonSerializable(typeof(StacItemRecord))]
[JsonSerializable(typeof(StacLink))]
[JsonSerializable(typeof(StacSearchResult))]
[JsonSerializable(typeof(StacTemporalInterval))]
// STAC API - Search and query parameters
[JsonSerializable(typeof(StacSearchParameters))]
[JsonSerializable(typeof(StacSortDirection))]
[JsonSerializable(typeof(StacSortField))]
// STAC API - Fields Extension (include/exclude filtering)
[JsonSerializable(typeof(FieldsSpecification))]
// STAC API - Geometry types
[JsonSerializable(typeof(GeometryType))]
[JsonSerializable(typeof(ParsedGeometry))]
// STAC API - Bulk operations
[JsonSerializable(typeof(BulkUpsertItemFailure))]
[JsonSerializable(typeof(BulkUpsertOptions))]
[JsonSerializable(typeof(BulkUpsertResult))]
// STAC API - CQL2 Filter Expression types (polymorphic hierarchy)
[JsonSerializable(typeof(Cql2BetweenExpression))]
[JsonSerializable(typeof(Cql2ComparisonExpression))]
[JsonSerializable(typeof(Cql2Expression))]
[JsonSerializable(typeof(Cql2InExpression))]
[JsonSerializable(typeof(Cql2IsNullExpression))]
[JsonSerializable(typeof(Cql2LikeExpression))]
[JsonSerializable(typeof(Cql2Literal))]
[JsonSerializable(typeof(Cql2LogicalExpression))]
[JsonSerializable(typeof(Cql2NotExpression))]
[JsonSerializable(typeof(Cql2Operand))]
[JsonSerializable(typeof(Cql2PropertyRef))]
[JsonSerializable(typeof(Cql2SpatialExpression))]
[JsonSerializable(typeof(Cql2TemporalExpression))]
// STAC API - Configuration
[JsonSerializable(typeof(StacSearchOptions))]
// Attachment API types (frequently serialized)
[JsonSerializable(typeof(AttachmentDescriptor))]
[JsonSerializable(typeof(FeatureAttachmentError))]
[JsonSerializable(typeof(FeatureAttachmentOperationResult))]
[JsonSerializable(typeof(FeatureAttachmentOperation))]
internal partial class JsonSourceGenerationContext : JsonSerializerContext
{
}

/// <summary>
/// Extension methods for using JSON source generation.
/// </summary>
public static class JsonSerializationExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        TypeInfoResolver = JsonSourceGenerationContext.Default,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a MetadataSnapshot using source-generated JSON serialization.
    /// ~2-3x faster than reflection-based serialization.
    /// </summary>
    public static byte[] SerializeToUtf8BytesFast(this MetadataSnapshot snapshot)
    {
        return JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonSourceGenerationContext.Default.MetadataSnapshot);
    }

    /// <summary>
    /// Deserializes a MetadataSnapshot using source-generated JSON deserialization.
    /// ~2-3x faster than reflection-based deserialization.
    /// </summary>
    public static MetadataSnapshot? DeserializeFast(byte[] utf8Json)
    {
        return JsonSerializer.Deserialize(utf8Json, JsonSourceGenerationContext.Default.MetadataSnapshot);
    }

    /// <summary>
    /// Gets the default JsonSerializerOptions with source generation enabled.
    /// </summary>
    public static JsonSerializerOptions GetFastSerializerOptions()
    {
        return DefaultOptions;
    }
}
