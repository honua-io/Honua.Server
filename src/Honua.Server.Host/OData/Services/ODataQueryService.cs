// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using UriParserODataPath = Microsoft.OData.UriParser.ODataPath;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OData.Services;

/// <summary>
/// Service responsible for OData query translation and execution.
/// Handles $filter, $top, $skip, $orderby, $select, and $count operations.
/// </summary>
public sealed class ODataQueryService
{
    private readonly ODataModelCache _modelCache;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ILogger<ODataQueryService> _logger;

    public ODataQueryService(
        ODataModelCache modelCache,
        IHonuaConfigurationService configurationService,
        IMetadataRegistry metadataRegistry,
        ILogger<ODataQueryService> logger)
    {
        _modelCache = Guard.NotNull(modelCache);
        _configurationService = Guard.NotNull(configurationService);
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Builds an OData query options object and translates it into a FeatureQuery for execution.
    /// </summary>
    /// <param name="request">The HTTP request containing OData query parameters ($filter, $top, $skip, etc.)</param>
    /// <param name="metadata">Metadata describing the OData entity structure and properties</param>
    /// <param name="entityType">The EDM entity type definition</param>
    /// <param name="path">Optional OData path object for advanced routing scenarios</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A tuple containing the parsed OData query options and the translated feature query</returns>
    public async Task<(ODataQueryOptions Options, FeatureQuery Query)> BuildFeatureQueryAsync(
        HttpRequest request,
        ODataEntityMetadata metadata,
        IEdmEntityType entityType,
        object? path,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(metadata);

        var descriptor = await _modelCache
            .GetOrCreateAsync(cancellationToken)
            .ConfigureAwait(false);
        var edmPath = path as UriParserODataPath;
        var context = new ODataQueryContext(descriptor.Model, typeof(Microsoft.AspNetCore.OData.Formatter.Value.EdmEntityObject), edmPath);
        var options = new ODataQueryOptions(context, request);
        var odataConfiguration = GetODataConfiguration();

        var limit = ComputeLimit(options, odataConfiguration);
        var offset = ComputeOffset(options);
        var sortOrders = BuildSortOrders(options);
        var propertyNames = BuildPropertyNames(options);
        var (filter, entityDefinition) = await BuildFilterAsync(request, options, metadata, cancellationToken).ConfigureAwait(false);

        var query = new FeatureQuery(
            Limit: limit,
            Offset: offset,
            ResultType: FeatureResultType.Results,
            PropertyNames: propertyNames,
            SortOrders: sortOrders,
            Filter: filter,
            EntityDefinition: entityDefinition);

        return (options, query);
    }

    public bool HasGeoIntersectsFilter(HttpRequest request)
    {
        if (request.HttpContext.Items.TryGetValue(ODataGeoFilterContext.OriginalFilterKey, out var storedFilter) && storedFilter is string stored)
        {
            if (stored.Contains("geo.intersects", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (request.Query.TryGetValue("$filter", out var filters) && filters.Count > 0)
        {
            return filters.ToString().Contains("geo.intersects", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public async Task<bool> ShouldPushDownGeoIntersectsAsync(
        ODataEntityMetadata metadata,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken = default)
    {
        var dataSourceId = metadata.Service.DataSourceId;
        var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var dataSource = snapshot.DataSources.FirstOrDefault(ds =>
            string.Equals(ds.Id, dataSourceId, StringComparison.OrdinalIgnoreCase));

        var provider = dataSource?.Provider;
        if (provider.IsNullOrWhiteSpace())
        {
            return false;
        }

        return provider switch
        {
            "postgis" => true,
            "mysql" => true,
            "sqlserver" => true,
            _ => false
        };
    }

    private int? ComputeLimit(ODataQueryOptions options, ODataConfiguration odataConfiguration)
    {
        const int AbsoluteMaxPageSize = 10_000;
        const int DefaultMaxPageSize = 1000;

        var configuredMaxPageSize = odataConfiguration.MaxPageSize > 0
            ? Math.Min(odataConfiguration.MaxPageSize, AbsoluteMaxPageSize)
            : DefaultMaxPageSize;

        int? limit = null;
        var topValue = options.Top?.Value;
        if (topValue is not null)
        {
            limit = Math.Min(Convert.ToInt32(topValue, CultureInfo.InvariantCulture), configuredMaxPageSize);
        }
        else if (odataConfiguration.DefaultPageSize > 0)
        {
            limit = Math.Min(odataConfiguration.DefaultPageSize, configuredMaxPageSize);
        }

        // NEVER return null - always enforce a limit
        if (limit is null || limit <= 0)
        {
            limit = DefaultMaxPageSize;
        }

        // Final safety check - enforce absolute maximum
        return Math.Min(limit.Value, AbsoluteMaxPageSize);
    }

    private int? ComputeOffset(ODataQueryOptions options)
    {
        int? offset = null;
        var skipValue = options.Skip?.Value;
        if (skipValue is not null)
        {
            var skip = Convert.ToInt64(skipValue, CultureInfo.InvariantCulture);
            if (skip < 0)
            {
                skip = 0;
            }

            offset = skip >= int.MaxValue ? int.MaxValue : (int)skip;
        }

        return offset;
    }

    private List<FeatureSortOrder>? BuildSortOrders(ODataQueryOptions options)
    {
        var orderByClause = options.OrderBy?.OrderByClause;
        if (orderByClause is null)
        {
            return null;
        }

        var sortOrders = new List<FeatureSortOrder>();
        for (var clause = orderByClause; clause is not null; clause = clause.ThenBy)
        {
            var field = ResolveOrderByProperty(clause.Expression);
            var direction = clause.Direction == OrderByDirection.Descending
                ? FeatureSortDirection.Descending
                : FeatureSortDirection.Ascending;
            sortOrders.Add(new FeatureSortOrder(field, direction));
        }

        return sortOrders;
    }

    private IReadOnlyList<string>? BuildPropertyNames(ODataQueryOptions options)
    {
        var rawSelect = options.SelectExpand?.RawSelect;
        if (rawSelect.IsNullOrWhiteSpace())
        {
            return null;
        }

        var propertyNames = rawSelect
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return propertyNames.Length == 0 ? null : propertyNames;
    }

    private async Task<(QueryFilter? Filter, QueryEntityDefinition? EntityDefinition)> BuildFilterAsync(
        HttpRequest request,
        ODataQueryOptions options,
        ODataEntityMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        QueryEntityDefinition? entityDefinition = null;
        QueryFilter? filter = null;

        string? rawFilter = null;
        if (request.HttpContext.Items.TryGetValue(ODataGeoFilterContext.OriginalFilterKey, out var storedFilter) && storedFilter is string stored)
        {
            rawFilter = stored;
            request.HttpContext.Items.Remove(ODataGeoFilterContext.OriginalFilterKey);
        }
        else
        {
            rawFilter = options.Filter?.RawValue;
        }

        if (rawFilter.HasValue() &&
            rawFilter.Contains("geo.intersects", StringComparison.OrdinalIgnoreCase))
        {
            entityDefinition = await BuildQueryEntityDefinitionAsync(metadata, cancellationToken).ConfigureAwait(false);
            filter = TryParseGeoIntersectsFilter(rawFilter, entityDefinition, metadata.Layer.Storage?.Srid, request);

            var shouldPushDown = request.HttpContext.Items.TryGetValue(
                    ODataGeoFilterContext.GeoIntersectsPushdownKey,
                    out var pushDownFlag) &&
                pushDownFlag is true;

            _logger.LogDebug(
                shouldPushDown
                    ? "Geo.intersects pushdown enabled for this request."
                    : "Geo.intersects pushdown disabled for this request.");

            if (!shouldPushDown &&
                request.HttpContext.Items.ContainsKey(ODataGeoFilterContext.GeoIntersectsInfoKey))
            {
                filter = null;
            }
            else if (shouldPushDown && filter is not null)
            {
                request.HttpContext.Items[ODataGeoFilterContext.GeoIntersectsPushdownAppliedKey] = true;
            }

            if (filter is null && options.Filter?.FilterClause is not null)
            {
                var parser = new ODataFilterParser(entityDefinition);
                filter = parser.Parse(options.Filter.FilterClause);
            }
        }
        else if (options.Filter?.FilterClause is not null)
        {
            entityDefinition = await BuildQueryEntityDefinitionAsync(metadata, cancellationToken).ConfigureAwait(false);
            var parser = new ODataFilterParser(entityDefinition);
            filter = parser.Parse(options.Filter.FilterClause);
        }

        return (filter, entityDefinition);
    }

    private QueryFilter? TryParseGeoIntersectsFilter(string rawFilter, QueryEntityDefinition entityDefinition, int? storageSrid, HttpRequest request)
    {
        var trimmed = rawFilter.Trim();
        if (!trimmed.StartsWith("geo.intersects", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var openParenIndex = trimmed.IndexOf('(');
        if (openParenIndex < 0 || !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return null;
        }

        var inner = trimmed.Substring(openParenIndex + 1, trimmed.Length - openParenIndex - 2);
        var arguments = SplitTopLevelArguments(inner);
        if (arguments is null || arguments.Count != 2)
        {
            return null;
        }

        var fieldToken = arguments[0];
        if (!entityDefinition.Fields.ContainsKey(fieldToken))
        {
            return null;
        }

        var geometryToken = arguments[1].Trim();
        var literalDelimiterIndex = geometryToken.IndexOf('\'');
        if (literalDelimiterIndex < 0)
        {
            return null;
        }

        if (!geometryToken.EndsWith("'", StringComparison.Ordinal))
        {
            return null;
        }

        var prefix = geometryToken[..literalDelimiterIndex].Trim();
        if (!prefix.Equals("geometry", StringComparison.OrdinalIgnoreCase) &&
            !prefix.Equals("geography", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var literal = geometryToken[(literalDelimiterIndex + 1)..^1];
        if (literal.IsNullOrWhiteSpace())
        {
            return null;
        }

        int? srid = null;
        var wkt = literal;
        if (literal.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = literal.IndexOf(';');
            if (separator > 5 && int.TryParse(literal.Substring(5, separator - 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSrid))
            {
                srid = parsedSrid;
                wkt = literal[(separator + 1)..];
            }
        }

        var queryGeometry = new QueryGeometryValue(wkt, srid);
        request.HttpContext.Items[ODataGeoFilterContext.GeoIntersectsInfoKey] = new GeoIntersectsFilterInfo(fieldToken, queryGeometry, storageSrid);

        var function = new QueryFunctionExpression("geo.intersects", new QueryExpression[]
        {
            new QueryFieldReference(fieldToken),
            new QueryConstant(queryGeometry)
        });

        return new QueryFilter(function);
    }

    private static List<string>? SplitTopLevelArguments(string input)
    {
        if (input.IsNullOrWhiteSpace())
        {
            return null;
        }

        var results = new List<string>();
        var start = 0;
        var inQuote = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '\'')
            {
                inQuote = !inQuote;
            }
            else if (ch == ',' && !inQuote)
            {
                var part = input[start..i].Trim();
                if (part.Length == 0)
                {
                    return null;
                }

                results.Add(part);
                start = i + 1;
            }
        }

        var lastPart = input[start..].Trim();
        if (lastPart.Length == 0)
        {
            return null;
        }

        results.Add(lastPart);
        return results;
    }

    private async Task<QueryEntityDefinition> BuildQueryEntityDefinitionAsync(
        ODataEntityMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var builder = new MetadataQueryModelBuilder();
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return builder.Build(snapshot, metadata.Service, metadata.Layer);
    }

    private static string ResolveOrderByProperty(SingleValueNode expression)
    {
        return expression switch
        {
            SingleValuePropertyAccessNode propertyNode when propertyNode.Property is not null => propertyNode.Property.Name,
            SingleValueOpenPropertyAccessNode openPropertyNode => openPropertyNode.Name,
            ConvertNode convertNode => ResolveOrderByProperty(convertNode.Source),
            _ => throw new NotSupportedException($"Only property-based $orderby clauses are supported. Received '{expression?.GetType().FullName ?? "unknown"}'.")
        };
    }

    private ODataConfiguration GetODataConfiguration() => _configurationService.Current.Services.OData;
}
