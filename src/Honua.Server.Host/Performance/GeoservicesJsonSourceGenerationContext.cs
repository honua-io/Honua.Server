// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Server.Host.GeoservicesREST;

namespace Honua.Server.Host.Performance;

/// <summary>
/// JSON source generation context for GeoservicesREST API high-performance serialization.
/// Eliminates reflection overhead for frequently serialized GeoservicesREST types.
/// </summary>
/// <remarks>
/// Benefits:
/// - ~2-3x faster serialization for GeoservicesREST responses
/// - Zero reflection at runtime
/// - Reduced memory allocations
/// - Better CPU cache utilization
///
/// Usage:
/// var json = JsonSerializer.Serialize(feature, GeoservicesJsonSourceGenerationContext.Default.GeoservicesRESTFeature);
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
// GeoservicesREST API responses (frequently serialized)
[JsonSerializable(typeof(GeoservicesRESTFeature))]
[JsonSerializable(typeof(GeoservicesRESTFeatureSetResponse))]
[JsonSerializable(typeof(GeoservicesRESTFeatureServiceSummary))]
[JsonSerializable(typeof(GeoservicesRESTLayerDetailResponse))]
[JsonSerializable(typeof(GeoservicesRESTFieldInfo))]
[JsonSerializable(typeof(GeoservicesRESTSpatialReference))]
[JsonSerializable(typeof(GeoservicesRESTExtent))]
[JsonSerializable(typeof(GeoservicesRESTLayerInfo))]
[JsonSerializable(typeof(ServicesDirectoryResponse))]
internal partial class GeoservicesJsonSourceGenerationContext : JsonSerializerContext
{
}

/// <summary>
/// Extension methods for using GeoservicesREST JSON source generation.
/// </summary>
public static class GeoservicesJsonSerializationExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        TypeInfoResolver = GeoservicesJsonSourceGenerationContext.Default,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a GeoservicesRESTFeatureSetResponse using source-generated JSON serialization.
    /// ~2-3x faster than reflection-based serialization.
    /// </summary>
    public static byte[] SerializeToUtf8BytesFast(this GeoservicesRESTFeatureSetResponse response)
    {
        return JsonSerializer.SerializeToUtf8Bytes(response,
            GeoservicesJsonSourceGenerationContext.Default.GeoservicesRESTFeatureSetResponse);
    }

    /// <summary>
    /// Serializes a GeoservicesRESTFeature using source-generated JSON serialization.
    /// ~2-3x faster than reflection-based serialization.
    /// </summary>
    public static string SerializeFast(this GeoservicesRESTFeature feature)
    {
        return JsonSerializer.Serialize(feature,
            GeoservicesJsonSourceGenerationContext.Default.GeoservicesRESTFeature);
    }

    /// <summary>
    /// Gets the default JsonSerializerOptions with GeoservicesREST source generation enabled.
    /// </summary>
    public static JsonSerializerOptions GetGeoservicesFastSerializerOptions()
    {
        return DefaultOptions;
    }
}
