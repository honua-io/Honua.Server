// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Resolves field-related parameters for Geoservices REST API requests.
/// </summary>
internal static class GeoservicesFieldResolver
{
    public static (string OutFields, IReadOnlyList<string>? PropertyNames, IReadOnlyDictionary<string, string> SelectedFields, IReadOnlyCollection<string>? RequestedFields)
        ResolveOutFields(IQueryCollection query, LayerDefinition layer, bool returnIdsOnly)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var metadataFields = new HashSet<string>(layer.Fields.Select(f => f.Name), comparer)
        {
            layer.IdField
        };
        metadataFields.RemoveWhere(name => name.EqualsIgnoreCase(layer.GeometryField));

        string? rawOutFields = returnIdsOnly
            ? layer.IdField
            : query.TryGetValue("outFields", out var values) && values.Count > 0
                ? values[^1]
                : "*";

        rawOutFields ??= "*";

        // Handle "*" case separately as it means "all fields"
        if (rawOutFields.EqualsIgnoreCase("*"))
        {
            var selected = metadataFields.ToDictionary(name => name, name => name, comparer);
            return ("*", null, selected, null);
        }

        // SECURITY: Pre-validate field count before parsing
        var tempRequested = QueryParsingHelpers.ParseCsv(rawOutFields);
        GeoservicesRESTInputValidator.ValidateOutFieldsCount(tempRequested.Count);

        // Use QueryParameterHelper for core parsing
        var (propertyNames, error) = QueryParameterHelper.ParsePropertyNames(
            rawOutFields,
            metadataFields,
            layer.IdField,
            layer.GeometryField);

        if (error is not null)
        {
            ThrowBadRequest(error);
        }

        if (propertyNames is null)
        {
            var selectedAll = metadataFields.ToDictionary(name => name, name => name, comparer);
            return (rawOutFields, null, selectedAll, null);
        }

        // propertyNames is never null here because we checked for "*" above
        var normalized = propertyNames.ToList();
        var selectedMap = new Dictionary<string, string>(comparer);
        var requestedOrder = new List<string>();

        // Build the selected map and maintain request order
        foreach (var field in normalized)
        {
            if (!selectedMap.ContainsKey(field))
            {
                selectedMap[field] = field;
                requestedOrder.Add(field);
            }
        }

        // Ensure ID field is always included (QueryParameterHelper already does this, but be explicit)
        if (!selectedMap.ContainsKey(layer.IdField))
        {
            selectedMap[layer.IdField] = layer.IdField;
            normalized.Add(layer.IdField);
        }

        IReadOnlyCollection<string>? requestedFields = requestedOrder.Count == 0
            ? Array.Empty<string>()
            : new ReadOnlyCollection<string>(requestedOrder);

        return (rawOutFields, normalized, selectedMap, requestedFields);
    }

    public static IReadOnlyList<FeatureSortOrder>? ResolveOrderByFields(IQueryCollection query, LayerDefinition layer)
    {
        var rawValue = query.TryGetValue("orderByFields", out var values) && values.Count > 0
            ? values[^1]
            : null;

        var comparer = StringComparer.OrdinalIgnoreCase;
        var validFields = new HashSet<string>(layer.Fields.Select(f => f.Name), comparer)
        {
            layer.IdField
        };

        var (sortOrders, error) = QueryParameterHelper.ParseSortOrders(
            rawValue,
            validFields,
            separator: ' ');

        if (error is not null)
        {
            ThrowBadRequest(error);
        }

        return sortOrders;
    }

    public static IReadOnlyList<string> ResolveGroupByFields(IQueryCollection query, LayerDefinition layer)
    {
        if (!query.TryGetValue("groupByFieldsForStatistics", out var values) || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var raw = values[^1];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var tokens = QueryParsingHelpers.ParseCsv(raw);
        if (tokens.Count == 0)
        {
            return Array.Empty<string>();
        }

        var resolved = new List<string>(tokens.Count);
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var normalized = NormalizeFieldName(token, layer);
            if (!resolved.Any(field => field.EqualsIgnoreCase(normalized)))
            {
                resolved.Add(normalized);
            }
        }

        return resolved.Count == 0 ? Array.Empty<string>() : resolved;
    }

    public static string NormalizeFieldName(string field, LayerDefinition layer)
    {
        if (field.EqualsIgnoreCase(layer.IdField))
        {
            return layer.IdField;
        }

        var match = layer.Fields.FirstOrDefault(f => f.Name.EqualsIgnoreCase(field));
        if (match is null)
        {
            ThrowBadRequest($"Field '{field}' is not defined for layer '{layer.Id}'.");
        }

        return match!.Name;
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}
