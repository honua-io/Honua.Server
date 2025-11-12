// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Server.Core.Models.Drone;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Core.DataOperations.Drone;

/// <summary>
/// PostgreSQL implementation of drone data repository
/// </summary>
public class DroneDataRepository : IDroneDataRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DroneDataRepository> _logger;

    public DroneDataRepository(
        NpgsqlDataSource dataSource,
        ILogger<DroneDataRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    #region Survey Operations

    public async Task<DroneSurvey> CreateSurveyAsync(
        CreateDroneSurveyDto dto,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO drone_surveys (
                name, description, survey_date, flight_altitude_m,
                ground_resolution_cm, coverage_area, metadata
            )
            VALUES (
                @name, @description, @surveyDate, @altitude,
                @resolution, ST_GeomFromGeoJSON(@coverageArea), @metadata
            )
            RETURNING id, name, description, survey_date, flight_altitude_m,
                      ground_resolution_cm, area_sqm, point_count,
                      ST_AsGeoJSON(coverage_area) as coverage_area,
                      orthophoto_url, dem_url, metadata, created_at, updated_at";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("name", dto.Name);
        cmd.Parameters.AddWithValue("description", (object?)dto.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("surveyDate", dto.SurveyDate);
        cmd.Parameters.AddWithValue("altitude", (object?)dto.FlightAltitudeM ?? DBNull.Value);
        cmd.Parameters.AddWithValue("resolution", (object?)dto.GroundResolutionCm ?? DBNull.Value);
        cmd.Parameters.AddWithValue("coverageArea",
            dto.CoverageArea != null ? JsonSerializer.Serialize(dto.CoverageArea) : DBNull.Value);
        cmd.Parameters.AddWithValue("metadata",
            dto.Metadata != null ? JsonSerializer.Serialize(dto.Metadata) : DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return MapDroneSurvey(reader);
    }

    public async Task<DroneSurvey?> GetSurveyAsync(Guid surveyId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, name, description, survey_date, flight_altitude_m,
                   ground_resolution_cm, area_sqm, point_count,
                   ST_AsGeoJSON(coverage_area) as coverage_area,
                   orthophoto_url, dem_url, metadata, created_at, updated_at
            FROM drone_surveys
            WHERE id = @surveyId";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapDroneSurvey(reader);
    }

    public async Task<IEnumerable<DroneSurveySummary>> ListSurveysAsync(
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT s.id, s.name, s.survey_date, s.point_count, s.area_sqm,
                   s.orthophoto_url IS NOT NULL as has_orthophoto,
                   EXISTS(SELECT 1 FROM drone_point_clouds pc WHERE pc.survey_id = s.id LIMIT 1) as has_point_cloud,
                   EXISTS(SELECT 1 FROM drone_3d_models m WHERE m.survey_id = s.id LIMIT 1) as has_3d_model
            FROM drone_surveys s
            ORDER BY s.survey_date DESC
            LIMIT @limit OFFSET @offset";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

        var surveys = new List<DroneSurveySummary>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            surveys.Add(new DroneSurveySummary
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                SurveyDate = reader.GetDateTime(2),
                PointCount = reader.GetInt64(3),
                AreaSqm = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                HasOrthophoto = reader.GetBoolean(5),
                HasPointCloud = reader.GetBoolean(6),
                Has3DModel = reader.GetBoolean(7)
            });
        }

        return surveys;
    }

    public async Task<bool> DeleteSurveyAsync(Guid surveyId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM drone_surveys WHERE id = @surveyId";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task UpdateSurveyStatisticsAsync(Guid surveyId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT update_drone_survey_stats(@surveyId)";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Point Cloud Operations

    public async IAsyncEnumerable<PointCloudPoint> QueryPointCloudAsync(
        Guid surveyId,
        PointCloudQueryOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bbox = options.BoundingBox ?? BoundingBox3D.World;

        var sql = $@"
            SELECT * FROM get_drone_points_in_bbox(
                @surveyId::uuid,
                @minX::double precision,
                @minY::double precision,
                @maxX::double precision,
                @maxY::double precision,
                @lodLevel::integer,
                @classifications::integer[]
            )
            OFFSET @offset
            {(options.Limit.HasValue ? "LIMIT @limit" : "")}";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);
        cmd.Parameters.AddWithValue("minX", bbox.MinX);
        cmd.Parameters.AddWithValue("minY", bbox.MinY);
        cmd.Parameters.AddWithValue("maxX", bbox.MaxX);
        cmd.Parameters.AddWithValue("maxY", bbox.MaxY);
        cmd.Parameters.AddWithValue("lodLevel", (int)options.LodLevel);
        cmd.Parameters.AddWithValue("classifications",
            options.ClassificationFilter != null ? (object)options.ClassificationFilter : DBNull.Value);
        cmd.Parameters.AddWithValue("offset", options.Offset);

        if (options.Limit.HasValue)
            cmd.Parameters.AddWithValue("limit", options.Limit.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new PointCloudPoint(
                X: reader.GetDouble(0),
                Y: reader.GetDouble(1),
                Z: reader.GetDouble(2),
                Red: (ushort)reader.GetInt32(3),
                Green: (ushort)reader.GetInt32(4),
                Blue: (ushort)reader.GetInt32(5),
                Classification: (byte)reader.GetInt32(6),
                Intensity: reader.IsDBNull(7) ? null : (ushort)reader.GetInt32(7)
            );
        }
    }

    public async Task<PointCloudStatistics> GetPointCloudStatisticsAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) as total_points,
                MIN(PC_PatchMin(pa, 'X')) as min_x,
                MIN(PC_PatchMin(pa, 'Y')) as min_y,
                MIN(PC_PatchMin(pa, 'Z')) as min_z,
                MAX(PC_PatchMax(pa, 'X')) as max_x,
                MAX(PC_PatchMax(pa, 'Y')) as max_y,
                MAX(PC_PatchMax(pa, 'Z')) as max_z
            FROM drone_point_clouds
            WHERE survey_id = @surveyId AND lod_level = 0";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PointCloudStatistics { TotalPoints = 0 };
        }

        var stats = new PointCloudStatistics
        {
            TotalPoints = reader.GetInt64(0)
        };

        if (!reader.IsDBNull(1))
        {
            stats.BoundingBox = new BoundingBox3D(
                MinX: reader.GetDouble(1),
                MinY: reader.GetDouble(2),
                MinZ: reader.GetDouble(3),
                MaxX: reader.GetDouble(4),
                MaxY: reader.GetDouble(5),
                MaxZ: reader.GetDouble(6)
            );
            stats.MinZ = reader.GetDouble(3);
            stats.MaxZ = reader.GetDouble(6);
        }

        return stats;
    }

    public async Task<long> InsertPointCloudDataAsync(
        Guid surveyId,
        IEnumerable<PointCloudPoint> points,
        int lodLevel = 0,
        CancellationToken cancellationToken = default)
    {
        // This is a simplified version - in production, you'd use PDAL pipelines
        // For now, this demonstrates the concept
        _logger.LogWarning(
            "InsertPointCloudDataAsync is a stub. Use PDAL pipelines for production LAZ import.");

        // In a real implementation, you would:
        // 1. Write points to a temporary LAZ file
        // 2. Run a PDAL pipeline to import into PostGIS
        // 3. Update survey statistics

        var pointList = points.ToList();
        _logger.LogInformation(
            "Would insert {Count} points for survey {SurveyId} at LOD {LOD}",
            pointList.Count, surveyId, lodLevel);

        return pointList.Count;
    }

    #endregion

    #region Orthomosaic Operations

    public async Task<DroneOrthomosaic> CreateOrthomosaicAsync(
        CreateOrthomosaicDto dto,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO drone_orthomosaics (
                survey_id, name, raster_path, storage_url, bounds,
                resolution_cm, metadata
            )
            VALUES (
                @surveyId, @name, @rasterPath, @storageUrl,
                ST_GeomFromGeoJSON(@bounds), @resolution, @metadata
            )
            RETURNING id, survey_id, name, raster_path, storage_url,
                      ST_AsGeoJSON(bounds) as bounds, resolution_cm,
                      tile_matrix_set, format, metadata, created_at";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", dto.SurveyId);
        cmd.Parameters.AddWithValue("name", dto.Name);
        cmd.Parameters.AddWithValue("rasterPath", dto.RasterPath);
        cmd.Parameters.AddWithValue("storageUrl", (object?)dto.StorageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bounds",
            dto.Bounds != null ? JsonSerializer.Serialize(dto.Bounds) : DBNull.Value);
        cmd.Parameters.AddWithValue("resolution", dto.ResolutionCm);
        cmd.Parameters.AddWithValue("metadata",
            dto.Metadata != null ? JsonSerializer.Serialize(dto.Metadata) : DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return MapOrthomosaic(reader);
    }

    public async Task<DroneOrthomosaic?> GetOrthomosaicAsync(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, survey_id, name, raster_path, storage_url,
                   ST_AsGeoJSON(bounds) as bounds, resolution_cm,
                   tile_matrix_set, format, metadata, created_at
            FROM drone_orthomosaics
            WHERE id = @orthomosaicId";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("orthomosaicId", orthomosaicId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapOrthomosaic(reader);
    }

    public async Task<IEnumerable<DroneOrthomosaic>> ListOrthomosaicsAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, survey_id, name, raster_path, storage_url,
                   ST_AsGeoJSON(bounds) as bounds, resolution_cm,
                   tile_matrix_set, format, metadata, created_at
            FROM drone_orthomosaics
            WHERE survey_id = @surveyId
            ORDER BY created_at DESC";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);

        var orthomosaics = new List<DroneOrthomosaic>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            orthomosaics.Add(MapOrthomosaic(reader));
        }

        return orthomosaics;
    }

    #endregion

    #region 3D Model Operations

    public async Task<Drone3DModel> Create3DModelAsync(
        Create3DModelDto dto,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO drone_3d_models (
                survey_id, name, model_type, model_path, storage_url,
                bounds, metadata
            )
            VALUES (
                @surveyId, @name, @modelType, @modelPath, @storageUrl,
                ST_GeomFromGeoJSON(@bounds), @metadata
            )
            RETURNING id, survey_id, name, model_type, model_path, storage_url,
                      ST_AsGeoJSON(bounds) as bounds, vertex_count, texture_count,
                      file_size_bytes, metadata, created_at";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", dto.SurveyId);
        cmd.Parameters.AddWithValue("name", dto.Name);
        cmd.Parameters.AddWithValue("modelType", dto.ModelType);
        cmd.Parameters.AddWithValue("modelPath", dto.ModelPath);
        cmd.Parameters.AddWithValue("storageUrl", (object?)dto.StorageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bounds",
            dto.Bounds != null ? JsonSerializer.Serialize(dto.Bounds) : DBNull.Value);
        cmd.Parameters.AddWithValue("metadata",
            dto.Metadata != null ? JsonSerializer.Serialize(dto.Metadata) : DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return Map3DModel(reader);
    }

    public async Task<Drone3DModel?> Get3DModelAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, survey_id, name, model_type, model_path, storage_url,
                   ST_AsGeoJSON(bounds) as bounds, vertex_count, texture_count,
                   file_size_bytes, metadata, created_at
            FROM drone_3d_models
            WHERE id = @modelId";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("modelId", modelId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return Map3DModel(reader);
    }

    public async Task<IEnumerable<Drone3DModel>> List3DModelsAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, survey_id, name, model_type, model_path, storage_url,
                   ST_AsGeoJSON(bounds) as bounds, vertex_count, texture_count,
                   file_size_bytes, metadata, created_at
            FROM drone_3d_models
            WHERE survey_id = @surveyId
            ORDER BY created_at DESC";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", surveyId);

        var models = new List<Drone3DModel>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            models.Add(Map3DModel(reader));
        }

        return models;
    }

    #endregion

    #region Helper Methods

    private DroneSurvey MapDroneSurvey(NpgsqlDataReader reader)
    {
        return new DroneSurvey
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            SurveyDate = reader.GetDateTime(3),
            FlightAltitudeM = reader.IsDBNull(4) ? null : reader.GetDouble(4),
            GroundResolutionCm = reader.IsDBNull(5) ? null : reader.GetDouble(5),
            AreaSqm = reader.IsDBNull(6) ? null : reader.GetDouble(6),
            PointCount = reader.GetInt64(7),
            CoverageArea = reader.IsDBNull(8) ? null : JsonSerializer.Deserialize<object>(reader.GetString(8)),
            OrthophotoUrl = reader.IsDBNull(9) ? null : reader.GetString(9),
            DemUrl = reader.IsDBNull(10) ? null : reader.GetString(10),
            Metadata = reader.IsDBNull(11) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(11)),
            CreatedAt = reader.GetDateTime(12),
            UpdatedAt = reader.GetDateTime(13)
        };
    }

    private DroneOrthomosaic MapOrthomosaic(NpgsqlDataReader reader)
    {
        return new DroneOrthomosaic
        {
            Id = reader.GetGuid(0),
            SurveyId = reader.GetGuid(1),
            Name = reader.GetString(2),
            RasterPath = reader.GetString(3),
            StorageUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
            Bounds = reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<object>(reader.GetString(5)),
            ResolutionCm = reader.GetDouble(6),
            TileMatrixSet = reader.GetString(7),
            Format = reader.GetString(8),
            Metadata = reader.IsDBNull(9) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(9)),
            CreatedAt = reader.GetDateTime(10)
        };
    }

    private Drone3DModel Map3DModel(NpgsqlDataReader reader)
    {
        return new Drone3DModel
        {
            Id = reader.GetGuid(0),
            SurveyId = reader.GetGuid(1),
            Name = reader.GetString(2),
            ModelType = reader.GetString(3),
            ModelPath = reader.GetString(4),
            StorageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
            Bounds = reader.IsDBNull(6) ? null : JsonSerializer.Deserialize<object>(reader.GetString(6)),
            VertexCount = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            TextureCount = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            FileSizeBytes = reader.IsDBNull(9) ? null : reader.GetInt64(9),
            Metadata = reader.IsDBNull(10) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(10)),
            CreatedAt = reader.GetDateTime(11)
        };
    }

    #endregion
}
