// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    private static (JsonObject? Aggregations, List<ElasticsearchStatisticMapping> Mappings, IReadOnlyList<string> GroupAggregationNames) BuildStatisticsAggregations(
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        LayerDefinition layer)
    {
        var mappings = new List<ElasticsearchStatisticMapping>(statistics.Count);
        var metrics = new JsonObject();
        var groupAggNames = groupByFields?.Select(field => SanitizeAggregationName(field, "group_")).ToList()
                            ?? new List<string>();

        for (var i = 0; i < statistics.Count; i++)
        {
            var stat = statistics[i];
            var outputName = stat.OutputName ?? $"{stat.Type}_{stat.FieldName}";

            if (stat.Type == StatisticType.Count)
            {
                mappings.Add(new ElasticsearchStatisticMapping(outputName, stat.Type, stat.FieldName, AggregationName: null, UseDocCount: true));
                continue;
            }

            if (stat.FieldName.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException($"Statistic '{outputName}' requires a field name.");
            }

            var aggregationName = SanitizeAggregationName(stat.FieldName, $"metric_{i}_");
            metrics[aggregationName] = BuildMetricAggregation(stat);
            mappings.Add(new ElasticsearchStatisticMapping(outputName, stat.Type, stat.FieldName, AggregationName: aggregationName, UseDocCount: false));
        }

        JsonObject? aggregations = null;

        if (groupAggNames.Count > 0)
        {
            aggregations = BuildGroupAggregations(groupByFields!, groupAggNames, metrics);
        }
        else if (metrics.Count > 0)
        {
            aggregations = metrics;
        }

        return (aggregations, mappings, groupAggNames);
    }

    private static (JsonObject Aggregations, IReadOnlyList<string> AggregationNames) BuildDistinctAggregations(IReadOnlyList<string> fieldNames)
    {
        var aggregationNames = fieldNames.Select(field => SanitizeAggregationName(field, "distinct_")).ToList();
        var aggregations = BuildGroupAggregations(fieldNames, aggregationNames, new JsonObject());
        return (aggregations, aggregationNames);
    }

    private static JsonObject BuildGroupAggregations(
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<string> aggregationNames,
        JsonObject metricsAggregations)
    {
        JsonObject? root = null;
        JsonObject? current = null;

        for (var i = 0; i < groupByFields.Count; i++)
        {
            var aggName = aggregationNames[i];
            var fieldName = groupByFields[i];

            var terms = new JsonObject
            {
                ["field"] = fieldName,
                ["size"] = DefaultTermsSize
            };

            var container = new JsonObject
            {
                [aggName] = new JsonObject
                {
                    ["terms"] = terms
                }
            };

            if (root is null)
            {
                root = container;
            }
            else
            {
                current!["aggs"] = container;
            }

            current = (JsonObject)container[aggName]!
                ?? throw new InvalidOperationException("Failed to create aggregation container for Elasticsearch query.");
        }

        if (current is not null)
        {
            if (metricsAggregations.Count > 0)
            {
                current["aggs"] = metricsAggregations;
            }
            else if (!current.ContainsKey("aggs"))
            {
                current["aggs"] = new JsonObject();
            }
        }

        return root ?? metricsAggregations;
    }

    private static IReadOnlyList<StatisticsResult> ParseUngroupedStatistics(JsonElement root, IReadOnlyList<ElasticsearchStatisticMapping> mappings)
    {
        var statistics = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var aggregations = root.TryGetProperty("aggregations", out var aggsElement) ? aggsElement : default;

        foreach (var mapping in mappings)
        {
            object? value;

            if (mapping.UseDocCount)
            {
                value = ExtractTotalCount(root);
            }
            else if (mapping.AggregationName.HasValue() &&
                     aggregations.ValueKind != JsonValueKind.Undefined &&
                     aggregations.TryGetProperty(mapping.AggregationName, out var metricElement) &&
                     metricElement.TryGetProperty("value", out var valueElement))
            {
                value = valueElement.ValueKind == JsonValueKind.Null ? null : ConvertJsonElementValue(valueElement);
            }
            else
            {
                value = null;
            }

            statistics[mapping.OutputName] = value;
        }

        var emptyGroups = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
        var statsReadOnly = new ReadOnlyDictionary<string, object?>(statistics);
        return new[] { new StatisticsResult(emptyGroups, statsReadOnly) };
    }

    private static IReadOnlyList<StatisticsResult> ParseGroupedStatistics(
        JsonElement root,
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<string> aggregationNames,
        IReadOnlyList<ElasticsearchStatisticMapping> mappings)
    {
        if (!root.TryGetProperty("aggregations", out var aggregationsElement))
        {
            return Array.Empty<StatisticsResult>();
        }

        var results = new List<StatisticsResult>();
        var currentGroup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        TraverseStatisticsBuckets(aggregationsElement, 0, groupByFields, aggregationNames, mappings, currentGroup, results);

        return results;
    }

    private static void TraverseStatisticsBuckets(
        JsonElement container,
        int depth,
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<string> aggregationNames,
        IReadOnlyList<ElasticsearchStatisticMapping> mappings,
        Dictionary<string, object?> currentGroup,
        List<StatisticsResult> results)
    {
        if (depth >= aggregationNames.Count)
        {
            var statistics = ExtractStatisticsFromBucket(container, mappings);
            var groupValues = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(currentGroup, StringComparer.OrdinalIgnoreCase));
            results.Add(new StatisticsResult(groupValues, statistics));
            return;
        }

        if (!container.TryGetProperty(aggregationNames[depth], out var aggregationElement) ||
            !aggregationElement.TryGetProperty("buckets", out var bucketsElement))
        {
            return;
        }

        foreach (var bucket in bucketsElement.EnumerateArray())
        {
            var key = bucket.TryGetProperty("key", out var keyElement)
                ? ConvertJsonElementValue(keyElement)
                : null;

            currentGroup[groupByFields[depth]] = key;
            TraverseStatisticsBuckets(bucket, depth + 1, groupByFields, aggregationNames, mappings, currentGroup, results);
        }

        currentGroup.Remove(groupByFields[depth]);
    }

    private static ReadOnlyDictionary<string, object?> ExtractStatisticsFromBucket(
        JsonElement bucket,
        IReadOnlyList<ElasticsearchStatisticMapping> mappings)
    {
        var stats = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings)
        {
            object? value;

            if (mapping.UseDocCount)
            {
                value = bucket.TryGetProperty("doc_count", out var countElement) && countElement.TryGetInt64(out var docCount)
                    ? docCount
                    : 0L;
            }
            else if (mapping.AggregationName.HasValue() &&
                     bucket.TryGetProperty(mapping.AggregationName, out var metricElement) &&
                     metricElement.TryGetProperty("value", out var valueElement))
            {
                value = valueElement.ValueKind == JsonValueKind.Null ? null : ConvertJsonElementValue(valueElement);
            }
            else
            {
                value = null;
            }

            stats[mapping.OutputName] = value;
        }

        return new ReadOnlyDictionary<string, object?>(stats);
    }

    private static IReadOnlyList<DistinctResult> ParseDistinctResults(
        JsonElement root,
        IReadOnlyList<string> fieldNames,
        IReadOnlyList<string> aggregationNames)
    {
        if (!root.TryGetProperty("aggregations", out var aggregationsElement))
        {
            return Array.Empty<DistinctResult>();
        }

        var results = new List<DistinctResult>();
        var currentValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        TraverseDistinctBuckets(aggregationsElement, 0, fieldNames, aggregationNames, currentValues, results);

        return results;
    }

    private static void TraverseDistinctBuckets(
        JsonElement container,
        int depth,
        IReadOnlyList<string> fieldNames,
        IReadOnlyList<string> aggregationNames,
        Dictionary<string, object?> currentValues,
        List<DistinctResult> results)
    {
        if (depth >= aggregationNames.Count)
        {
            var valueCopy = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(currentValues, StringComparer.OrdinalIgnoreCase));
            results.Add(new DistinctResult(valueCopy));
            return;
        }

        if (!container.TryGetProperty(aggregationNames[depth], out var aggregationElement) ||
            !aggregationElement.TryGetProperty("buckets", out var bucketsElement))
        {
            return;
        }

        foreach (var bucket in bucketsElement.EnumerateArray())
        {
            var key = bucket.TryGetProperty("key", out var keyElement)
                ? ConvertJsonElementValue(keyElement)
                : null;

            currentValues[fieldNames[depth]] = key;
            TraverseDistinctBuckets(bucket, depth + 1, fieldNames, aggregationNames, currentValues, results);
        }

        currentValues.Remove(fieldNames[depth]);
    }

    private static string SanitizeAggregationName(string value, string prefix)
    {
        var builder = new StringBuilder(prefix.Length + Math.Max(value?.Length ?? 0, 4));
        builder.Append(prefix);

        if (value.IsNullOrWhiteSpace())
        {
            builder.Append("agg");
            return builder.ToString();
        }

        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        return builder.ToString();
    }

    private static JsonObject BuildMetricAggregation(StatisticDefinition stat)
    {
        if (stat.FieldName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Statistic '{stat.Type}' requires a field name.");
        }

        return stat.Type switch
        {
            StatisticType.Sum => new JsonObject { ["sum"] = new JsonObject { ["field"] = stat.FieldName } },
            StatisticType.Avg => new JsonObject { ["avg"] = new JsonObject { ["field"] = stat.FieldName } },
            StatisticType.Min => new JsonObject { ["min"] = new JsonObject { ["field"] = stat.FieldName } },
            StatisticType.Max => new JsonObject { ["max"] = new JsonObject { ["field"] = stat.FieldName } },
            _ => throw new NotSupportedException($"Statistic type '{stat.Type}' is not supported by the Elasticsearch provider.")
        };
    }

    private static long ExtractTotalCount(JsonElement root)
    {
        if (root.TryGetProperty("hits", out var hitsElement) &&
            hitsElement.TryGetProperty("total", out var totalElement) &&
            totalElement.TryGetProperty("value", out var valueElement))
        {
            if (valueElement.TryGetInt64(out var count))
            {
                return count;
            }

            if (valueElement.TryGetDouble(out var doubleValue))
            {
                return (long)Math.Round(doubleValue, MidpointRounding.AwayFromZero);
            }
        }

        return 0L;
    }
}
