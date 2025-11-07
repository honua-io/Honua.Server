// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Csw;

/// <summary>
/// CSW 2.0.2 (Catalog Service for the Web) implementation.
/// Provides catalog search and discovery capabilities.
/// </summary>
internal static class CswHandlers
{
    private static readonly XNamespace Csw = "http://www.opengis.net/cat/csw/2.0.2";
    private static readonly XNamespace Ows = "http://www.opengis.net/ows";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";
    private static readonly XNamespace Gmd = "http://www.isotc211.org/2005/gmd";
    private static readonly XNamespace Gco = "http://www.isotc211.org/2005/gco";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Dct = "http://purl.org/dc/terms/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    private const int DefaultMaxRecords = 10;
    private const int MaxRecordLimit = 100;

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] ICatalogProjectionService catalog,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(catalog);

        var request = context.Request;
        var query = request.Query;

        string serviceValue;
        string? requestValue;
        Dictionary<string, string> parameters;

        // Check if this is a POST request with XML body
        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            request.ContentType != null &&
            request.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            var parsedRequest = await CswXmlParser.ParsePostRequestAsync(request, cancellationToken);
            if (parsedRequest == null)
            {
                return CreateExceptionReport("NoApplicableCode", null, "Unable to parse XML request.");
            }

            serviceValue = parsedRequest.Service;
            requestValue = parsedRequest.Request;
            parameters = parsedRequest.Parameters;
        }
        else
        {
            // GET request or POST with query parameters
            serviceValue = QueryParsingHelpers.GetQueryValue(query, "service") ?? "";
            requestValue = QueryParsingHelpers.GetQueryValue(query, "request");
            parameters = new Dictionary<string, string>();
        }

        if (!string.Equals(serviceValue, "CSW", StringComparison.OrdinalIgnoreCase))
        {
            return CreateExceptionReport("InvalidParameterValue", "service", "Parameter 'service' must be set to 'CSW'.");
        }

        if (requestValue.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "request", "Parameter 'request' is required.");
        }

        var metadataSnapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return requestValue.ToUpperInvariant() switch
        {
            "GETCAPABILITIES" => await HandleGetCapabilitiesAsync(request, metadataSnapshot, cancellationToken),
            "DESCRIBERECORD" => HandleDescribeRecord(request),
            "GETRECORDS" => await HandleGetRecordsAsync(request, query, catalog, metadataSnapshot, parameters, cancellationToken),
            "GETRECORDBYID" => await HandleGetRecordByIdAsync(request, query, catalog, metadataRegistry, parameters, cancellationToken),
            "GETDOMAIN" => HandleGetDomain(request),
            "TRANSACTION" => HandleTransaction(request, parameters),
            _ => CreateExceptionReport("InvalidParameterValue", "request", $"Request '{requestValue}' is not supported.")
        };
    }

    private static async Task<IResult> HandleGetCapabilitiesAsync(HttpRequest request, MetadataSnapshot metadata, CancellationToken cancellationToken)
    {
        var builder = new CswCapabilitiesBuilder();
        var capabilities = await builder.BuildCapabilitiesAsync(metadata, request, cancellationToken).ConfigureAwait(false);

        return Results.Content(capabilities, "application/xml; charset=utf-8");
    }

    private static IResult HandleDescribeRecord(HttpRequest request)
    {
        var response = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Csw + "DescribeRecordResponse",
                new XAttribute("version", "2.0.2"),
                new XAttribute(XNamespace.Xmlns + "csw", Csw),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XElement(Csw + "SchemaComponent",
                    new XAttribute("targetNamespace", Csw.NamespaceName),
                    new XAttribute("schemaLanguage", "http://www.w3.org/XML/Schema"),
                    new XElement(XNamespace.Get("http://www.w3.org/2001/XMLSchema") + "schema",
                        new XAttribute("targetNamespace", Csw.NamespaceName),
                        new XElement(XNamespace.Get("http://www.w3.org/2001/XMLSchema") + "element",
                            new XAttribute("name", "Record"),
                            new XAttribute("type", "csw:RecordType")
                        )
                    )
                )
            )
        );

        return Results.Content(response.ToString(), "application/xml; charset=utf-8");
    }

    private static Task<IResult> HandleGetRecordsAsync(
        HttpRequest request,
        IQueryCollection query,
        ICatalogProjectionService catalog,
        MetadataSnapshot metadata,
        Dictionary<string, string> xmlParameters,
        CancellationToken cancellationToken)
    {
        return ActivityScope.Execute(
            HonuaTelemetry.Metadata,
            "CSW GetRecords",
            [("csw.operation", "GetRecords")],
            activity =>
            {
                var startPosition = int.TryParse(
                    xmlParameters.GetValueOrDefault("startPosition") ?? QueryParsingHelpers.GetQueryValue(query, "startPosition"),
                    out var sp) ? sp : 1;
                var maxRecords = int.TryParse(
                    xmlParameters.GetValueOrDefault("maxRecords") ?? QueryParsingHelpers.GetQueryValue(query, "maxRecords"),
                    out var mr) ? Math.Min(mr, MaxRecordLimit) : DefaultMaxRecords;
                var resultType = xmlParameters.GetValueOrDefault("resultType") ?? QueryParsingHelpers.GetQueryValue(query, "resultType") ?? "results";
                var outputSchema = xmlParameters.GetValueOrDefault("outputSchema") ?? QueryParsingHelpers.GetQueryValue(query, "outputSchema") ?? Csw.NamespaceName;

                // Get all records from catalog
                var searchQuery = xmlParameters.GetValueOrDefault("q") ?? QueryParsingHelpers.GetQueryValue(query, "q");
                var allRecords = catalog.Search(searchQuery);
                var totalCount = allRecords.Count;

                var records = allRecords
                    .Skip(startPosition - 1)
                    .Take(maxRecords)
                    .ToList();

                var numberOfRecordsReturned = records.Count;
                var nextRecord = startPosition + numberOfRecordsReturned <= totalCount ? startPosition + numberOfRecordsReturned : 0;

                var searchResults = new XElement(Csw + "SearchResults",
                    new XAttribute("numberOfRecordsMatched", totalCount),
                    new XAttribute("numberOfRecordsReturned", numberOfRecordsReturned),
                    new XAttribute("nextRecord", nextRecord),
                    new XAttribute("recordSchema", outputSchema)
                );

                if (resultType.Equals("results", StringComparison.OrdinalIgnoreCase))
                {
                    var useIso19139 = outputSchema.Equals("http://www.isotc211.org/2005/gmd", StringComparison.OrdinalIgnoreCase);

                    foreach (var record in records)
                    {
                        if (useIso19139)
                        {
                            var layer = metadata.Layers.FirstOrDefault(l => l.Id == record.LayerId);
                            if (layer?.Iso19115 != null)
                            {
                                searchResults.Add(Iso19139Mapper.CreateIso19139Metadata(record, layer, request));
                            }
                            else
                            {
                                // Fallback to Dublin Core if ISO 19115 metadata not available
                                searchResults.Add(CreateDublinCoreRecord(record, request));
                            }
                        }
                        else
                        {
                            searchResults.Add(CreateDublinCoreRecord(record, request));
                        }
                    }
                }

                var responseNamespaces = new XElement(Csw + "GetRecordsResponse",
                    new XAttribute("version", "2.0.2"),
                    new XAttribute(XNamespace.Xmlns + "csw", Csw),
                    new XAttribute(XNamespace.Xmlns + "dc", Dc),
                    new XAttribute(XNamespace.Xmlns + "dct", Dct),
                    new XAttribute(XNamespace.Xmlns + "ows", Ows),
                    new XAttribute(XNamespace.Xmlns + "xsi", Xsi)
                );

                // Add ISO 19139 namespaces if needed
                if (outputSchema.Equals("http://www.isotc211.org/2005/gmd", StringComparison.OrdinalIgnoreCase))
                {
                    responseNamespaces.Add(
                        new XAttribute(XNamespace.Xmlns + "gmd", Gmd),
                        new XAttribute(XNamespace.Xmlns + "gco", Gco),
                        new XAttribute(XNamespace.Xmlns + "gml", "http://www.opengis.net/gml")
                    );
                }

                responseNamespaces.Add(
                    new XElement(Csw + "SearchStatus", new XAttribute("timestamp", DateTime.UtcNow.ToString("o"))),
                    searchResults
                );

                var response = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    responseNamespaces
                );

                return Task.FromResult(Results.Content(response.ToString(), "application/xml; charset=utf-8"));
            });
    }

    private static async Task<IResult> HandleGetRecordByIdAsync(
        HttpRequest request,
        IQueryCollection query,
        ICatalogProjectionService catalog,
        IMetadataRegistry metadataRegistry,
        Dictionary<string, string> xmlParameters,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Metadata,
            "CSW GetRecordById",
            [("csw.operation", "GetRecordById")],
            async activity =>
            {
                var id = xmlParameters.GetValueOrDefault("id") ?? QueryParsingHelpers.GetQueryValue(query, "id");
                activity?.AddTag("csw.record_id", id);
                if (id.IsNullOrWhiteSpace())
                {
                    return CreateExceptionReport("MissingParameterValue", "id", "Parameter 'id' is required.");
                }

                var outputSchema = xmlParameters.GetValueOrDefault("outputSchema") ?? QueryParsingHelpers.GetQueryValue(query, "outputSchema") ?? Csw.NamespaceName;

                var record = catalog.GetRecord(id);

                if (record == null)
                {
                    return CreateExceptionReport("InvalidParameterValue", "id", $"Record with id '{id}' not found.");
                }

                // Get metadata from the registry passed as parameter
                var metadata = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

                var useIso19139 = outputSchema.Equals("http://www.isotc211.org/2005/gmd", StringComparison.OrdinalIgnoreCase);

                XElement recordElement;
                if (useIso19139)
                {
                    var layer = metadata.Layers.FirstOrDefault(l => l.Id == record.LayerId);
                    if (layer?.Iso19115 != null)
                    {
                        recordElement = Iso19139Mapper.CreateIso19139Metadata(record, layer, request);
                    }
                    else
                    {
                        // Fallback to Dublin Core if ISO 19115 metadata not available
                        recordElement = CreateDublinCoreRecord(record, request);
                    }
                }
                else
                {
                    recordElement = CreateDublinCoreRecord(record, request);
                }

                var responseRoot = new XElement(Csw + "GetRecordByIdResponse",
                    new XAttribute("version", "2.0.2"),
                    new XAttribute(XNamespace.Xmlns + "csw", Csw),
                    new XAttribute(XNamespace.Xmlns + "dc", Dc),
                    new XAttribute(XNamespace.Xmlns + "dct", Dct),
                    new XAttribute(XNamespace.Xmlns + "ows", Ows),
                    new XAttribute(XNamespace.Xmlns + "xsi", Xsi)
                );

                // Add ISO 19139 namespaces if needed
                if (useIso19139)
                {
                    responseRoot.Add(
                        new XAttribute(XNamespace.Xmlns + "gmd", Gmd),
                        new XAttribute(XNamespace.Xmlns + "gco", Gco),
                        new XAttribute(XNamespace.Xmlns + "gml", "http://www.opengis.net/gml")
                    );
                }

                responseRoot.Add(recordElement);

                var response = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    responseRoot
                );

                return Results.Content(response.ToString(), "application/xml; charset=utf-8");
            });
    }

    private static IResult HandleGetDomain(HttpRequest request)
    {
        var response = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Csw + "GetDomainResponse",
                new XAttribute("version", "2.0.2"),
                new XAttribute(XNamespace.Xmlns + "csw", Csw),
                new XElement(Csw + "DomainValues",
                    new XAttribute("type", "csw:Record"),
                    new XElement(Csw + "PropertyName", "dc:type"),
                    new XElement(Csw + "ListOfValues",
                        new XElement(Csw + "Value", "dataset"),
                        new XElement(Csw + "Value", "service")
                    )
                )
            )
        );

        return Results.Content(response.ToString(), "application/xml; charset=utf-8");
    }

    private static XElement CreateDublinCoreRecord(CatalogDiscoveryRecord record, HttpRequest request)
    {
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";

        return new XElement(Csw + "Record",
            new XElement(Dc + "identifier", record.Id),
            new XElement(Dc + "title", record.Title),
            new XElement(Dc + "type", record.ServiceType ?? "dataset"),
            new XElement(Dct + "abstract", record.Summary ?? ""),
            record.SpatialExtent?.Bbox != null && record.SpatialExtent.Bbox.Count > 0
                ? new XElement(Ows + "BoundingBox",
                    new XAttribute("crs", "urn:ogc:def:crs:EPSG::4326"),
                    new XElement(Ows + "LowerCorner", $"{record.SpatialExtent.Bbox[0][1]} {record.SpatialExtent.Bbox[0][0]}"),
                    new XElement(Ows + "UpperCorner", $"{record.SpatialExtent.Bbox[0][3]} {record.SpatialExtent.Bbox[0][2]}")
                )
                : null,
            new XElement(Dct + "references", $"{baseUrl}/catalog/{record.GroupId}/services/{record.ServiceId}")
        );
    }

    private static IResult HandleTransaction(HttpRequest request, Dictionary<string, string> parameters)
    {
        // CSW Transaction (Insert, Update, Delete) is an optional operation
        // Honua stores metadata in YAML/JSON files that are loaded at startup
        // Supporting Transaction would require:
        // 1. A metadata persistence service that can write to YAML/JSON files
        // 2. Validation of incoming metadata records (ISO 19139 or Dublin Core)
        // 3. Conflict resolution and versioning
        // 4. Trigger metadata reload after successful transaction
        //
        // For now, return OperationNotSupported per CSW 2.0.2 specification
        // Users should edit metadata files directly or use the Admin API for metadata management

        return CreateExceptionReport(
            "OperationNotSupported",
            "request",
            "CSW Transaction operation is not currently supported. " +
            "Please edit metadata files directly (YAML/JSON) or use the Admin API to reload metadata. " +
            "For programmatic catalog management, consider using the STAC API which supports write operations.");
    }

    private static IResult CreateExceptionReport(string exceptionCode, string? locator, string exceptionText)
    {
        var exception = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ows + "ExceptionReport",
                new XAttribute("version", "1.0.0"),
                new XAttribute(XNamespace.Xmlns + "ows", Ows),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(Xsi + "schemaLocation", "http://www.opengis.net/ows http://schemas.opengis.net/ows/1.0.0/owsExceptionReport.xsd"),
                new XElement(Ows + "Exception",
                    new XAttribute("exceptionCode", exceptionCode),
                    locator != null ? new XAttribute("locator", locator) : null,
                    new XElement(Ows + "ExceptionText", exceptionText)
                )
            )
        );

        return Results.Content(exception.ToString(), "application/xml; charset=utf-8", statusCode: 400);
    }
}
