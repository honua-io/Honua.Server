using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Utility class that consolidates database seeding logic for integration tests.
/// Provides reusable methods for seeding GIS features, STAC collections, and test data.
/// </summary>
/// <remarks>
/// <para>
/// This class eliminates ~300 lines of duplicate database seeding logic across
/// 5+ test fixtures by providing common seeding patterns for:
/// <list type="bullet">
/// <item>PostGIS database tables with geometry columns</item>
/// <item>MySQL/MariaDB spatial tables</item>
/// <item>STAC catalog collections and items</item>
/// <item>Test feature data with realistic attributes</item>
/// </list>
/// </para>
/// <para>
/// Uses <see cref="GeometryTestData"/> and <see cref="RealisticGisTestData"/>
/// for high-quality test geometries and attributes.
/// </para>
/// </remarks>
/// <example>
/// Seeding a PostGIS table:
/// <code>
/// using var connection = new NpgsqlConnection(connectionString);
/// await connection.OpenAsync();
///
/// var service = new ServiceDefinition { Id = "roads", ... };
/// var layer = new LayerDefinition { Id = "roads-primary", ... };
///
/// await TestDatabaseSeeder.SeedPostGisTableAsync(connection, service, layer, featureCount: 100);
/// </code>
/// </example>
public static class TestDatabaseSeeder
{
    private static readonly WKTWriter WktWriter = new();

    /// <summary>
    /// Seeds a PostGIS table with test feature data.
    /// Creates the table if it doesn't exist, then inserts features with geometries.
    /// </summary>
    /// <param name="connection">The database connection (must be open).</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition containing table and field information.</param>
    /// <param name="featureCount">Number of features to insert (default: 10).</param>
    /// <param name="transaction">Optional transaction to use (default: null).</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">If any required parameter is null.</exception>
    /// <exception cref="InvalidOperationException">If layer storage configuration is missing.</exception>
    public static async Task SeedPostGisTableAsync(
        IDbConnection connection,
        ServiceDefinition service,
        LayerDefinition layer,
        int featureCount = 10,
        IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layer);

        if (layer.Storage is null)
        {
            throw new InvalidOperationException($"Layer {layer.Id} is missing storage configuration.");
        }

        var tableName = layer.Storage.Table ?? throw new InvalidOperationException("Table name is required.");
        var geometryColumn = layer.Storage.GeometryColumn ?? "geom";
        var primaryKey = layer.Storage.PrimaryKey ?? layer.IdField ?? "id";
        var srid = layer.Storage.Srid ?? 4326;

        // Drop and recreate table
        var dropTableSql = $"DROP TABLE IF EXISTS {tableName}";
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = dropTableSql;
            cmd.Transaction = transaction;
            await ExecuteNonQueryAsync(cmd);
        }

        // Build CREATE TABLE statement
        var createTableSql = BuildPostGisCreateTableSql(
            tableName,
            primaryKey,
            geometryColumn,
            layer.Fields,
            layer.GeometryType ?? "Point",
            srid
        );

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            cmd.Transaction = transaction;
            await ExecuteNonQueryAsync(cmd);
        }

        // Insert features
        for (var i = 1; i <= featureCount; i++)
        {
            var attributes = CreateTestFeatureAttributes(layer, i);
            var geometry = CreateTestGeometry(layer.GeometryType ?? "Point", i, featureCount);

            await InsertPostGisFeatureAsync(
                connection,
                tableName,
                primaryKey,
                geometryColumn,
                attributes,
                geometry,
                srid,
                transaction
            );
        }
    }

    /// <summary>
    /// Seeds a MySQL/MariaDB spatial table with test feature data.
    /// Similar to PostGIS seeding but uses MySQL spatial syntax.
    /// </summary>
    /// <param name="connection">The database connection (must be open).</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition containing table and field information.</param>
    /// <param name="featureCount">Number of features to insert (default: 10).</param>
    /// <param name="transaction">Optional transaction to use (default: null).</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task SeedMySqlTableAsync(
        IDbConnection connection,
        ServiceDefinition service,
        LayerDefinition layer,
        int featureCount = 10,
        IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layer);

        if (layer.Storage is null)
        {
            throw new InvalidOperationException($"Layer {layer.Id} is missing storage configuration.");
        }

        var tableName = layer.Storage.Table ?? throw new InvalidOperationException("Table name is required.");
        var geometryColumn = layer.Storage.GeometryColumn ?? "geom";
        var primaryKey = layer.Storage.PrimaryKey ?? layer.IdField ?? "id";
        var srid = layer.Storage.Srid ?? 4326;

        // Drop and recreate table
        var dropTableSql = $"DROP TABLE IF EXISTS {tableName}";
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = dropTableSql;
            cmd.Transaction = transaction;
            await ExecuteNonQueryAsync(cmd);
        }

        // Build CREATE TABLE statement (MySQL syntax)
        var createTableSql = BuildMySqlCreateTableSql(
            tableName,
            primaryKey,
            geometryColumn,
            layer.Fields,
            layer.GeometryType ?? "Point",
            srid
        );

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            cmd.Transaction = transaction;
            await ExecuteNonQueryAsync(cmd);
        }

        // Insert features
        for (var i = 1; i <= featureCount; i++)
        {
            var attributes = CreateTestFeatureAttributes(layer, i);
            var geometry = CreateTestGeometry(layer.GeometryType ?? "Point", i, featureCount);

            await InsertMySqlFeatureAsync(
                connection,
                tableName,
                primaryKey,
                geometryColumn,
                attributes,
                geometry,
                srid,
                transaction
            );
        }
    }

    /// <summary>
    /// Seeds STAC collections and items for testing.
    /// </summary>
    /// <param name="store">The STAC catalog store.</param>
    /// <param name="collectionCount">Number of collections to create (default: 3).</param>
    /// <param name="itemsPerCollection">Number of items per collection (default: 10).</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task SeedStacDataAsync(
        IStacCatalogStore store,
        int collectionCount = 3,
        int itemsPerCollection = 10)
    {
        ArgumentNullException.ThrowIfNull(store);

        await store.EnsureInitializedAsync();

        for (var i = 1; i <= collectionCount; i++)
        {
            var collection = CreateTestStacCollection(i);
            await store.UpsertCollectionAsync(collection);

            for (var j = 1; j <= itemsPerCollection; j++)
            {
                var item = CreateTestStacItem(collection.Id, i, j);
                await store.UpsertItemAsync(item);
            }
        }
    }

    /// <summary>
    /// Creates a collection of test features with geometries and attributes.
    /// Useful for in-memory repositories and mock data.
    /// </summary>
    /// <param name="count">Number of features to create.</param>
    /// <param name="geometryType">The geometry type (default: Point).</param>
    /// <returns>A collection of test feature records.</returns>
    public static IReadOnlyList<FeatureRecord> CreateTestFeatures(int count, string geometryType = "Point")
    {
        var features = new List<FeatureRecord>(count);

        for (var i = 1; i <= count; i++)
        {
            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["feature_id"] = i,
                ["name"] = $"Test Feature {i}",
                ["category"] = i % 2 == 0 ? "A" : "B",
                ["status"] = i % 3 == 0 ? "active" : "inactive",
                ["value"] = 100.0 * i,
                ["created_at"] = DateTimeOffset.UtcNow.AddDays(-i),
                ["geom"] = CreateGeoJsonGeometry(geometryType, i, count)
            };

            features.Add(new FeatureRecord(attributes));
        }

        return features;
    }

    /// <summary>
    /// Creates realistic road features using data from <see cref="RealisticGisTestData"/>.
    /// </summary>
    /// <param name="count">Number of road features to create.</param>
    /// <returns>A collection of realistic road feature records.</returns>
    public static IReadOnlyList<FeatureRecord> CreateRealisticRoadFeatures(int count)
    {
        var features = new List<FeatureRecord>(count);
        var cities = new[]
        {
            RealisticGisTestData.NewYork,
            RealisticGisTestData.London,
            RealisticGisTestData.Tokyo,
            RealisticGisTestData.Sydney,
            RealisticGisTestData.SaoPaulo
        };

        for (var i = 1; i <= count; i++)
        {
            var city = cities[i % cities.Length];

            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["road_id"] = i,
                ["name"] = $"Street {i}",
                ["status"] = i % 3 == 0 ? "closed" : "open",
                ["observed_at"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
                ["geom"] = new JsonObject
                {
                    ["type"] = "LineString",
                    ["coordinates"] = new JsonArray
                    {
                        new JsonArray(city.lon, city.lat),
                        new JsonArray(city.lon + 0.01, city.lat + 0.01)
                    }
                }
            };

            features.Add(new FeatureRecord(attributes));
        }

        return features;
    }

    #region Private Helper Methods

    private static string BuildPostGisCreateTableSql(
        string tableName,
        string primaryKey,
        string geometryColumn,
        IReadOnlyList<FieldDefinition> fields,
        string geometryType,
        int srid)
    {
        var columns = new List<string>
        {
            $"{primaryKey} INTEGER PRIMARY KEY"
        };

        foreach (var field in fields.Where(f => !string.Equals(f.Name, primaryKey, StringComparison.OrdinalIgnoreCase) &&
                                                  !string.Equals(f.Name, geometryColumn, StringComparison.OrdinalIgnoreCase)))
        {
            var sqlType = MapFieldTypeToPostGisSql(field.DataType, field.StorageType);
            var nullable = field.Nullable ? "NULL" : "NOT NULL";
            columns.Add($"{field.Name} {sqlType} {nullable}");
        }

        var createTable = $"CREATE TABLE {tableName} ({string.Join(", ", columns)})";

        // Add geometry column separately using AddGeometryColumn
        var addGeometry = $"SELECT AddGeometryColumn('{tableName.Split('.').Last()}', '{geometryColumn}', {srid}, '{geometryType.ToUpperInvariant()}', 2)";

        return $"{createTable}; {addGeometry}";
    }

    private static string BuildMySqlCreateTableSql(
        string tableName,
        string primaryKey,
        string geometryColumn,
        IReadOnlyList<FieldDefinition> fields,
        string geometryType,
        int srid)
    {
        var columns = new List<string>
        {
            $"`{primaryKey}` INT PRIMARY KEY"
        };

        foreach (var field in fields.Where(f => !string.Equals(f.Name, primaryKey, StringComparison.OrdinalIgnoreCase) &&
                                                  !string.Equals(f.Name, geometryColumn, StringComparison.OrdinalIgnoreCase)))
        {
            var sqlType = MapFieldTypeToMySqlSql(field.DataType, field.StorageType);
            var nullable = field.Nullable ? "NULL" : "NOT NULL";
            columns.Add($"`{field.Name}` {sqlType} {nullable}");
        }

        columns.Add($"`{geometryColumn}` {geometryType.ToUpperInvariant()} SRID {srid}");

        return $"CREATE TABLE {tableName} ({string.Join(", ", columns)})";
    }

    private static string MapFieldTypeToPostGisSql(string dataType, string? storageType)
    {
        if (!string.IsNullOrEmpty(storageType))
        {
            return storageType;
        }

        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" => "INTEGER",
            "long" or "bigint" => "BIGINT",
            "string" or "text" => "TEXT",
            "double" or "float" => "DOUBLE PRECISION",
            "decimal" => "NUMERIC",
            "boolean" or "bool" => "BOOLEAN",
            "datetime" or "datetimeoffset" => "TIMESTAMPTZ",
            "date" => "DATE",
            _ => "TEXT"
        };
    }

    private static string MapFieldTypeToMySqlSql(string dataType, string? storageType)
    {
        if (!string.IsNullOrEmpty(storageType))
        {
            return storageType;
        }

        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" => "INT",
            "long" or "bigint" => "BIGINT",
            "string" or "text" => "TEXT",
            "double" or "float" => "DOUBLE",
            "decimal" => "DECIMAL(18,6)",
            "boolean" or "bool" => "TINYINT(1)",
            "datetime" or "datetimeoffset" => "DATETIME",
            "date" => "DATE",
            _ => "TEXT"
        };
    }

    private static Dictionary<string, object?> CreateTestFeatureAttributes(LayerDefinition layer, int index)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.Storage?.GeometryColumn ?? "geom", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip geometry field
            }

            attributes[field.Name] = field.DataType.ToLowerInvariant() switch
            {
                "int" or "integer" => index,
                "long" or "bigint" => (long)index,
                "string" or "text" => $"Test {field.Name} {index}",
                "double" or "float" => 100.0 * index,
                "decimal" => (decimal)(100.0 * index),
                "boolean" or "bool" => index % 2 == 0,
                "datetime" or "datetimeoffset" => DateTimeOffset.UtcNow.AddDays(-index),
                "date" => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-index)),
                _ => $"Value {index}"
            };
        }

        return attributes;
    }

    private static NtsGeometry CreateTestGeometry(string geometryType, int index, int total)
    {
        var scenario = index % 3 == 0
            ? GeometryTestData.GeodeticScenario.Simple
            : GeometryTestData.GeodeticScenario.Simple;

        var geoType = geometryType.ToLowerInvariant() switch
        {
            "point" => GeometryTestData.GeometryType.Point,
            "linestring" => GeometryTestData.GeometryType.LineString,
            "polygon" => GeometryTestData.GeometryType.Polygon,
            "multipoint" => GeometryTestData.GeometryType.MultiPoint,
            "multilinestring" => GeometryTestData.GeometryType.MultiLineString,
            "multipolygon" => GeometryTestData.GeometryType.MultiPolygon,
            _ => GeometryTestData.GeometryType.Point
        };

        return GeometryTestData.GetTestGeometry(geoType, scenario);
    }

    private static JsonObject CreateGeoJsonGeometry(string geometryType, int index, int total)
    {
        var geometry = CreateTestGeometry(geometryType, index, total);
        var geoJson = GeometryTestData.ToGeoJson(geometry);
        return JsonNode.Parse(geoJson)?.AsObject() ?? new JsonObject();
    }

    private static async Task InsertPostGisFeatureAsync(
        IDbConnection connection,
        string tableName,
        string primaryKey,
        string geometryColumn,
        Dictionary<string, object?> attributes,
        NtsGeometry geometry,
        int srid,
        IDbTransaction? transaction)
    {
        var wkt = WktWriter.Write(geometry);
        var columns = new List<string> { primaryKey, geometryColumn };
        var values = new List<string> { $"@{primaryKey}", $"ST_GeomFromText(@wkt, {srid})" };

        foreach (var kvp in attributes)
        {
            if (!string.Equals(kvp.Key, primaryKey, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key, geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                columns.Add(kvp.Key);
                values.Add($"@{kvp.Key}");
            }
        }

        var sql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;

        AddParameter(cmd, primaryKey, attributes[primaryKey]);
        AddParameter(cmd, "wkt", wkt);

        foreach (var kvp in attributes)
        {
            if (!string.Equals(kvp.Key, primaryKey, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key, geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                AddParameter(cmd, kvp.Key, kvp.Value);
            }
        }

        await ExecuteNonQueryAsync(cmd);
    }

    private static async Task InsertMySqlFeatureAsync(
        IDbConnection connection,
        string tableName,
        string primaryKey,
        string geometryColumn,
        Dictionary<string, object?> attributes,
        NtsGeometry geometry,
        int srid,
        IDbTransaction? transaction)
    {
        var wkt = WktWriter.Write(geometry);
        var columns = new List<string> { $"`{primaryKey}`", $"`{geometryColumn}`" };
        var values = new List<string> { $"@{primaryKey}", $"ST_GeomFromText(@wkt, {srid})" };

        foreach (var kvp in attributes)
        {
            if (!string.Equals(kvp.Key, primaryKey, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key, geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                columns.Add($"`{kvp.Key}`");
                values.Add($"@{kvp.Key}");
            }
        }

        var sql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;

        AddParameter(cmd, primaryKey, attributes[primaryKey]);
        AddParameter(cmd, "wkt", wkt);

        foreach (var kvp in attributes)
        {
            if (!string.Equals(kvp.Key, primaryKey, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kvp.Key, geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                AddParameter(cmd, kvp.Key, kvp.Value);
            }
        }

        await ExecuteNonQueryAsync(cmd);
    }

    private static StacCollectionRecord CreateTestStacCollection(int index)
    {
        var now = DateTimeOffset.UtcNow;

        return new StacCollectionRecord
        {
            Id = $"test-collection-{index}",
            Title = $"Test Collection {index}",
            Description = $"Test collection for integration testing (#{index})",
            License = "proprietary",
            Keywords = new[] { "test", "integration", $"collection{index}" },
            Extent = new StacExtent
            {
                Spatial = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Temporal = new List<StacTemporalInterval>
                {
                    new() { Start = now.AddDays(-30), End = now }
                }
            },
            Properties = new JsonObject
            {
                ["test:index"] = index,
                ["test:created"] = now.ToString("O")
            },
            ServiceId = $"service-{index}",
            LayerId = $"layer-{index}",
            Extensions = Array.Empty<string>(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static StacItemRecord CreateTestStacItem(string collectionId, int collectionIndex, int itemIndex)
    {
        var now = DateTimeOffset.UtcNow;
        var itemId = $"{collectionId}-item-{itemIndex}";

        return new StacItemRecord
        {
            Id = itemId,
            CollectionId = collectionId,
            Title = $"Test Item {itemIndex}",
            Description = $"Test item {itemIndex} in collection {collectionId}",
            Properties = new JsonObject
            {
                ["test:item_index"] = itemIndex,
                ["test:collection_index"] = collectionIndex
            },
            Assets = new Dictionary<string, StacAsset>(StringComparer.OrdinalIgnoreCase)
            {
                ["data"] = new StacAsset
                {
                    Href = $"https://example.com/data/{itemId}.tif",
                    Type = "image/tiff",
                    Roles = new[] { "data" }
                }
            },
            Bbox = new[] { -122.5, 45.5, -122.4, 45.6 },
            Geometry = null,
            Datetime = now.AddHours(-itemIndex),
            StartDatetime = now.AddHours(-itemIndex - 1),
            EndDatetime = now.AddHours(-itemIndex),
            RasterDatasetId = $"dataset-{collectionIndex}",
            Extensions = Array.Empty<string>(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static void AddParameter(IDbCommand cmd, string name, object? value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
    }

    private static Task<int> ExecuteNonQueryAsync(IDbCommand cmd)
    {
        // IDbCommand doesn't have async methods, but most implementations do
        // This wrapper provides compatibility
        return Task.FromResult(cmd.ExecuteNonQuery());
    }

    #endregion
}
