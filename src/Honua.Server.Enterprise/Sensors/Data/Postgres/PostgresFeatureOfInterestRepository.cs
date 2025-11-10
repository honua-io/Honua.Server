// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// PostgreSQL implementation for FeatureOfInterest entity operations.
/// </summary>
internal sealed class PostgresFeatureOfInterestRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly string _basePath;
    private readonly GeoJsonReader _geoJsonReader;
    private readonly GeoJsonWriter _geoJsonWriter;

    public PostgresFeatureOfInterestRepository(string connectionString, string basePath, ILogger logger)
    {
        _connectionString = connectionString;
        _basePath = basePath;
        _logger = logger;
        _geoJsonReader = new GeoJsonReader();
        _geoJsonWriter = new GeoJsonWriter();
    }

    public async Task<FeatureOfInterest?> GetByIdAsync(string id, ExpandOptions? expand, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_features_of_interest
            WHERE id = @Id::uuid
            """;

        var result = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (result == null)
            return null;

        var featureOfInterest = new FeatureOfInterest
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Feature = result.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(result.feature_geojson.ToString())
                : null!,
            Properties = result.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(result.properties.ToString())
                : null,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_basePath}/FeaturesOfInterest({result.id})"
        };

        return featureOfInterest;
    }

    public async Task<PagedResult<FeatureOfInterest>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sqlBuilder = new StringBuilder("""
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_features_of_interest
            """);

        var parameters = new DynamicParameters();

        if (options.Filter != null)
        {
            sqlBuilder.AppendLine(" WHERE " + PostgresQueryHelper.TranslateFilter(options.Filter, parameters));
        }

        if (options.OrderBy?.Any() == true)
        {
            sqlBuilder.AppendLine(" ORDER BY " + string.Join(", ",
                options.OrderBy.Select(o => $"{o.Property} {(o.Direction == SortDirection.Ascending ? "ASC" : "DESC")}")));
        }
        else
        {
            sqlBuilder.AppendLine(" ORDER BY created_at DESC");
        }

        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;

        sqlBuilder.AppendLine(" LIMIT @Limit OFFSET @Offset");
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        var results = await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlBuilder.ToString(), parameters, cancellationToken: ct));

        var items = results.Select(r => new FeatureOfInterest
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            EncodingType = r.encoding_type,
            Feature = r.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(r.feature_geojson.ToString())
                : null!,
            Properties = r.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.properties.ToString())
                : null,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_basePath}/FeaturesOfInterest({r.id})"
        }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            var countSql = "SELECT COUNT(*) FROM sta_features_of_interest";
            if (options.Filter != null)
            {
                var countParams = new DynamicParameters();
                countSql += " WHERE " + PostgresQueryHelper.TranslateFilter(options.Filter, countParams);
                totalCount = await conn.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, countParams, cancellationToken: ct));
            }
            else
            {
                totalCount = await conn.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, cancellationToken: ct));
            }
        }

        return new PagedResult<FeatureOfInterest>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<FeatureOfInterest> CreateAsync(FeatureOfInterest featureOfInterest, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var featureGeoJson = featureOfInterest.Feature != null
            ? _geoJsonWriter.Write(featureOfInterest.Feature)
            : null;

        const string sql = """
            INSERT INTO sta_features_of_interest (
                name,
                description,
                encoding_type,
                feature,
                properties
            )
            VALUES (
                @Name,
                @Description,
                @EncodingType,
                ST_GeomFromGeoJSON(@FeatureGeoJson),
                @Properties::jsonb
            )
            RETURNING
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            """;

        var result = await conn.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                featureOfInterest.Name,
                featureOfInterest.Description,
                featureOfInterest.EncodingType,
                FeatureGeoJson = featureGeoJson,
                Properties = featureOfInterest.Properties != null
                    ? JsonSerializer.Serialize(featureOfInterest.Properties)
                    : null
            },
            cancellationToken: ct));

        var created = new FeatureOfInterest
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Feature = result.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(result.feature_geojson.ToString())
                : null!,
            Properties = result.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(result.properties.ToString())
                : null,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_basePath}/FeaturesOfInterest({result.id})"
        };

        _logger.LogInformation("Created FeatureOfInterest {FeatureOfInterestId}", created.Id);

        return created;
    }

    public async Task<FeatureOfInterest> UpdateAsync(string id, FeatureOfInterest featureOfInterest, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var featureGeoJson = featureOfInterest.Feature != null
            ? _geoJsonWriter.Write(featureOfInterest.Feature)
            : null;

        const string sql = """
            UPDATE sta_features_of_interest
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                encoding_type = COALESCE(@EncodingType, encoding_type),
                feature = COALESCE(ST_GeomFromGeoJSON(@FeatureGeoJson), feature),
                properties = COALESCE(@Properties::jsonb, properties),
                updated_at = now()
            WHERE id = @Id::uuid
            RETURNING
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            """;

        var result = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                Id = id,
                featureOfInterest.Name,
                featureOfInterest.Description,
                featureOfInterest.EncodingType,
                FeatureGeoJson = featureGeoJson,
                Properties = featureOfInterest.Properties != null
                    ? JsonSerializer.Serialize(featureOfInterest.Properties)
                    : null
            },
            cancellationToken: ct));

        if (result == null)
            throw new InvalidOperationException($"FeatureOfInterest {id} not found");

        var updated = new FeatureOfInterest
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Feature = result.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(result.feature_geojson.ToString())
                : null!,
            Properties = result.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(result.properties.ToString())
                : null,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_basePath}/FeaturesOfInterest({result.id})"
        };

        _logger.LogInformation("Updated FeatureOfInterest {FeatureOfInterestId}", id);

        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            DELETE FROM sta_features_of_interest
            WHERE id = @Id::uuid
            """;

        var rowsAffected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (rowsAffected == 0)
            throw new InvalidOperationException($"FeatureOfInterest {id} not found");

        _logger.LogInformation("Deleted FeatureOfInterest {FeatureOfInterestId}", id);
    }

    public async Task<FeatureOfInterest> GetOrCreateAsync(
        string name,
        string description,
        Geometry geometry,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var featureGeoJson = geometry != null ? _geoJsonWriter.Write(geometry) : null;

        // First, try to find an existing FeatureOfInterest with the same geometry
        const string findSql = """
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_features_of_interest
            WHERE ST_Equals(feature, ST_GeomFromGeoJSON(@FeatureGeoJson))
            LIMIT 1
            """;

        var existing = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(findSql, new { FeatureGeoJson = featureGeoJson }, cancellationToken: ct));

        if (existing != null)
        {
            var existingId = (string)existing.id;
            _logger.LogDebug("Found existing FeatureOfInterest {FeatureOfInterestId} with matching geometry", existingId);

            return new FeatureOfInterest
            {
                Id = existingId,
                Name = existing.name,
                Description = existing.description,
                EncodingType = existing.encoding_type,
                Feature = existing.feature_geojson != null
                    ? _geoJsonReader.Read<Geometry>(existing.feature_geojson.ToString())
                    : null!,
                Properties = existing.properties != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(existing.properties.ToString())
                    : null,
                CreatedAt = existing.created_at,
                UpdatedAt = existing.updated_at,
                SelfLink = $"{_basePath}/FeaturesOfInterest({existingId})"
            };
        }

        // If not found, create a new FeatureOfInterest
        const string createSql = """
            INSERT INTO sta_features_of_interest (
                name,
                description,
                encoding_type,
                feature
            )
            VALUES (
                @Name,
                @Description,
                'application/geo+json',
                ST_GeomFromGeoJSON(@FeatureGeoJson)
            )
            RETURNING
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            """;

        var created = await conn.QuerySingleAsync<dynamic>(
            new CommandDefinition(createSql, new
            {
                Name = name,
                Description = description,
                FeatureGeoJson = featureGeoJson
            },
            cancellationToken: ct));

        var createdId = (string)created.id;
        _logger.LogInformation("Created new FeatureOfInterest {FeatureOfInterestId}", createdId);

        return new FeatureOfInterest
        {
            Id = createdId,
            Name = created.name,
            Description = created.description,
            EncodingType = created.encoding_type,
            Feature = created.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(created.feature_geojson.ToString())
                : null!,
            Properties = created.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(created.properties.ToString())
                : null,
            CreatedAt = created.created_at,
            UpdatedAt = created.updated_at,
            SelfLink = $"{_basePath}/FeaturesOfInterest({createdId})"
        };
    }
}
