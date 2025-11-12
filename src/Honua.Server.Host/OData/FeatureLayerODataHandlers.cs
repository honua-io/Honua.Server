// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.OData;
using Honua.Server.Core.OData.Query;
using NetTopologySuite.IO;

namespace Honua.Server.Host.OData;

/// <summary>
/// HTTP handlers for OData v4 endpoints for feature layers.
/// Implements OData query protocol without Microsoft dependencies for AOT compatibility.
/// </summary>
public static class FeatureLayerODataHandlers
{
    // ========================================
    // Service Root & Metadata
    // ========================================

    /// <summary>
    /// GET /odata - OData service document listing all available entity sets.
    /// </summary>
    public static async Task<IResult> GetServiceDocument(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        CancellationToken ct = default)
    {
        var baseUrl = GetBaseUrl(context);
        var entitySets = new List<object>();

        // Get metadata snapshot
        var snapshot = await metadataRegistry.GetSnapshotAsync(ct);

        foreach (var service in snapshot.Services)
        {
            foreach (var layer in service.Layers)
            {
                // Entity set name is {serviceId}_{layerId} with hyphens converted to underscores
                var entitySetName = $"{service.Id}_{layer.Id}".Replace("-", "_");
                entitySets.Add(new
                {
                    name = entitySetName,
                    kind = "EntitySet",
                    url = $"{baseUrl}/odata/{entitySetName}"
                });
            }
        }

        var serviceDoc = new Dictionary<string, object?>
        {
            ["@odata.context"] = $"{baseUrl}/odata/$metadata",
            ["value"] = entitySets
        };

        return Results.Json(serviceDoc, CreateJsonOptions());
    }

    /// <summary>
    /// GET /odata/$metadata - OData metadata document (EDMX).
    /// </summary>
    public static async Task<IResult> GetMetadata(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        CancellationToken ct = default)
    {
        // Get metadata snapshot
        var snapshot = await metadataRegistry.GetSnapshotAsync(ct);
        var entitySets = new List<string>();

        foreach (var service in snapshot.Services)
        {
            foreach (var layer in service.Layers)
            {
                var entitySetName = $"{service.Id}_{layer.Id}".Replace("-", "_");
                var entityTypeName = $"{entitySetName}Type";
                entitySets.Add($@"
    <EntityType Name=""{entityTypeName}"">
      <Key>
        <PropertyRef Name=""id""/>
      </Key>
      <Property Name=""id"" Type=""Edm.String"" Nullable=""false""/>
      <Property Name=""geometry"" Type=""Edm.GeometryPoint""/>
    </EntityType>
    <EntitySet Name=""{entitySetName}"" EntityType=""Default.{entityTypeName}""/>");
            }
        }

        var edmx = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices>
    <Schema Namespace=""Default"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
{string.Join(Environment.NewLine, entitySets)}
      <EntityContainer Name=""DefaultContainer"">
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

        return Results.Content(edmx, "application/xml");
    }

    // ========================================
    // Collection Queries
    // ========================================

    /// <summary>
    /// GET /odata/{service}_{layer} - Query feature collection with OData query options.
    /// Supports $filter, $select, $orderby, $top, $skip, $count, and spatial functions.
    /// </summary>
    public static async Task<IResult> GetFeatureCollection(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IDataStoreProvider dataStore,
        string entitySetName,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        // Parse entity set name to get service and layer IDs
        var (serviceId, layerId) = ParseEntitySetName(entitySetName);

        // Resolve metadata
        var (dataSource, service, layer) = await ResolveMetadata(metadataRegistry, serviceId, layerId, ct);

        // Parse OData query options
        var queryOptions = QueryOptionsParser.Parse(filter, null, select, orderby, top, skip, count);

        // Convert to FeatureQuery
        var featureQuery = ODataQueryAdapter.ToFeatureQuery(queryOptions);

        // Execute query
        var features = new List<FeatureRecord>();
        await foreach (var feature in dataStore.QueryAsync(dataSource, service, layer, featureQuery, ct))
        {
            features.Add(feature);
        }

        // Get count if requested
        long? totalCount = null;
        if (count)
        {
            totalCount = await dataStore.CountAsync(dataSource, service, layer, featureQuery, ct);
        }

        // Convert to OData entities
        var entities = features.Select(f => ConvertFeatureToEntity(f)).ToList();

        var baseUrl = GetBaseUrl(context);

        // Build response with optional @odata.count property
        var response = totalCount.HasValue
            ? new Dictionary<string, object?>
            {
                ["@odata.context"] = $"{baseUrl}/odata/$metadata#{entitySetName}",
                ["@odata.count"] = totalCount.Value,
                ["value"] = entities
            }
            : new Dictionary<string, object?>
            {
                ["@odata.context"] = $"{baseUrl}/odata/$metadata#{entitySetName}",
                ["value"] = entities
            };

        return Results.Json(response, CreateJsonOptions());
    }

    /// <summary>
    /// GET /odata/{service}_{layer}({id}) - Get single feature by ID.
    /// </summary>
    public static async Task<IResult> GetFeature(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IDataStoreProvider dataStore,
        string entitySetName,
        string id,
        [FromQuery(Name = "$select")] string? select,
        CancellationToken ct = default)
    {
        var (serviceId, layerId) = ParseEntitySetName(entitySetName);
        var (dataSource, service, layer) = await ResolveMetadata(metadataRegistry, serviceId, layerId, ct);

        // Parse select
        var queryOptions = QueryOptionsParser.Parse(null, null, select, null, null, null, false);
        var featureQuery = ODataQueryAdapter.ToFeatureQuery(queryOptions);

        // Get feature
        var feature = await dataStore.GetAsync(dataSource, service, layer, id, featureQuery, ct);

        if (feature == null)
            return Results.NotFound(new { error = $"Feature {id} not found" });

        var entity = ConvertFeatureToEntity(feature);
        return Results.Json(entity, CreateJsonOptions());
    }

    // ========================================
    // Create (POST)
    // ========================================

    /// <summary>
    /// POST /odata/{service}_{layer} - Create new feature.
    /// Supports full edit capability including geometry and all properties.
    /// </summary>
    public static async Task<IResult> CreateFeature(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IDataStoreProvider dataStore,
        string entitySetName,
        [FromBody] JsonElement body,
        CancellationToken ct = default)
    {
        var (serviceId, layerId) = ParseEntitySetName(entitySetName);
        var (dataSource, service, layer) = await ResolveMetadata(metadataRegistry, serviceId, layerId, ct);

        // Convert JSON body to FeatureRecord
        var record = ConvertEntityToFeature(body);

        // Create feature
        var created = await dataStore.CreateAsync(dataSource, service, layer, record, null, ct);

        var entity = ConvertFeatureToEntity(created);
        var baseUrl = GetBaseUrl(context);
        var location = $"{baseUrl}/odata/{entitySetName}({created.Attributes["id"]})";

        return Results.Created(location, entity);
    }

    // ========================================
    // Update (PATCH)
    // ========================================

    /// <summary>
    /// PATCH /odata/{service}_{layer}({id}) - Update existing feature.
    /// Supports partial updates and spatial geometry modifications.
    /// </summary>
    public static async Task<IResult> UpdateFeature(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IDataStoreProvider dataStore,
        string entitySetName,
        string id,
        [FromBody] JsonElement body,
        CancellationToken ct = default)
    {
        var (serviceId, layerId) = ParseEntitySetName(entitySetName);
        var (dataSource, service, layer) = await ResolveMetadata(metadataRegistry, serviceId, layerId, ct);

        // Convert JSON body to FeatureRecord
        var record = ConvertEntityToFeature(body);

        // Update feature
        var updated = await dataStore.UpdateAsync(dataSource, service, layer, id, record, null, ct);

        if (updated == null)
            return Results.NotFound(new { error = $"Feature {id} not found" });

        var entity = ConvertFeatureToEntity(updated);
        return Results.Ok(entity);
    }

    // ========================================
    // Delete (DELETE)
    // ========================================

    /// <summary>
    /// DELETE /odata/{service}_{layer}({id}) - Delete feature.
    /// </summary>
    public static async Task<IResult> DeleteFeature(
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IDataStoreProvider dataStore,
        string entitySetName,
        string id,
        CancellationToken ct = default)
    {
        var (serviceId, layerId) = ParseEntitySetName(entitySetName);
        var (dataSource, service, layer) = await ResolveMetadata(metadataRegistry, serviceId, layerId, ct);

        // Delete feature
        var deleted = await dataStore.DeleteAsync(dataSource, service, layer, id, null, ct);

        if (!deleted)
            return Results.NotFound(new { error = $"Feature {id} not found" });

        return Results.NoContent();
    }

    // ========================================
    // Count ($count)
    // ========================================

    /// <summary>
    /// GET /odata/{service}_{layer}/$count - Get count of features.
    /// Returns the count as plain text per OData v4 spec.
    /// </summary>
    public static async Task<IResult> GetCollectionCount(
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IDataStoreProvider dataStore,
        string entitySetName,
        [FromQuery(Name = "$filter")] string? filter,
        CancellationToken ct = default)
    {
        var (serviceId, layerId) = ParseEntitySetName(entitySetName);
        var (dataSource, service, layer) = await ResolveMetadata(metadataRegistry, serviceId, layerId, ct);

        // Parse filter if provided
        var queryOptions = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);
        var featureQuery = ODataQueryAdapter.ToFeatureQuery(queryOptions);

        // Get count
        var count = await dataStore.CountAsync(dataSource, service, layer, featureQuery, ct);

        // Return as plain text per OData spec
        return Results.Content(count.ToString(), "text/plain");
    }

    // ========================================
    // Helper Methods
    // ========================================

    private static (string serviceId, string layerId) ParseEntitySetName(string entitySetName)
    {
        // Entity set name format: {serviceId}_{layerId} with hyphens converted to underscores
        // We need to handle the case where service or layer IDs might contain underscores
        var parts = entitySetName.Split('_');

        if (parts.Length < 2)
            throw new ArgumentException($"Invalid entity set name: {entitySetName}");

        // For now, assume serviceId is first part and layerId is remaining parts joined
        // This is a simplification - in production might need more sophisticated parsing
        var serviceId = parts[0].Replace("_", "-");
        var layerId = string.Join("_", parts.Skip(1)).Replace("_", "-");

        return (serviceId, layerId);
    }

    private static async Task<(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer)>
        ResolveMetadata(IMetadataRegistry metadataRegistry, string serviceId, string layerId, CancellationToken ct)
    {
        // Get metadata snapshot
        var snapshot = await metadataRegistry.GetSnapshotAsync(ct);

        // Find service
        var service = snapshot.Services.FirstOrDefault(s =>
            string.Equals(s.Id, serviceId, StringComparison.OrdinalIgnoreCase));
        if (service == null)
            throw new InvalidOperationException($"Service {serviceId} not found");

        // Find layer in service
        var layer = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
        if (layer == null)
            throw new InvalidOperationException($"Layer {layerId} not found in service {serviceId}");

        // Find data source
        var dataSource = snapshot.DataSources.FirstOrDefault(ds =>
            string.Equals(ds.Id, service.DataSourceId, StringComparison.OrdinalIgnoreCase));
        if (dataSource == null)
            throw new InvalidOperationException($"Data source {service.DataSourceId} not found");

        return (dataSource, service, layer);
    }

    private static object ConvertFeatureToEntity(FeatureRecord feature)
    {
        // Convert FeatureRecord attributes to a dictionary suitable for JSON serialization
        var entity = new Dictionary<string, object?>();

        foreach (var (key, value) in feature.Attributes)
        {
            // Handle geometry specially - convert to GeoJSON object
            if (value is NetTopologySuite.Geometries.Geometry geometry)
            {
                var writer = new GeoJsonWriter();
                var geoJson = writer.Write(geometry);
                // Deserialize GeoJSON string to object (Dictionary/List) for proper serialization
                entity[key] = JsonSerializer.Deserialize<object>(geoJson);
            }
            else
            {
                entity[key] = value;
            }
        }

        return entity;
    }

    private static FeatureRecord ConvertEntityToFeature(JsonElement entity)
    {
        var attributes = new Dictionary<string, object?>();
        var geoJsonReader = new GeoJsonReader();

        foreach (var property in entity.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;

            // Handle geometry - check if it's a GeoJSON object
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("type", out _))
            {
                // Likely a GeoJSON geometry
                try
                {
                    var geoJson = value.GetRawText();
                    var geometry = geoJsonReader.Read<NetTopologySuite.Geometries.Geometry>(geoJson);
                    attributes[name] = geometry;
                }
                catch
                {
                    // Not a geometry, treat as regular object
                    attributes[name] = JsonSerializer.Deserialize<object>(value.GetRawText());
                }
            }
            else
            {
                // Handle primitive types
                attributes[name] = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => JsonSerializer.Deserialize<object>(value.GetRawText())
                };
            }
        }

        return new FeatureRecord(attributes);
    }

    private static string GetBaseUrl(HttpContext context)
    {
        return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
}
