// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Data;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Handlers for WFS capabilities-related operations.
/// </summary>
internal static class WfsCapabilitiesHandlers
{
    /// <summary>
    /// Handles GetCapabilities requests.
    /// </summary>
    public static async Task<IResult> HandleGetCapabilitiesAsync(
        HttpRequest request,
        [FromServices] MetadataSnapshot snapshot,
        [FromServices] ICapabilitiesCache capabilitiesCache,
        CancellationToken cancellationToken)
    {
        using var activity = HonuaTelemetry.OgcProtocols.StartActivity("WFS GetCapabilities");
        activity?.SetTag("wfs.operation", "GetCapabilities");

        var featureTypeCount = snapshot.Services
            .Where(s => s.Enabled && s.Ogc.CollectionsEnabled)
            .Sum(s => s.Layers.Count);
        activity?.SetTag("wfs.feature_type_count", featureTypeCount);

        // Extract version and language from request
        var query = request.Query;
        var version = QueryParsingHelpers.GetQueryValue(query, "version") ?? "2.0.0";
        var acceptLanguage = request.Headers["Accept-Language"].FirstOrDefault();

        // Try to get from cache
        if (capabilitiesCache.TryGetCapabilities("wfs", "global", version, acceptLanguage, out var cachedXml))
        {
            activity?.SetTag("wfs.cache_hit", true);
            return Results.Content(cachedXml, "application/xml");
        }

        activity?.SetTag("wfs.cache_hit", false);

        // Cache miss - generate capabilities
        var builder = new WfsCapabilitiesBuilder();
        var xml = await builder.BuildCapabilitiesAsync(snapshot, request, cancellationToken).ConfigureAwait(false);

        // Store in cache
        await capabilitiesCache.SetCapabilitiesAsync("wfs", "global", version, acceptLanguage, xml, cancellationToken)
            .ConfigureAwait(false);

        return Results.Content(xml, "application/xml");
    }

    /// <summary>
    /// Handles DescribeFeatureType requests.
    /// </summary>
    public static async Task<IResult> HandleDescribeFeatureTypeAsync(
        HttpRequest request,
        IQueryCollection query,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IWfsSchemaCache schemaCache,
        CancellationToken cancellationToken)
    {
        var typeNamesRaw = QueryParsingHelpers.GetQueryValue(query, "typeNames") ?? QueryParsingHelpers.GetQueryValue(query, "typeName");
        if (typeNamesRaw.IsNullOrWhiteSpace())
        {
            return WfsHelpers.CreateException("MissingParameterValue", "typeNames", "Parameter 'typeNames' is required.");
        }

        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeNamesRaw, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
        if (contextResult.IsFailure)
        {
            return WfsHelpers.MapResolutionError(contextResult.Error!, typeNamesRaw);
        }

        var context = contextResult.Value;
        var service = context.Service;
        var layer = context.Layer;

        // Try to get schema from cache
        var collectionId = $"{service.Id}:{layer.Id}";
        if (schemaCache.TryGetSchema(collectionId, out var cachedSchema) && cachedSchema != null)
        {
            return Results.Content(cachedSchema.ToString(SaveOptions.DisableFormatting), "application/xml");
        }

        // Cache miss - generate schema
        var targetNamespace = $"https://honua.dev/wfs/{service.Id}";
        var schema = new XDocument(
            new XElement(WfsConstants.Xs + "schema",
                new XAttribute(XNamespace.Xmlns + "xs", WfsConstants.Xs),
                new XAttribute(XNamespace.Xmlns + "gml", WfsConstants.Gml),
                new XAttribute(XNamespace.Xmlns + "tns", targetNamespace),
                new XAttribute("targetNamespace", targetNamespace),
                new XAttribute("elementFormDefault", "qualified"),
                // WFS COMPLIANCE: Import GML schema for gml:*PropertyType references
                new XElement(WfsConstants.Xs + "import",
                    new XAttribute("namespace", WfsConstants.Gml),
                    new XAttribute("schemaLocation", "http://schemas.opengis.net/gml/3.2.1/gml.xsd")),
                BuildFeatureTypeSchema(layer),
                new XElement(WfsConstants.Xs + "element",
                    new XAttribute("name", layer.Id),
                    new XAttribute("type", $"tns:{layer.Id}"))));

        // Store in cache
        await schemaCache.SetSchemaAsync(collectionId, schema, cancellationToken).ConfigureAwait(false);

        return Results.Content(schema.ToString(SaveOptions.DisableFormatting), "application/xml");
    }

    /// <summary>
    /// Handles ListStoredQueries requests.
    /// </summary>
    public static async Task<IResult> HandleListStoredQueriesAsync(
        HttpRequest request,
        [FromServices] IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        var snapshot = await registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var queryElements = new List<XElement>
        {
            // WFS 2.0 requires at minimum the GetFeatureById stored query
            new XElement(WfsConstants.Wfs + "StoredQuery",
                new XAttribute("id", "urn:ogc:def:query:OGC-WFS::GetFeatureById"))
        };

        // Add configured stored queries from all services
        foreach (var service in snapshot.Services)
        {
            foreach (var storedQuery in service.Ogc.StoredQueries)
            {
                queryElements.Add(new XElement(WfsConstants.Wfs + "StoredQuery",
                    new XAttribute("id", storedQuery.Id)));
            }
        }

        var doc = new XDocument(
            new XElement(WfsConstants.Wfs + "ListStoredQueriesResponse",
                new XAttribute(XNamespace.Xmlns + "wfs", WfsConstants.Wfs),
                queryElements));

        return Results.Content(doc.ToString(SaveOptions.DisableFormatting), "application/xml");
    }

    /// <summary>
    /// Handles DescribeStoredQueries requests.
    /// </summary>
    public static async Task<IResult> HandleDescribeStoredQueriesAsync(
        HttpRequest request,
        IQueryCollection query,
        [FromServices] IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(query);

        var snapshot = await registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var storedQueryId = QueryParsingHelpers.GetQueryValue(query, "storedQueryId") ?? QueryParsingHelpers.GetQueryValue(query, "STOREDQUERY_ID");

        var descriptions = new List<XElement>
        {
            // WFS 2.0 GetFeatureById mandatory query
            new XElement(WfsConstants.Wfs + "StoredQueryDescription",
                new XAttribute("id", "urn:ogc:def:query:OGC-WFS::GetFeatureById"),
                new XElement(WfsConstants.Wfs + "Title", "Get feature by identifier"),
                new XElement(WfsConstants.Wfs + "Abstract", "Returns a feature given its identifier"),
                new XElement(WfsConstants.Wfs + "Parameter",
                    new XAttribute("name", "id"),
                    new XAttribute("type", "xs:string"),
                    new XElement(WfsConstants.Wfs + "Title", "Feature identifier")),
                new XElement(WfsConstants.Wfs + "QueryExpressionText",
                    new XAttribute("returnFeatureTypes", ""),
                    new XAttribute("language", "urn:ogc:def:queryLanguage:OGC-WFS::WFS_QueryExpression"),
                    new XAttribute("isPrivate", "false")))
        };

        // Add configured stored queries from all services
        foreach (var service in snapshot.Services)
        {
            foreach (var storedQuery in service.Ogc.StoredQueries)
            {
                // Filter by specific query ID if requested
                if (storedQueryId.HasValue() &&
                    !storedQuery.Id.EqualsIgnoreCase(storedQueryId))
                {
                    continue;
                }

                var parameters = storedQuery.Parameters
                    .Select(p => new XElement(WfsConstants.Wfs + "Parameter",
                        new XAttribute("name", p.Name),
                        new XAttribute("type", p.Type),
                        new XElement(WfsConstants.Wfs + "Title", p.Title),
                        p.Abstract.IsNullOrWhiteSpace() ? null : new XElement(WfsConstants.Wfs + "Abstract", p.Abstract)))
                    .ToList();

                descriptions.Add(new XElement(WfsConstants.Wfs + "StoredQueryDescription",
                    new XAttribute("id", storedQuery.Id),
                    new XElement(WfsConstants.Wfs + "Title", storedQuery.Title),
                    storedQuery.Abstract.IsNullOrWhiteSpace() ? null : new XElement(WfsConstants.Wfs + "Abstract", storedQuery.Abstract),
                    parameters,
                    new XElement(WfsConstants.Wfs + "QueryExpressionText",
                        new XAttribute("returnFeatureTypes", $"{service.Id}:{storedQuery.LayerId}"),
                        new XAttribute("language", "urn:ogc:def:queryLanguage:OGC-WFS::WFS_QueryExpression"),
                        new XAttribute("isPrivate", "false"))));
            }
        }

        var doc = new XDocument(
            new XElement(WfsConstants.Wfs + "DescribeStoredQueriesResponse",
                new XAttribute(XNamespace.Xmlns + "wfs", WfsConstants.Wfs),
                new XAttribute(XNamespace.Xmlns + "fes", WfsConstants.Fes),
                descriptions));

        return Results.Content(doc.ToString(SaveOptions.DisableFormatting), "application/xml");
    }

    #region Private Helper Methods

    private static XElement BuildFeatureTypeSchema(LayerDefinition layer)
    {
        var elements = new List<XElement>();

        var geometryElement = new XElement(WfsConstants.Xs + "element",
            new XAttribute("name", layer.GeometryField),
            new XAttribute("type", WfsHelpers.ResolveGeometryType(layer.GeometryType ?? string.Empty)),
            new XAttribute("minOccurs", 0));
        elements.Add(geometryElement);

        var fields = FieldMetadataResolver.ResolveFields(layer, includeGeometry: false, includeIdField: true);
        foreach (var field in fields)
        {
            elements.Add(new XElement(WfsConstants.Xs + "element",
                new XAttribute("name", field.Name),
                new XAttribute("type", WfsHelpers.MapFieldType(field.DataType))));
        }

        return new XElement(WfsConstants.Xs + "complexType",
            new XAttribute("name", layer.Id),
            new XElement(WfsConstants.Xs + "sequence", elements));
    }

    #endregion
}
