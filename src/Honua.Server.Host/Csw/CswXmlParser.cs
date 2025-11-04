// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Xml.Linq;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Csw;

/// <summary>
/// Parses CSW XML POST requests and extracts request parameters.
/// </summary>
internal static class CswXmlParser
{
    private static readonly XNamespace Csw = "http://www.opengis.net/cat/csw/2.0.2";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";

    public static async Task<CswRequest?> ParsePostRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentType == null || !request.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            // Validate stream size before processing to prevent DoS
            SecureXmlSettings.ValidateStreamSize(request.Body);

            // Use secure XML parsing to prevent XXE attacks
            var doc = await SecureXmlSettings.LoadSecureAsync(request.Body, LoadOptions.None, cancellationToken);
            var root = doc.Root;

            if (root == null)
            {
                return null;
            }

            // Extract service and version attributes
            var service = root.Attribute("service")?.Value ?? "CSW";
            var version = root.Attribute("version")?.Value;

            var requestType = root.Name.LocalName;

            return requestType switch
            {
                "GetCapabilities" => ParseGetCapabilities(root, service, version),
                "DescribeRecord" => ParseDescribeRecord(root, service, version),
                "GetRecords" => ParseGetRecords(root, service, version),
                "GetRecordById" => ParseGetRecordById(root, service, version),
                "GetDomain" => ParseGetDomain(root, service, version),
                _ => new CswRequest { Service = service, Request = requestType }
            };
        }
        catch
        {
            return null;
        }
    }

    private static CswRequest ParseGetCapabilities(XElement root, string service, string? version)
    {
        return new CswRequest
        {
            Service = service,
            Request = "GetCapabilities",
            Version = version
        };
    }

    private static CswRequest ParseDescribeRecord(XElement root, string service, string? version)
    {
        var typeName = root.Element(Csw + "TypeName")?.Value;

        return new CswRequest
        {
            Service = service,
            Request = "DescribeRecord",
            Version = version,
            Parameters = new Dictionary<string, string>
            {
                ["typeName"] = typeName ?? "csw:Record"
            }
        };
    }

    private static CswRequest ParseGetRecords(XElement root, string service, string? version)
    {
        var resultType = root.Attribute("resultType")?.Value ?? "results";
        var outputSchema = root.Attribute("outputSchema")?.Value ?? Csw.NamespaceName;
        var startPosition = root.Attribute("startPosition")?.Value ?? "1";
        var maxRecords = root.Attribute("maxRecords")?.Value ?? "10";

        var parameters = new Dictionary<string, string>
        {
            ["resultType"] = resultType,
            ["outputSchema"] = outputSchema,
            ["startPosition"] = startPosition,
            ["maxRecords"] = maxRecords
        };

        // Extract query if present
        var query = root.Descendants(Csw + "Query").FirstOrDefault();
        if (query != null)
        {
            // Extract constraint/filter
            var constraint = query.Element(Csw + "Constraint");
            if (constraint != null)
            {
                var filter = constraint.Element(Ogc + "Filter");
                if (filter != null)
                {
                    // Extract PropertyIsLike for simple text search
                    var propertyIsLike = filter.Descendants(Ogc + "PropertyIsLike").FirstOrDefault();
                    if (propertyIsLike != null)
                    {
                        var literal = propertyIsLike.Element(Ogc + "Literal")?.Value;
                        if (literal.HasValue())
                        {
                            parameters["q"] = literal;
                        }
                    }
                }
            }
        }

        return new CswRequest
        {
            Service = service,
            Request = "GetRecords",
            Version = version,
            Parameters = parameters
        };
    }

    private static CswRequest ParseGetRecordById(XElement root, string service, string? version)
    {
        var id = root.Element(Csw + "Id")?.Value;
        var outputSchema = root.Attribute("outputSchema")?.Value ?? Csw.NamespaceName;

        return new CswRequest
        {
            Service = service,
            Request = "GetRecordById",
            Version = version,
            Parameters = new Dictionary<string, string>
            {
                ["id"] = id ?? "",
                ["outputSchema"] = outputSchema
            }
        };
    }

    private static CswRequest ParseGetDomain(XElement root, string service, string? version)
    {
        var propertyName = root.Element(Csw + "PropertyName")?.Value;
        var parameterName = root.Element(Csw + "ParameterName")?.Value;

        return new CswRequest
        {
            Service = service,
            Request = "GetDomain",
            Version = version,
            Parameters = new Dictionary<string, string>
            {
                ["propertyName"] = propertyName ?? "",
                ["parameterName"] = parameterName ?? ""
            }
        };
    }
}

/// <summary>
/// Represents a parsed CSW request.
/// </summary>
internal sealed class CswRequest
{
    public required string Service { get; init; }
    public required string Request { get; init; }
    public string? Version { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
}
