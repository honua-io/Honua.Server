// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.Azure.Cosmos;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
using QueryDefinition = Microsoft.Azure.Cosmos.QueryDefinition;
namespace Honua.Server.Enterprise.Data.CosmosDb;

internal sealed class CosmosDbFeatureQueryBuilder
{
    private readonly LayerDefinition _layer;
    private readonly string[][] _partitionKeyPaths;
    private readonly List<KeyValuePair<string, object?>> _parameters = new();
    private int _parameterOrdinal;

    private const int DefaultLimit = 1000;

    public CosmosDbFeatureQueryBuilder(LayerDefinition layer, string[][]? partitionKeyPaths = null)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _partitionKeyPaths = partitionKeyPaths ?? Array.Empty<string[]>();
    }

    public QueryDefinition BuildSelect(FeatureQuery query)
    {
        Guard.NotNull(query);
        Reset();

        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(BuildProjection(query.PropertyNames)).Append(" FROM c");

        AppendWhereClause(sql, query);

        var requiresOrderBy = (query.Offset ?? 0) > 0;
        AppendOrderBy(sql, query, requiresOrderBy);
        AppendPagination(sql, query);

        return ToQueryDefinition(sql);
    }

    public QueryDefinition BuildCount(FeatureQuery query)
    {
        Guard.NotNull(query);
        Reset();

        var sql = new StringBuilder("SELECT VALUE COUNT(1) FROM c");
        AppendWhereClause(sql, query);
        return ToQueryDefinition(sql);
    }

    public QueryDefinition BuildById(string featureId)
    {
        Guard.NotNullOrWhiteSpace(featureId);
        Reset();

        // Use LayerMetadataHelper to get primary key column
        var keyField = LayerMetadataHelper.GetPrimaryKeyColumn(_layer) ?? "id";

        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(BuildProjection(null)).Append(" FROM c WHERE ");
        sql.Append($"{FormatProperty(keyField)} = {AddParameter(featureId)}");

        return ToQueryDefinition(sql);
    }

    private void Reset()
    {
        _parameters.Clear();
        _parameterOrdinal = 0;
    }

    private string BuildProjection(IReadOnlyList<string>? propertyNames)
    {
        var projectionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectionList = new List<string>();

        void Add(string property)
        {
            if (property.IsNullOrWhiteSpace())
            {
                return;
            }

            if (projectionSet.Add(property))
            {
                projectionList.Add(property);
            }
        }

        Add("id");

        // Use LayerMetadataHelper to get geometry column
        var geometryField = LayerMetadataHelper.GetGeometryColumn(_layer);
        if (geometryField.HasValue())
        {
            Add(geometryField);
        }

        foreach (var path in _partitionKeyPaths)
        {
            if (path.Length > 0)
            {
                Add(path[0]);
            }
        }

        if (_layer.Fields is { Count: > 0 })
        {
            foreach (var field in _layer.Fields)
            {
                Add(field.Name);
            }
        }

        if (propertyNames is { Count: > 0 })
        {
            foreach (var property in propertyNames)
            {
                Add(property);
            }
        }

        if (projectionList.Count == 0)
        {
            return "c";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < projectionList.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(FormatProperty(projectionList[i]));
        }

        return builder.ToString();
    }

    private void AppendWhereClause(StringBuilder sql, FeatureQuery query)
    {
        var conditions = new List<string>();

        if (query.Filter?.Expression is not null)
        {
            var filterSql = CosmosDbFilterTranslator.TryTranslate(
                query.Filter,
                property => FormatProperty(property),
                AddParameter);

            if (filterSql.HasValue())
            {
                conditions.Add(filterSql);
            }
        }

        if (query.Bbox is not null)
        {
            // Use LayerMetadataHelper to get geometry column
            var geometryField = LayerMetadataHelper.GetGeometryColumn(_layer);
            if (geometryField.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException($"Layer '{_layer.Id}' does not define a geometry field.");
            }

            var polygon = CreatePolygonLiteral(query.Bbox);
            conditions.Add($"ST_WITHIN({FormatProperty(geometryField)}, {polygon})");
        }

        var temporalColumn = _layer.Storage?.TemporalColumn;
        if (query.Temporal is not null && temporalColumn.HasValue())
        {
            if (query.Temporal.Start.HasValue)
            {
                var startParam = AddParameter(query.Temporal.Start.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
                conditions.Add($"{FormatProperty(temporalColumn)} >= {startParam}");
            }

            if (query.Temporal.End.HasValue)
            {
                var endParam = AddParameter(query.Temporal.End.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
                conditions.Add($"{FormatProperty(temporalColumn)} <= {endParam}");
            }
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }
    }

    private void AppendOrderBy(StringBuilder sql, FeatureQuery query, bool requiresOrderBy)
    {
        if (query.SortOrders is { Count: > 0 })
        {
            var clauses = query.SortOrders
                .Select(sort => $"{FormatProperty(sort.Field)} {(sort.Direction == FeatureSortDirection.Descending ? "DESC" : "ASC")}");

            sql.Append(" ORDER BY ").Append(string.Join(", ", clauses));
            return;
        }

        if (requiresOrderBy)
        {
            // Use LayerMetadataHelper to get primary key column
            var idColumn = LayerMetadataHelper.GetPrimaryKeyColumn(_layer) ?? "id";
            sql.Append(" ORDER BY ").Append(FormatProperty(idColumn));
        }
    }

    private void AppendPagination(StringBuilder sql, FeatureQuery query)
    {
        var hasOffset = query.Offset.HasValue && query.Offset.Value > 0;
        var hasLimit = query.Limit.HasValue && query.Limit.Value > 0;

        if (!hasOffset && !hasLimit)
        {
            return;
        }

        var offsetValue = hasOffset ? Math.Max(0, query.Offset!.Value) : 0;
        var limitValue = hasLimit ? Math.Max(1, query.Limit!.Value) : DefaultLimit;

        var offsetParameter = AddParameter(offsetValue);
        var limitParameter = AddParameter(limitValue);

        sql.Append(" OFFSET ").Append(offsetParameter).Append(" LIMIT ").Append(limitParameter);
    }

    private string AddParameter(object? value)
    {
        var name = $"@p{_parameterOrdinal++}";
        _parameters.Add(new KeyValuePair<string, object?>(name, value));
        return name;
    }

    private QueryDefinition ToQueryDefinition(StringBuilder sql)
    {
        var queryDefinition = new QueryDefinition(sql.ToString());
        foreach (var (name, value) in _parameters)
        {
            queryDefinition = queryDefinition.WithParameter(name, value);
        }

        return queryDefinition;
    }

    private static string FormatProperty(string property)
    {
        if (property.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Property name cannot be null or empty.");
        }

        var segments = property.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException($"Invalid property path '{property}'.");
        }

        var builder = new StringBuilder("c");
        foreach (var segment in segments)
        {
            if (NeedsBracket(segment))
            {
                builder.Append("[\"").Append(segment.Replace("\"", "\\\"", StringComparison.Ordinal)).Append("\"]");
            }
            else
            {
                builder.Append('.').Append(segment);
            }
        }

        return builder.ToString();
    }

    private static bool NeedsBracket(string segment)
    {
        foreach (var ch in segment)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                return true;
            }
        }

        return false;
    }

    public QueryDefinition BuildDistinctQuery(
        FeatureQuery query,
        IReadOnlyList<string> fieldNames,
        out IReadOnlyDictionary<string, string> aliasMap)
    {
        Guard.NotNull(query);
        Guard.NotNull(fieldNames);
        if (fieldNames.Count == 0)
        {
            throw new InvalidOperationException("Distinct queries require at least one field.");
        }

        Reset();

        var aliasDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sql = new StringBuilder("SELECT DISTINCT ");

        for (var i = 0; i < fieldNames.Count; i++)
        {
            var field = fieldNames[i];
            if (field.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Distinct field names cannot be null or whitespace.");
            }

            if (i > 0)
            {
                sql.Append(", ");
            }

            var alias = CreateAlias("d", i);
            sql.Append(FormatProperty(field)).Append(" AS ").Append(alias);
            aliasDictionary[alias] = field;
        }

        sql.Append(" FROM c");
        AppendWhereClause(sql, query);

        aliasMap = new ReadOnlyDictionary<string, string>(aliasDictionary);
        return ToQueryDefinition(sql);
    }

    public QueryDefinition BuildStatisticsQuery(
        FeatureQuery query,
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<StatisticDefinition> statistics,
        out IReadOnlyDictionary<string, string> groupAliasMap,
        out IReadOnlyDictionary<string, StatisticDefinition> statisticAliasMap)
    {
        Guard.NotNull(query);
        Guard.NotNull(statistics);

        if (statistics.Count == 0)
        {
            throw new InvalidOperationException("Statistics queries require at least one statistic definition.");
        }

        Reset();

        groupByFields ??= Array.Empty<string>();

        var selectParts = new List<string>();
        var groupMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var statMap = new Dictionary<string, StatisticDefinition>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < groupByFields.Count; i++)
        {
            var field = groupByFields[i];
            if (field.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Group-by field names cannot be null or whitespace.");
            }

            var alias = CreateAlias("g", i);
            selectParts.Add($"{FormatProperty(field)} AS {alias}");
            groupMap[alias] = field;
        }

        for (var i = 0; i < statistics.Count; i++)
        {
            var statistic = statistics[i];
            var alias = CreateAlias("s", i);
            selectParts.Add($"{BuildStatisticExpression(statistic)} AS {alias}");
            statMap[alias] = statistic;
        }

        var sql = new StringBuilder();
        sql.Append("SELECT ");
        sql.Append(string.Join(", ", selectParts));
        sql.Append(" FROM c");

        AppendWhereClause(sql, query);

        if (groupByFields.Count > 0)
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", groupByFields.Select(FormatProperty)));
        }

        groupAliasMap = new ReadOnlyDictionary<string, string>(groupMap);
        statisticAliasMap = new ReadOnlyDictionary<string, StatisticDefinition>(statMap);
        return ToQueryDefinition(sql);
    }

    private static string CreateAlias(string prefix, int index)
        => $"{prefix}_{index}";

    private static string CreatePolygonLiteral(BoundingBox bbox)
    {
        return FormattableString.Invariant($"{{\"type\":\"Polygon\",\"coordinates\":[[[{bbox.MinX},{bbox.MinY}],[{bbox.MinX},{bbox.MaxY}],[{bbox.MaxX},{bbox.MaxY}],[{bbox.MaxX},{bbox.MinY}],[{bbox.MinX},{bbox.MinY}]]]}}");
    }

    private string BuildStatisticExpression(StatisticDefinition statistic)
    {
        return statistic.Type switch
        {
            StatisticType.Count when statistic.FieldName.IsNullOrWhiteSpace() => "COUNT(1)",
            StatisticType.Count => $"COUNT({FormatProperty(statistic.FieldName!)})",
            StatisticType.Sum => $"SUM({FormatProperty(RequireField(statistic))})",
            StatisticType.Avg => $"AVG({FormatProperty(RequireField(statistic))})",
            StatisticType.Min => $"MIN({FormatProperty(RequireField(statistic))})",
            StatisticType.Max => $"MAX({FormatProperty(RequireField(statistic))})",
            _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported for Cosmos DB.")
        };
    }

    private static string RequireField(StatisticDefinition statistic)
    {
        if (statistic.FieldName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Statistic '{statistic.Type}' requires a field name.");
        }

        return statistic.FieldName;
    }
}
