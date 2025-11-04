// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Carto;

internal sealed class CartoSqlQueryExecutor
{
    private const int DefaultMaxLimit = 5000;

    private readonly CartoDatasetResolver _datasetResolver;
    private readonly CartoSqlQueryParser _parser;
    private readonly IFeatureRepository _repository;

    public CartoSqlQueryExecutor(
        CartoDatasetResolver datasetResolver,
        CartoSqlQueryParser parser,
        IFeatureRepository repository)
    {
        _datasetResolver = Guard.NotNull(datasetResolver);
        _parser = Guard.NotNull(parser);
        _repository = Guard.NotNull(repository);
    }

    public async Task<CartoSqlExecutionResult> ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        if (!_parser.TryParse(sql, out var definition, out var parseError))
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, parseError ?? "Unable to parse SQL query.");
        }

        if (!_datasetResolver.TryResolve(definition.DatasetId, out var dataset))
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status404NotFound, $"Dataset '{definition.DatasetId}' was not found.");
        }

        if (!string.Equals(dataset.Service.ServiceType, "feature", StringComparison.OrdinalIgnoreCase))
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, $"Dataset '{definition.DatasetId}' is not a feature service.");
        }

        var (filter, filterError) = TryBuildFilter(dataset, definition);
        if (filterError is not null)
        {
            return filterError;
        }

        var (featureSortOrders, sortError) = TryBuildSortOrders(dataset, definition);
        if (sortError is not null)
        {
            return sortError;
        }

        var (result, duration) = await Honua.Server.Core.Observability.PerformanceMeasurement.MeasureWithDurationAsync(async () =>
        {
            if (definition.IsCount)
            {
                return await ExecuteCountAsync(dataset, definition, filter, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            }
            else if (definition.HasAggregates || definition.HasGrouping)
            {
                return await ExecuteAggregateAsync(dataset, definition, filter, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            }
            else if (definition.IsDistinct)
            {
                return await ExecuteDistinctAsync(dataset, definition, filter, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await ExecuteSelectAsync(dataset, definition, filter, featureSortOrders, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        // Update the response time with the actual total elapsed time
        if (result.IsSuccess && result.Response is not null)
        {
            var updatedResponse = result.Response with { Time = duration.TotalSeconds };
            return new CartoSqlExecutionResult(updatedResponse, null, StatusCodes.Status200OK);
        }

        return result;
    }

    private async Task<CartoSqlExecutionResult> ExecuteCountAsync(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition,
        QueryFilter? filter,
        TimeSpan elapsedTime,
        CancellationToken cancellationToken)
    {
        try
        {
            FeatureQuery? featureQuery = filter is null
                ? null
                : new FeatureQuery(Filter: filter);

            var count = await _repository.CountAsync(dataset.ServiceId, dataset.LayerId, featureQuery, cancellationToken).ConfigureAwait(false);

            var fieldName = string.IsNullOrWhiteSpace(definition.CountAlias) ? "count" : definition.CountAlias!;
            var fields = new Dictionary<string, CartoSqlFieldInfo>(StringComparer.OrdinalIgnoreCase)
            {
                [fieldName] = new CartoSqlFieldInfo("number", "count", false, null)
            };

            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [fieldName] = count
            };

            var response = new CartoSqlResponse
            {
                Time = elapsedTime.TotalSeconds,
                Fields = fields,
                TotalRows = 1,
                Rows = new List<IDictionary<string, object?>> { row }
            };

            return CartoSqlExecutionResult.Success(response);
        }
        catch (Exception)
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status500InternalServerError, "Failed to execute count query.", null);
        }
    }

    private async Task<CartoSqlExecutionResult> ExecuteAggregateAsync(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition,
        QueryFilter? filter,
        TimeSpan elapsedTime,
        CancellationToken cancellationToken)
    {
        try
        {
            var (aggregateMetadata, aggregateError) = BuildAggregateMetadata(dataset, definition);
            if (aggregateError is not null)
            {
                return aggregateError;
            }

            var statistics = BuildStatisticDefinitions(aggregateMetadata, dataset);
            var featureQuery = new FeatureQuery(
                Limit: null,
                Offset: null,
                PropertyNames: null,
                SortOrders: null,
                Filter: filter);

            var results = await _repository
                .QueryStatisticsAsync(
                    dataset.ServiceId,
                    dataset.LayerId,
                    statistics,
                    definition.GroupBy,
                    featureQuery,
                    cancellationToken)
                .ConfigureAwait(false);

            var rows = BuildAggregateRowsFromStatistics(results, definition);
            var orderedRows = ApplyAggregateOrdering(rows, definition).ToList();

            var totalGroups = orderedRows.Count;

            if (definition.Offset.HasValue)
            {
                orderedRows = orderedRows.Skip(definition.Offset.Value).ToList();
            }

            if (definition.Limit.HasValue)
            {
                orderedRows = orderedRows.Take(definition.Limit.Value).ToList();
            }

            var fields = BuildAggregateFieldMap(dataset, definition);

            var response = new CartoSqlResponse
            {
                Time = elapsedTime.TotalSeconds,
                Fields = fields,
                TotalRows = totalGroups,
                Rows = orderedRows
            };

            return CartoSqlExecutionResult.Success(response);
        }
        catch (Exception)
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status500InternalServerError, "Failed to execute aggregate query.", null);
        }
    }

    private async Task<CartoSqlExecutionResult> ExecuteSelectAsync(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition,
        QueryFilter? filter,
        IReadOnlyList<FeatureSortOrder>? sortOrders,
        TimeSpan elapsedTime,
        CancellationToken cancellationToken)
    {
        var featureQuery = BuildFeatureQuery(dataset, definition, filter, sortOrders);
        var rows = new List<IDictionary<string, object?>>();

        try
        {
            await foreach (var record in _repository.QueryAsync(dataset.ServiceId, dataset.LayerId, featureQuery, cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ShapeRow(record, dataset, definition));
            }
        }
        catch (Exception)
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status500InternalServerError, "Failed to execute query.", null);
        }

        long totalRows;
        try
        {
            var countQuery = featureQuery with { Limit = null, Offset = null, PropertyNames = null, SortOrders = null };
            totalRows = await _repository.CountAsync(dataset.ServiceId, dataset.LayerId, countQuery, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            totalRows = rows.Count;
        }

        var fields = BuildFieldMap(dataset, definition);

        var response = new CartoSqlResponse
        {
            Time = elapsedTime.TotalSeconds,
            Fields = fields,
            TotalRows = totalRows,
            Rows = rows
        };

        return CartoSqlExecutionResult.Success(response);
    }

    private async Task<CartoSqlExecutionResult> ExecuteDistinctAsync(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition,
        QueryFilter? filter,
        TimeSpan elapsedTime,
        CancellationToken cancellationToken)
    {
        if (definition.Projections.Count == 0)
        {
            return CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, "SELECT DISTINCT requires explicit columns.", null);
        }

        var fieldNames = definition.Projections.Select(p => p.Source).ToArray();
        var featureQuery = new FeatureQuery(
            Limit: null,
            Offset: null,
            PropertyNames: null,
            SortOrders: null,
            Filter: filter);

        var distinctValues = await _repository
            .QueryDistinctAsync(dataset.ServiceId, dataset.LayerId, fieldNames, featureQuery, cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<IDictionary<string, object?>>(distinctValues.Count);
        foreach (var result in distinctValues)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var projection in definition.Projections)
            {
                result.Values.TryGetValue(projection.Source, out var value);
                row[projection.OutputName] = NormalizeValue(value);
            }

            rows.Add(row);
        }

        var orderedRows = ApplyAggregateOrdering(rows, definition).ToList();
        var totalRows = orderedRows.Count;

        if (definition.Offset.HasValue)
        {
            orderedRows = orderedRows.Skip(definition.Offset.Value).ToList();
        }

        if (definition.Limit.HasValue)
        {
            orderedRows = orderedRows.Take(definition.Limit.Value).ToList();
        }

        var fields = BuildFieldMap(dataset, definition);

        var response = new CartoSqlResponse
        {
            Time = elapsedTime.TotalSeconds,
            Fields = fields,
            TotalRows = totalRows,
            Rows = orderedRows
        };

        return CartoSqlExecutionResult.Success(response);
    }

    private static FeatureQuery BuildFeatureQuery(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition,
        QueryFilter? filter,
        IReadOnlyList<FeatureSortOrder>? sortOrders)
    {
        var maxRecords = dataset.Layer.Query?.MaxRecordCount ?? DefaultMaxLimit;
        var requestedLimit = definition.Limit;
        int? effectiveLimit = requestedLimit.HasValue
            ? Math.Min(requestedLimit.Value, maxRecords)
            : maxRecords;

        var propertyNames = definition.SelectsAll
            ? null
            : definition.Projections.Select(p => p.Source).ToArray();

        return new FeatureQuery(
            Limit: effectiveLimit,
            Offset: definition.Offset,
            PropertyNames: propertyNames,
            SortOrders: sortOrders,
            Filter: filter);
    }

    private static IReadOnlyDictionary<string, CartoSqlFieldInfo> BuildFieldMap(CartoDatasetContext dataset, CartoSqlQueryDefinition definition)
    {
        var fields = new Dictionary<string, CartoSqlFieldInfo>(StringComparer.OrdinalIgnoreCase);
        var lookup = CartoFieldMapper.BuildFieldLookup(dataset.Layer);

        if (definition.SelectsAll)
        {
            foreach (var field in dataset.Layer.Fields)
            {
                if (field is null)
                {
                    continue;
                }

                fields[field.Name] = CartoFieldMapper.ToSqlField(dataset.Layer, field);
            }

            return new ReadOnlyDictionary<string, CartoSqlFieldInfo>(fields);
        }

        foreach (var projection in definition.Projections)
        {
            if (lookup.TryGetValue(projection.Source, out var field))
            {
                fields[projection.OutputName] = CartoFieldMapper.ToSqlField(dataset.Layer, field);
            }
            else
            {
                fields[projection.OutputName] = new CartoSqlFieldInfo("string", null, true, null);
            }
        }

        return new ReadOnlyDictionary<string, CartoSqlFieldInfo>(fields);
    }

    private static IDictionary<string, object?> ShapeRow(
        FeatureRecord record,
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (definition.SelectsAll)
        {
            foreach (var pair in record.Attributes)
            {
                row[pair.Key] = NormalizeValue(pair.Value);
            }

            return row;
        }

        foreach (var projection in definition.Projections)
        {
            record.Attributes.TryGetValue(projection.Source, out var value);
            row[projection.OutputName] = NormalizeValue(value);
        }

        return row;
    }

    private static object? NormalizeValue(object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                return null;
            case byte[] bytes:
                return Convert.ToBase64String(bytes);
            case JsonNode node:
                return node;
            case JsonElement element:
                return element.Clone();
            case JsonDocument document:
                return JsonSerializer.Deserialize<object>(document.RootElement.GetRawText());
            default:
                return value;
        }
    }

    private static (QueryFilter? Filter, CartoSqlExecutionResult? Error) TryBuildFilter(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.WhereClause))
        {
            return (null, null);
        }

        try
        {
            var filter = CqlFilterParser.Parse(definition.WhereClause!, dataset.Layer);
            return (filter, null);
        }
        catch (Exception ex)
        {
            return (null, CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, "Invalid WHERE clause.", ex.Message));
        }
    }

    private static (IReadOnlyList<FeatureSortOrder>? SortOrders, CartoSqlExecutionResult? Error) TryBuildSortOrders(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition)
    {
        if (definition.SortOrders.Count == 0)
        {
            return (null, null);
        }

        var lookup = CartoFieldMapper.BuildFieldLookup(dataset.Layer);
        var projectionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var projection in definition.Projections)
        {
            projectionMap[projection.OutputName] = projection.Source;
        }

        var aggregateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregate in definition.Aggregates)
        {
            aggregateNames.Add(aggregate.OutputName);
        }

        var orders = new List<FeatureSortOrder>(definition.SortOrders.Count);

        foreach (var sortDefinition in definition.SortOrders)
        {
            if (string.Equals(sortDefinition.Field, dataset.Layer.IdField, StringComparison.OrdinalIgnoreCase))
            {
                orders.Add(new FeatureSortOrder(dataset.Layer.IdField, sortDefinition.Descending ? FeatureSortDirection.Descending : FeatureSortDirection.Ascending));
                continue;
            }

            if (lookup.TryGetValue(sortDefinition.Field, out var directField))
            {
                orders.Add(new FeatureSortOrder(directField.Name, sortDefinition.Descending ? FeatureSortDirection.Descending : FeatureSortDirection.Ascending));
                continue;
            }

            if (projectionMap.TryGetValue(sortDefinition.Field, out var mappedField) && lookup.TryGetValue(mappedField, out var mappedDefinition))
            {
                orders.Add(new FeatureSortOrder(mappedDefinition.Name, sortDefinition.Descending ? FeatureSortDirection.Descending : FeatureSortDirection.Ascending));
                continue;
            }

            if (aggregateNames.Contains(sortDefinition.Field))
            {
                // Aggregated ordering is applied after aggregation; skip repository-level ordering.
                continue;
            }

            return (null, CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, $"ORDER BY field '{sortDefinition.Field}' is not defined for dataset '{dataset.DatasetId}'.", null));
        }

        return (orders.Count == 0 ? null : orders, null);
    }

    private static (List<AggregateMetadata> Metadata, CartoSqlExecutionResult? Error) BuildAggregateMetadata(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition)
    {
        var metadata = new List<AggregateMetadata>(definition.Aggregates.Count);
        var lookup = CartoFieldMapper.BuildFieldLookup(dataset.Layer);

        foreach (var aggregate in definition.Aggregates)
        {
            FieldDefinition? field = null;
            if (!string.IsNullOrWhiteSpace(aggregate.TargetField))
            {
                if (!lookup.TryGetValue(aggregate.TargetField!, out field))
                {
                    return (metadata, CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, $"Aggregate field '{aggregate.TargetField}' is not defined for dataset '{dataset.DatasetId}'.", null));
                }
            }

            switch (aggregate.Function)
            {
                case CartoSqlAggregateFunction.Count:
                    break;
                case CartoSqlAggregateFunction.Sum:
                case CartoSqlAggregateFunction.Avg:
                case CartoSqlAggregateFunction.Min:
                case CartoSqlAggregateFunction.Max:
                    if (field is null)
                    {
                        return (metadata, CartoSqlExecutionResult.Failure(StatusCodes.Status400BadRequest, $"Aggregate '{aggregate.OutputName}' requires a column reference.", null));
                    }
                    break;
            }

            metadata.Add(new AggregateMetadata(aggregate, field));
        }

        return (metadata, null);
    }

    private static IReadOnlyList<StatisticDefinition> BuildStatisticDefinitions(
        IReadOnlyList<AggregateMetadata> metadata,
        CartoDatasetContext dataset)
    {
        var statistics = new List<StatisticDefinition>(metadata.Count);

        foreach (var aggregate in metadata)
        {
            var fieldName = aggregate.Field?.Name ?? dataset.Layer.IdField;
            var statisticType = aggregate.Definition.Function switch
            {
                CartoSqlAggregateFunction.Count => StatisticType.Count,
                CartoSqlAggregateFunction.Sum => StatisticType.Sum,
                CartoSqlAggregateFunction.Avg => StatisticType.Avg,
                CartoSqlAggregateFunction.Min => StatisticType.Min,
                CartoSqlAggregateFunction.Max => StatisticType.Max,
                _ => throw new NotSupportedException($"Unsupported aggregate function '{aggregate.Definition.Function}'.")
            };

            statistics.Add(new StatisticDefinition(fieldName, statisticType, aggregate.Definition.OutputName));
        }

        return statistics;
    }

    private static List<IDictionary<string, object?>> BuildAggregateRowsFromStatistics(
        IReadOnlyList<StatisticsResult> results,
        CartoSqlQueryDefinition definition)
    {
        var rows = new List<IDictionary<string, object?>>(results.Count);
        var groupIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < definition.GroupBy.Count; i++)
        {
            groupIndex[definition.GroupBy[i]] = i;
        }

        foreach (var result in results)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (definition.Projections.Count == 0)
            {
                foreach (var groupField in definition.GroupBy)
                {
                    result.GroupValues.TryGetValue(groupField, out var value);
                    row[groupField] = NormalizeValue(value);
                }
            }
            else
            {
                foreach (var projection in definition.Projections)
                {
                    if (groupIndex.ContainsKey(projection.Source) &&
                        result.GroupValues.TryGetValue(projection.Source, out var value))
                    {
                        row[projection.OutputName] = NormalizeValue(value);
                    }
                }
            }

            foreach (var aggregate in definition.Aggregates)
            {
                result.Statistics.TryGetValue(aggregate.OutputName, out var value);
                row[aggregate.OutputName] = NormalizeValue(value);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static IEnumerable<IDictionary<string, object?>> ApplyAggregateOrdering(
        IEnumerable<IDictionary<string, object?>> rows,
        CartoSqlQueryDefinition definition)
    {
        IOrderedEnumerable<IDictionary<string, object?>>? ordered = null;
        foreach (var sort in definition.SortOrders)
        {
            Func<IDictionary<string, object?>, object?> keySelector = row => row.TryGetValue(sort.Field, out var value) ? value : null;
            ordered = ordered is null
                ? (sort.Descending
                    ? rows.OrderByDescending(keySelector, AggregateValueComparer.Instance)
                    : rows.OrderBy(keySelector, AggregateValueComparer.Instance))
                : (sort.Descending
                    ? ordered.ThenByDescending(keySelector, AggregateValueComparer.Instance)
                    : ordered.ThenBy(keySelector, AggregateValueComparer.Instance));
        }

        return ordered ?? rows;
    }

    private static IReadOnlyDictionary<string, CartoSqlFieldInfo> BuildAggregateFieldMap(
        CartoDatasetContext dataset,
        CartoSqlQueryDefinition definition)
    {
        var fields = new Dictionary<string, CartoSqlFieldInfo>(StringComparer.OrdinalIgnoreCase);
        var lookup = CartoFieldMapper.BuildFieldLookup(dataset.Layer);

        foreach (var projection in definition.Projections)
        {
            if (lookup.TryGetValue(projection.Source, out var field))
            {
                fields[projection.OutputName] = CartoFieldMapper.ToSqlField(dataset.Layer, field);
            }
            else
            {
                fields[projection.OutputName] = new CartoSqlFieldInfo("string", null, true, null);
            }
        }

        foreach (var aggregate in definition.Aggregates)
        {
            switch (aggregate.Function)
            {
                case CartoSqlAggregateFunction.Count:
                    fields[aggregate.OutputName] = new CartoSqlFieldInfo("number", "count", false, null);
                    break;
                case CartoSqlAggregateFunction.Sum:
                    fields[aggregate.OutputName] = new CartoSqlFieldInfo("number", "sum", false, null);
                    break;
                case CartoSqlAggregateFunction.Avg:
                    fields[aggregate.OutputName] = new CartoSqlFieldInfo("number", "avg", false, null);
                    break;
                case CartoSqlAggregateFunction.Min:
                case CartoSqlAggregateFunction.Max:
                    if (!string.IsNullOrWhiteSpace(aggregate.TargetField) && lookup.TryGetValue(aggregate.TargetField!, out var field))
                    {
                        fields[aggregate.OutputName] = CartoFieldMapper.ToSqlField(dataset.Layer, field);
                    }
                    else
                    {
                        fields[aggregate.OutputName] = new CartoSqlFieldInfo("string", null, true, null);
                    }
                    break;
            }
        }

        if (definition.Projections.Count == 0)
        {
            foreach (var groupColumn in definition.GroupBy)
            {
                if (fields.ContainsKey(groupColumn))
                {
                    continue;
                }

                if (lookup.TryGetValue(groupColumn, out var field))
                {
                    fields[groupColumn] = CartoFieldMapper.ToSqlField(dataset.Layer, field);
                }
                else
                {
                    fields[groupColumn] = new CartoSqlFieldInfo("string", null, true, null);
                }
            }
        }

        return new ReadOnlyDictionary<string, CartoSqlFieldInfo>(fields);
    }

    private sealed record AggregateMetadata(CartoSqlAggregateDefinition Definition, FieldDefinition? Field);

    private sealed class AggregateValueComparer : IComparer<object?>
    {
        public static AggregateValueComparer Instance { get; } = new();

        public static bool AreEqual(object? left, object? right)
        {
            return Instance.Compare(left, right) == 0;
        }

        public static int ComputeHashCode(object? value)
        {
            if (value is null)
            {
                return 0;
            }

            return value switch
            {
                string s => StringComparer.OrdinalIgnoreCase.GetHashCode(s),
                bool b => b.GetHashCode(),
                int i => i.GetHashCode(),
                long l => l.GetHashCode(),
                double d => d.GetHashCode(),
                decimal m => m.GetHashCode(),
                float f => f.GetHashCode(),
                _ => StringComparer.OrdinalIgnoreCase.GetHashCode(value.ToString() ?? string.Empty)
            };
        }

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (IsNumeric(x) && IsNumeric(y))
            {
                var left = Convert.ToDecimal(x, CultureInfo.InvariantCulture);
                var right = Convert.ToDecimal(y, CultureInfo.InvariantCulture);
                return left.CompareTo(right);
            }

            if (x is IComparable comparable && x.GetType().IsInstanceOfType(y))
            {
                return comparable.CompareTo(y);
            }

            return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumeric(object value)
        {
            return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
        }
    }
}
