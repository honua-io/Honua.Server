// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Formats features as JSON-LD following the JSON-LD 1.1 specification.
/// </summary>
public static class JsonLdFeatureFormatter
{
    private const string OgcFeaturesContext = "http://www.opengis.net/def/ont/geosparql/1.0";
    private const string GeoSparqlNamespace = "http://www.opengis.net/ont/geosparql#";
    private const string SchemaOrgNamespace = "http://schema.org/";
    private const string DcTermsNamespace = "http://purl.org/dc/terms/";

    /// <summary>
    /// Creates a JSON-LD context for OGC Features.
    /// </summary>
    public static JsonObject CreateContext(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var context = new JsonObject
        {
            ["@vocab"] = SchemaOrgNamespace,
            ["geosparql"] = GeoSparqlNamespace,
            ["dcterms"] = DcTermsNamespace,
            ["geometry"] = new JsonObject
            {
                ["@id"] = "geosparql:hasGeometry",
                ["@type"] = "@id"
            },
            ["properties"] = new JsonObject
            {
                ["@id"] = "geosparql:hasProperties",
                ["@type"] = "@id"
            },
            ["id"] = new JsonObject
            {
                ["@id"] = "@id",
                ["@type"] = "@id"
            },
            ["type"] = "@type"
        };

        // Add layer-specific field mappings if available
        if (layer.Fields != null && layer.Fields.Count > 0)
        {
            foreach (var field in layer.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Name))
                {
                    var fieldContext = new JsonObject
                    {
                        ["@id"] = $"geosparql:{field.Name}"
                    };

                    // Map field types to JSON-LD types
                    if (!string.IsNullOrWhiteSpace(field.DataType))
                    {
                        var xsdType = MapFieldTypeToXsd(field.DataType);
                        if (!string.IsNullOrWhiteSpace(xsdType))
                        {
                            fieldContext["@type"] = xsdType;
                        }
                    }

                    context[field.Name] = fieldContext;
                }
            }
        }

        return context;
    }

    /// <summary>
    /// Converts a feature to JSON-LD format.
    /// </summary>
    public static JsonObject ToJsonLdFeature(
        string baseUri,
        string collectionId,
        LayerDefinition layer,
        object feature,
        JsonObject? contextOverride = null)
    {
        Guard.NotNull(baseUri);
        Guard.NotNull(collectionId);
        Guard.NotNull(layer);
        Guard.NotNull(feature);

        var featureJson = JsonSerializer.SerializeToNode(feature)?.AsObject();
        if (featureJson == null)
        {
            throw new InvalidOperationException("Failed to serialize feature to JSON");
        }

        var jsonLd = new JsonObject();

        // Add context
        if (contextOverride != null)
        {
            jsonLd["@context"] = contextOverride;
        }
        else
        {
            jsonLd["@context"] = CreateContext(layer);
        }

        // Add @type
        jsonLd["@type"] = "geosparql:Feature";

        // Add @id (feature URI)
        if (featureJson.TryGetPropertyValue("id", out var idNode) && idNode != null)
        {
            var featureId = idNode.ToString();
            jsonLd["@id"] = $"{baseUri}/ogc/collections/{collectionId}/items/{featureId}";
        }

        // Copy geometry
        if (featureJson.TryGetPropertyValue("geometry", out var geometryNode) && geometryNode != null)
        {
            jsonLd["geometry"] = geometryNode.DeepClone();
        }

        // Copy properties with semantic annotations
        if (featureJson.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject propsObj)
        {
            var semanticProps = new JsonObject();
            foreach (var prop in propsObj)
            {
                semanticProps[prop.Key] = prop.Value?.DeepClone();
            }
            jsonLd["properties"] = semanticProps;
        }

        // Copy links if present
        if (featureJson.TryGetPropertyValue("links", out var linksNode) && linksNode != null)
        {
            jsonLd["links"] = linksNode.DeepClone();
        }

        return jsonLd;
    }

    /// <summary>
    /// Converts a feature collection to JSON-LD format.
    /// </summary>
    public static JsonObject ToJsonLdFeatureCollection(
        string baseUri,
        string collectionId,
        LayerDefinition layer,
        IEnumerable<object> features,
        long numberMatched,
        long numberReturned,
        object? links = null)
    {
        Guard.NotNull(baseUri);
        Guard.NotNull(collectionId);
        Guard.NotNull(layer);
        Guard.NotNull(features);

        var context = CreateContext(layer);
        var jsonLd = new JsonObject
        {
            ["@context"] = context,
            ["@type"] = "geosparql:FeatureCollection",
            ["@id"] = $"{baseUri}/ogc/collections/{collectionId}/items"
        };

        // Convert features
        var featureArray = new JsonArray();
        foreach (var feature in features)
        {
            var jsonLdFeature = ToJsonLdFeature(baseUri, collectionId, layer, feature, null);
            // Remove duplicate @context from individual features when in a collection
            jsonLdFeature.Remove("@context");
            featureArray.Add(jsonLdFeature);
        }

        jsonLd["features"] = featureArray;
        jsonLd["numberMatched"] = numberMatched;
        jsonLd["numberReturned"] = numberReturned;

        if (links != null)
        {
            jsonLd["links"] = JsonSerializer.SerializeToNode(links);
        }

        return jsonLd;
    }

    /// <summary>
    /// Serializes a JSON-LD object to a string.
    /// </summary>
    public static string Serialize(JsonObject jsonLd)
    {
        Guard.NotNull(jsonLd);

        return JsonSerializer.Serialize(jsonLd, JsonSerializerOptionsRegistry.Web);
    }

    private static string? MapFieldTypeToXsd(string fieldType)
    {
        return fieldType.ToLowerInvariant() switch
        {
            "string" or "esrifieldtypestring" => "http://www.w3.org/2001/XMLSchema#string",
            "integer" or "esrifieldtypeinteger" or "esrifieldtypesmallinteger" => "http://www.w3.org/2001/XMLSchema#integer",
            "double" or "esrifieldtypedouble" or "esrifieldtypesingle" => "http://www.w3.org/2001/XMLSchema#double",
            "date" or "esrifieldtypedate" => "http://www.w3.org/2001/XMLSchema#dateTime",
            "boolean" or "esrifieldtypeboolean" => "http://www.w3.org/2001/XMLSchema#boolean",
            "guid" or "esrifieldtypeguid" or "esrifieldtypeglobalid" => "http://www.w3.org/2001/XMLSchema#string",
            _ => null
        };
    }
}
