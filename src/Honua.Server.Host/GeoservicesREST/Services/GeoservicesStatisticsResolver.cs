// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Resolves statistical query parameters for Geoservices REST API requests.
/// </summary>
internal static class GeoservicesStatisticsResolver
{
    public static IReadOnlyList<GeoservicesRESTStatisticDefinition> ResolveStatistics(IQueryCollection query, LayerDefinition layer)
    {
        if (!query.TryGetValue("outStatistics", out var values) || values.Count == 0)
        {
            return Array.Empty<GeoservicesRESTStatisticDefinition>();
        }

        var raw = values[^1];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<GeoservicesRESTStatisticDefinition>();
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                ThrowBadRequest("outStatistics must be a JSON array.");
            }

            var entries = new List<GeoservicesRESTStatisticDefinition>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    ThrowBadRequest("Each outStatistics entry must be a JSON object.");
                }

                if (!element.TryGetProperty("statisticType", out var typeProperty) || typeProperty.ValueKind != JsonValueKind.String)
                {
                    ThrowBadRequest("Each outStatistics entry must specify statisticType.");
                }

                var statisticType = ParseStatisticType(typeProperty.GetString());

                string? fieldName = null;
                FieldDefinition? fieldDefinition = null;

                if (element.TryGetProperty("onStatisticField", out var fieldProperty) && fieldProperty.ValueKind != JsonValueKind.Null)
                {
                    var fieldValue = fieldProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(fieldValue) && !fieldValue!.EqualsIgnoreCase("*"))
                    {
                        fieldName = GeoservicesFieldResolver.NormalizeFieldName(fieldValue!, layer);
                        fieldDefinition = layer.Fields.FirstOrDefault(f => f.Name.EqualsIgnoreCase(fieldName));
                        if (fieldDefinition is null && !fieldName.EqualsIgnoreCase(layer.IdField))
                        {
                            ThrowBadRequest($"Field '{fieldValue}' referenced in outStatistics is not defined for layer '{layer.Id}'.");
                        }
                    }
                }

                if (statisticType != GeoservicesRESTStatisticType.Count && string.IsNullOrWhiteSpace(fieldName))
                {
                    ThrowBadRequest("onStatisticField is required for sum, avg, min, and max statistics.");
                }

                if (!element.TryGetProperty("outStatisticFieldName", out var nameProperty) || nameProperty.ValueKind != JsonValueKind.String)
                {
                    ThrowBadRequest("Each outStatistics entry must include outStatisticFieldName.");
                }

                var outputName = nameProperty.GetString();
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    ThrowBadRequest("outStatisticFieldName cannot be empty.");
                }

                if (entries.Any(existing => existing.OutputName.EqualsIgnoreCase(outputName)))
                {
                    ThrowBadRequest($"Duplicate outStatisticFieldName '{outputName}' detected.");
                }

                entries.Add(new GeoservicesRESTStatisticDefinition(outputName!, statisticType, fieldName, fieldDefinition));
            }

            // SECURITY: Validate statistics count
            GeoservicesRESTInputValidator.ValidateStatisticsCount(entries.Count);

            return entries.Count == 0 ? Array.Empty<GeoservicesRESTStatisticDefinition>() : entries;
        }
        catch (JsonException)
        {
            ThrowBadRequest("outStatistics must be valid JSON.");
            return Array.Empty<GeoservicesRESTStatisticDefinition>();
        }
    }

    private static GeoservicesRESTStatisticType ParseStatisticType(string? statisticType)
    {
        if (string.IsNullOrWhiteSpace(statisticType))
        {
            ThrowBadRequest("statisticType must be specified.");
        }

        return statisticType!.Trim().ToLowerInvariant() switch
        {
            "count" => GeoservicesRESTStatisticType.Count,
            "sum" => GeoservicesRESTStatisticType.Sum,
            "avg" or "average" => GeoservicesRESTStatisticType.Avg,
            "min" => GeoservicesRESTStatisticType.Min,
            "max" => GeoservicesRESTStatisticType.Max,
            _ => ThrowStatisticTypeError(statisticType)
        };
    }

    private static GeoservicesRESTStatisticType ThrowStatisticTypeError(string statisticType)
    {
        ThrowBadRequest($"statisticType '{statisticType}' is not supported. Supported values are count, sum, avg, min, max.");
        return GeoservicesRESTStatisticType.Count;
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}

public enum GeoservicesRESTStatisticType
{
    Count,
    Sum,
    Avg,
    Min,
    Max
}

public sealed record GeoservicesRESTStatisticDefinition(
    string OutputName,
    GeoservicesRESTStatisticType Type,
    string? FieldName,
    FieldDefinition? FieldDefinition);
