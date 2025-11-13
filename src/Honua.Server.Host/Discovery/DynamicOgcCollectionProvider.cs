// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Discovery;

/// <summary>
/// Provides dynamically generated OGC API Features collections from auto-discovered tables.
/// </summary>
public sealed class DynamicOgcCollectionProvider
{
    private readonly ITableDiscoveryService discoveryService;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly AutoDiscoveryOptions options;
    private readonly ILogger<DynamicOgcCollectionProvider> logger;

    public DynamicOgcCollectionProvider(
        ITableDiscoveryService discoveryService,
        IMetadataRegistry metadataRegistry,
        IOptions<AutoDiscoveryOptions> options,
        ILogger<DynamicOgcCollectionProvider> logger)
    {
        this.discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        this.metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all auto-discovered collections for OGC API Features.
    /// </summary>
    public async Task<IEnumerable<OgcCollection>> GetCollectionsAsync(
        string dataSourceId,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (!this.options.Enabled || !this.options.DiscoverPostGISTablesAsOgcCollections)
        {
            return Array.Empty<OgcCollection>();
        }

        var tables = await this.discoveryService.DiscoverTablesAsync(dataSourceId, cancellationToken);

        var collections = new List<OgcCollection>();

        foreach (var table in tables)
        {
            var collection = CreateOgcCollection(table, httpContext);
            collections.Add(collection);
        }

        this.logger.LogDebug("Generated {Count} OGC collections from discovery", collections.Count);

        return collections;
    }

    /// <summary>
    /// Gets a specific auto-discovered collection by ID.
    /// </summary>
    public async Task<OgcCollection?> GetCollectionAsync(
        string dataSourceId,
        string collectionId,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (!this.options.Enabled || !this.options.DiscoverPostGISTablesAsOgcCollections)
        {
            return null;
        }

        // Collection ID format: schema_tablename
        var parts = collectionId.Split('_', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var schema = parts[0];
        var tableName = parts[1];
        var qualifiedName = $"{schema}.{tableName}";

        var table = await this.discoveryService.DiscoverTableAsync(dataSourceId, qualifiedName, cancellationToken);

        if (table == null)
        {
            return null;
        }

        return CreateOgcCollection(table, httpContext);
    }

    private OgcCollection CreateOgcCollection(DiscoveredTable table, HttpContext httpContext)
    {
        var collectionId = GetCollectionId(table);
        var friendlyName = this.options.UseFriendlyNames
            ? table.TableName.Humanize(LetterCasing.Title)
            : table.TableName;

        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";

        // Build CRS list
        var crs = new List<string>
        {
            $"http://www.opengis.net/def/crs/EPSG/0/{table.SRID}",
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
        };

        // Build extent
        OgcSpatialExtent? spatialExtent = null;
        if (table.Extent != null)
        {
            spatialExtent = new OgcSpatialExtent
            {
                Bbox = new[] { table.Extent.ToArray() },
                Crs = crs[0]
            };
        }

        // Build links
        var links = new List<OgcLink>
        {
            new OgcLink(
                $"{baseUrl}/collections/{collectionId}",
                "self",
                "application/json",
                "This collection"),
            new OgcLink(
                $"{baseUrl}/collections/{collectionId}/items",
                "items",
                "application/geo+json",
                "Items in this collection"),
            new OgcLink(
                $"{baseUrl}/collections/{collectionId}/items?f=html",
                "items",
                "text/html",
                "Items in this collection (HTML)"),
            new OgcLink(
                $"{baseUrl}/collections/{collectionId}/schema",
                "describedby",
                "application/schema+json",
                "Schema for this collection")
        };

        // Add queryables link
        if (this.options.GenerateOpenApiDocs)
        {
            links.Add(new OgcLink(
                $"{baseUrl}/collections/{collectionId}/queryables",
                "http://www.opengis.net/def/rel/ogc/1.0/queryables",
                "application/schema+json",
                "Queryable properties for this collection"));
        }

        return new OgcCollection
        {
            Id = collectionId,
            Title = friendlyName,
            Description = table.Description ?? $"Auto-discovered table from {table.QualifiedName}",
            Keywords = new[] { "auto-discovered", table.Schema, table.TableName },
            Crs = crs,
            StorageCrs = crs[0],
            Extent = new OgcExtent
            {
                Spatial = spatialExtent
            },
            ItemType = "feature",
            Links = links
        };
    }

    /// <summary>
    /// Generates queryables schema for a discovered collection.
    /// </summary>
    public async Task<Dictionary<string, object>> GetQueryablesAsync(
        string dataSourceId,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        var parts = collectionId.Split('_', 2);
        if (parts.Length != 2)
        {
            return new Dictionary<string, object>();
        }

        var qualifiedName = $"{parts[0]}.{parts[1]}";
        var table = await this.discoveryService.DiscoverTableAsync(dataSourceId, qualifiedName, cancellationToken);

        if (table == null)
        {
            return new Dictionary<string, object>();
        }

        var properties = new Dictionary<string, object>();

        // Add primary key
        properties[table.PrimaryKeyColumn] = new Dictionary<string, object>
        {
            ["type"] = GetJsonSchemaType(table.Columns[table.PrimaryKeyColumn].DataType),
            ["title"] = table.Columns[table.PrimaryKeyColumn].Alias ?? table.PrimaryKeyColumn
        };

        // Add geometry
        properties[table.GeometryColumn] = new Dictionary<string, object>
        {
            ["$ref"] = "https://geojson.org/schema/Geometry.json",
            ["title"] = "Geometry"
        };

        // Add all other columns
        foreach (var column in table.Columns.Values.Where(c => !c.IsPrimaryKey))
        {
            properties[column.Name] = new Dictionary<string, object>
            {
                ["type"] = GetJsonSchemaType(column.DataType),
                ["title"] = column.Alias ?? column.Name
            };

            if (column.IsNullable)
            {
                ((Dictionary<string, object>)properties[column.Name])["nullable"] = true;
            }
        }

        return new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2019-09/schema",
            ["$id"] = $"https://honua.io/schemas/collections/{collectionId}/queryables",
            ["type"] = "object",
            ["title"] = "Queryables for " + (table.TableName.Humanize(LetterCasing.Title)),
            ["properties"] = properties
        };
    }

    private static string GetCollectionId(DiscoveredTable table)
    {
        return $"{table.Schema}_{table.TableName}";
    }

    private static string GetJsonSchemaType(string honuaDataType)
    {
        return honuaDataType.ToLowerInvariant() switch
        {
            "string" => "string",
            "int16" or "int32" or "int64" => "integer",
            "float32" or "float64" or "decimal" => "number",
            "boolean" => "boolean",
            "date" or "datetime" or "datetimeoffset" => "string",
            "guid" => "string",
            "json" => "object",
            _ => "string"
        };
    }
}

/// <summary>
/// OGC API Features collection metadata.
/// </summary>
public sealed class OgcCollection
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Crs { get; init; } = Array.Empty<string>();
    public string? StorageCrs { get; init; }
    public OgcExtent? Extent { get; init; }
    public string ItemType { get; init; } = "feature";
    public IReadOnlyList<OgcLink> Links { get; init; } = Array.Empty<OgcLink>();
}

/// <summary>
/// OGC extent (spatial and temporal).
/// </summary>
public sealed class OgcExtent
{
    public OgcSpatialExtent? Spatial { get; init; }
    public OgcTemporalExtent? Temporal { get; init; }
}

/// <summary>
/// OGC spatial extent.
/// </summary>
public sealed class OgcSpatialExtent
{
    public required IReadOnlyList<double[]> Bbox { get; init; }
    public required string Crs { get; init; }
}

/// <summary>
/// OGC temporal extent.
/// </summary>
public sealed class OgcTemporalExtent
{
    public IReadOnlyList<(DateTimeOffset? Start, DateTimeOffset? End)> Interval { get; init; } = Array.Empty<(DateTimeOffset?, DateTimeOffset?)>();
    public string? Trs { get; init; }
}
