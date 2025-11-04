// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Snowflake;

/// <summary>
/// Handles single-record CRUD operations for Snowflake features.
/// </summary>
internal sealed class SnowflakeFeatureOperations
{
    private readonly SnowflakeConnectionManager _connectionManager;
    private static readonly IDataStoreCapabilities Capabilities = SnowflakeDataStoreCapabilities.Instance;

    public SnowflakeFeatureOperations(SnowflakeConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildSelect(query ?? new FeatureQuery());

        await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return SnowflakeRecordMapper.CreateFeatureRecord(reader, layer);
        }
    }

    public async Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildCount(query ?? new FeatureQuery());

        await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return result is null or DBNull ? 0L : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildById(featureId);

        await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return SnowflakeRecordMapper.CreateFeatureRecord(reader, layer);
        }

        return null;
    }

    public async Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildInsert(record);

        await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Snowflake doesn't support RETURNING, so fetch the inserted record
        var idValue = record.Attributes.TryGetValue(layer.IdField, out var id) ? id : null;
        if (idValue != null)
        {
            var inserted = await GetAsync(dataSource, service, layer, idValue.ToString()!, null, cancellationToken).ConfigureAwait(false);
            return inserted ?? record;
        }

        return record;
    }

    public async Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureId);
        Guard.NotNull(record);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildUpdate(featureId, record);

        await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            return null;
        }

        return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureId);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildDelete(featureId);

        await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return rowsAffected > 0;
    }

    public async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(statistics);

        if (statistics.Count == 0)
        {
            return Array.Empty<StatisticsResult>();
        }

        Task<IReadOnlyList<StatisticsResult>> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeStatisticsAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                statistics,
                groupByFields,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var builder = new SnowflakeFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildStatistics(query, statistics, groupByFields);

            await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var groupFieldSet = groupByFields is { Count: > 0 }
                ? new HashSet<string>(groupByFields, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var statisticAliases = new HashSet<string>(
                statistics.Select(s => s.OutputName ?? $"{s.Type}_{s.FieldName}"),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<StatisticsResult>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < reader.FieldCount; index++)
                {
                    var columnName = reader.GetName(index);
                    var value = reader.IsDBNull(index) ? null : reader.GetValue(index);

                    if (groupFieldSet.Contains(columnName))
                    {
                        groupValues[columnName] = value;
                    }
                    else if (statisticAliases.Contains(columnName))
                    {
                        statisticValues[columnName] = value;
                    }
                    else if (groupFieldSet.Count > 0)
                    {
                        groupValues[columnName] = value;
                    }
                    else
                    {
                        statisticValues[columnName] = value;
                    }
                }

                results.Add(new StatisticsResult(
                    new ReadOnlyDictionary<string, object?>(groupValues),
                    new ReadOnlyDictionary<string, object?>(statisticValues)));
            }

            return results;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(fieldNames);

        if (fieldNames.Count == 0)
        {
            return Array.Empty<DistinctResult>();
        }

        Task<IReadOnlyList<DistinctResult>> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeDistinctAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                fieldNames,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var builder = new SnowflakeFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildDistinct(query, fieldNames);

            await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var results = new List<DistinctResult>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < reader.FieldCount; index++)
                {
                    var columnName = reader.GetName(index);
                    values[columnName] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                }

                results.Add(new DistinctResult(new ReadOnlyDictionary<string, object?>(values)));
            }

            return results;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    public async Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        Task<BoundingBox?> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeExtentAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                service,
                layer,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var builder = new SnowflakeFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildExtent(query);

            await using var command = SnowflakeRecordMapper.CreateCommand(connection, sql, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var minX = ReadNullableDouble(reader, 0);
            var minY = ReadNullableDouble(reader, 1);
            var maxX = ReadNullableDouble(reader, 2);
            var maxY = ReadNullableDouble(reader, 3);

            if (!minX.HasValue || !minY.HasValue || !maxX.HasValue || !maxY.HasValue)
            {
                return null;
            }

            var targetCrs = query.Crs ?? service.Ogc.DefaultCrs ?? "EPSG:4326";
            return new BoundingBox(minX.Value, minY.Value, maxX.Value, maxY.Value, Crs: targetCrs);
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    private static bool IsSpatialCapabilityError(Exception exception)
    {
        if (exception is null)
        {
            return false;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (ContainsSpatialCapabilityIndicator(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpatialCapabilityIndicator(string? message)
    {
        if (message.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (!message.Contains("ST_", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("GEOGRAPHY", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var triggers = new[]
        {
            "sql compilation error",
            "sql access control error",
            "function does not exist",
            "not authorized",
            "insufficient privileges",
            "must be enabled",
            "feature is not enabled",
            "unsupported feature",
            "disabled",
            "not supported"
        };

        return triggers.Any(trigger => message.Contains(trigger, StringComparison.OrdinalIgnoreCase));
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);

        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' has no connection string configured.");
        }

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5; // 5 second timeout for health checks

        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static FeatureQuery NormalizeAnalyticsQuery(FeatureQuery? query)
    {
        var baseQuery = query ?? new FeatureQuery();
        return baseQuery with
        {
            Limit = null,
            Offset = null,
            PropertyNames = null,
            SortOrders = null,
            ResultType = FeatureResultType.Results
        };
    }

    private static double? ReadNullableDouble(IDataRecord reader, int ordinal)
    {
        if (ordinal < 0 || ordinal >= reader.FieldCount)
        {
            return null;
        }

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }
}
