// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Data;
using Dapper;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;

/// <summary>
/// PostgreSQL implementation of anomaly detection repository
/// </summary>
public sealed class PostgresAnomalyDetectionRepository : IAnomalyDetectionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresAnomalyDetectionRepository> _logger;

    public PostgresAnomalyDetectionRepository(
        string connectionString,
        ILogger<PostgresAnomalyDetectionRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<StaleDatastreamInfo>> GetStaleDatastreamsAsync(
        TimeSpan threshold,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                d.id as DatastreamId,
                d.name as DatastreamName,
                t.id as ThingId,
                t.name as ThingName,
                s.id as SensorId,
                s.name as SensorName,
                op.name as ObservedPropertyName,
                MAX(o.phenomenon_time) as LastObservationTime,
                EXTRACT(EPOCH FROM (NOW() - MAX(o.phenomenon_time)))::bigint as SecondsSinceLastObservation,
                t.organization_id as TenantId
            FROM sta_datastreams d
            INNER JOIN sta_things t ON d.thing_id = t.id
            INNER JOIN sta_sensors s ON d.sensor_id = s.id
            INNER JOIN sta_observed_properties op ON d.observed_property_id = op.id
            LEFT JOIN sta_observations o ON d.id = o.datastream_id
            WHERE ($1::VARCHAR IS NULL OR t.organization_id = $1)
            GROUP BY d.id, d.name, t.id, t.name, s.id, s.name, op.name, t.organization_id
            HAVING MAX(o.phenomenon_time) IS NULL
                OR MAX(o.phenomenon_time) < NOW() - $2::interval
            ORDER BY SecondsSinceLastObservation DESC NULLS FIRST";

        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { tenantId, threshold = threshold.ToString() });

        return results.Select(r => new StaleDatastreamInfo
        {
            DatastreamId = r.datastreamid.ToString(),
            DatastreamName = r.datastreamname,
            ThingId = r.thingid.ToString(),
            ThingName = r.thingname,
            SensorId = r.sensorid.ToString(),
            SensorName = r.sensorname,
            ObservedPropertyName = r.observedpropertyname,
            LastObservationTime = r.lastobservationtime,
            TimeSinceLastObservation = r.secondssincelastobservation != null
                ? TimeSpan.FromSeconds((long)r.secondssincelastobservation)
                : TimeSpan.MaxValue,
            TenantId = r.tenantid
        }).ToList();
    }

    public async Task<DatastreamStatistics?> GetDatastreamStatisticsAsync(
        string datastreamId,
        TimeSpan window,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            WITH numeric_observations AS (
                SELECT
                    (result::text)::numeric as numeric_result
                FROM sta_observations
                WHERE datastream_id = $1::uuid
                    AND phenomenon_time >= NOW() - $2::interval
                    AND jsonb_typeof(to_jsonb(result)) = 'number'
            )
            SELECT
                COUNT(*) as observation_count,
                AVG(numeric_result) as mean,
                STDDEV_POP(numeric_result) as std_dev,
                MIN(numeric_result) as min,
                MAX(numeric_result) as max
            FROM numeric_observations
            HAVING COUNT(*) > 0";

        var result = await conn.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { datastreamId = Guid.Parse(datastreamId), window = window.ToString() });

        if (result == null || result.observation_count == 0)
        {
            return null;
        }

        // Get datastream details
        var datastreamSql = @"
            SELECT
                d.id,
                d.name,
                op.name as observed_property_name
            FROM sta_datastreams d
            INNER JOIN sta_observed_properties op ON d.observed_property_id = op.id
            WHERE d.id = $1::uuid";

        var dsInfo = await conn.QuerySingleOrDefaultAsync<dynamic>(
            datastreamSql,
            new { datastreamId = Guid.Parse(datastreamId) });

        if (dsInfo == null)
        {
            return null;
        }

        return new DatastreamStatistics
        {
            DatastreamId = datastreamId,
            DatastreamName = dsInfo.name,
            ObservedPropertyName = dsInfo.observed_property_name,
            ObservationCount = (int)result.observation_count,
            Mean = result.mean != null ? Convert.ToDouble(result.mean) : 0,
            StandardDeviation = result.std_dev != null ? Convert.ToDouble(result.std_dev) : 0,
            Min = result.min != null ? Convert.ToDouble(result.min) : 0,
            Max = result.max != null ? Convert.ToDouble(result.max) : 0,
            Window = window
        };
    }

    public async Task<IReadOnlyList<ObservationSummary>> GetRecentObservationsAsync(
        string datastreamId,
        DateTime since,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                id,
                phenomenon_time as PhenomenonTime,
                result as Result,
                CASE
                    WHEN jsonb_typeof(to_jsonb(result)) = 'number'
                    THEN (result::text)::numeric
                    ELSE NULL
                END as NumericResult,
                datastream_id as DatastreamId
            FROM sta_observations
            WHERE datastream_id = $1::uuid
                AND phenomenon_time >= $2
            ORDER BY phenomenon_time DESC
            LIMIT $3";

        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { datastreamId = Guid.Parse(datastreamId), since, limit });

        return results.Select(r => new ObservationSummary
        {
            Id = r.id.ToString(),
            PhenomenonTime = r.phenomenontime,
            Result = r.result,
            NumericResult = r.numericresult != null ? Convert.ToDouble(r.numericresult) : null,
            DatastreamId = r.datastreamid.ToString()
        }).ToList();
    }

    public async Task<IReadOnlyList<DatastreamInfo>> GetActiveDatastreamsAsync(
        string? tenantId = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT
                d.id as DatastreamId,
                d.name as DatastreamName,
                t.id as ThingId,
                t.name as ThingName,
                s.id as SensorId,
                s.name as SensorName,
                op.id as ObservedPropertyId,
                op.name as ObservedPropertyName,
                MAX(o.phenomenon_time) as LastObservationTime,
                COUNT(o.id) as TotalObservations,
                t.organization_id as TenantId
            FROM sta_datastreams d
            INNER JOIN sta_things t ON d.thing_id = t.id
            INNER JOIN sta_sensors s ON d.sensor_id = s.id
            INNER JOIN sta_observed_properties op ON d.observed_property_id = op.id
            LEFT JOIN sta_observations o ON d.id = o.datastream_id
            WHERE ($1::VARCHAR IS NULL OR t.organization_id = $1)
            GROUP BY d.id, d.name, t.id, t.name, s.id, s.name, op.id, op.name, t.organization_id
            ORDER BY d.name";

        var results = await conn.QueryAsync<dynamic>(sql, new { tenantId });

        return results.Select(r => new DatastreamInfo
        {
            DatastreamId = r.datastreamid.ToString(),
            DatastreamName = r.datastreamname,
            ThingId = r.thingid.ToString(),
            ThingName = r.thingname,
            SensorId = r.sensorid.ToString(),
            SensorName = r.sensorname,
            ObservedPropertyId = r.observedpropertyid.ToString(),
            ObservedPropertyName = r.observedpropertyname,
            LastObservationTime = r.lastobservationtime,
            TotalObservations = (int)(r.totalobservations ?? 0),
            TenantId = r.tenantid
        }).ToList();
    }

    public async Task RecordAlertAsync(
        string datastreamId,
        AnomalyType anomalyType,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Create alert history table if it doesn't exist
        await EnsureAlertHistoryTableAsync(conn, ct);

        var sql = @"
            INSERT INTO sta_anomaly_alerts (datastream_id, anomaly_type, tenant_id, created_at)
            VALUES ($1::uuid, $2, $3, NOW())";

        await conn.ExecuteAsync(
            sql,
            new
            {
                datastreamId = Guid.Parse(datastreamId),
                anomalyType = anomalyType.ToString(),
                tenantId
            });
    }

    public async Task<bool> CanSendAlertAsync(
        string datastreamId,
        AnomalyType anomalyType,
        TimeSpan window,
        int maxAlerts,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Ensure table exists
        await EnsureAlertHistoryTableAsync(conn, ct);

        var sql = @"
            SELECT COUNT(*)
            FROM sta_anomaly_alerts
            WHERE datastream_id = $1::uuid
                AND anomaly_type = $2
                AND ($3::VARCHAR IS NULL OR tenant_id = $3)
                AND created_at >= NOW() - $4::interval";

        var count = await conn.ExecuteScalarAsync<int>(
            sql,
            new
            {
                datastreamId = Guid.Parse(datastreamId),
                anomalyType = anomalyType.ToString(),
                tenantId,
                window = window.ToString()
            });

        return count < maxAlerts;
    }

    public async Task<int> GetTotalAlertCountAsync(
        TimeSpan window,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Ensure table exists
        await EnsureAlertHistoryTableAsync(conn, ct);

        var sql = @"
            SELECT COUNT(*)
            FROM sta_anomaly_alerts
            WHERE ($1::VARCHAR IS NULL OR tenant_id = $1)
                AND created_at >= NOW() - $2::interval";

        return await conn.ExecuteScalarAsync<int>(
            sql,
            new { tenantId, window = window.ToString() });
    }

    private async Task EnsureAlertHistoryTableAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS sta_anomaly_alerts (
                id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                datastream_id UUID NOT NULL,
                anomaly_type VARCHAR(50) NOT NULL,
                tenant_id VARCHAR(255),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_datastream
                ON sta_anomaly_alerts(datastream_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_tenant
                ON sta_anomaly_alerts(tenant_id, created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_created
                ON sta_anomaly_alerts(created_at DESC);";

        await conn.ExecuteAsync(sql);
    }
}
