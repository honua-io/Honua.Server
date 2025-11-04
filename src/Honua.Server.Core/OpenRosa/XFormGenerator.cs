// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// Generates XForms XML from Honua layer metadata.
/// </summary>
public interface IXFormGenerator
{
    XForm Generate(LayerDefinition layer, string baseUrl);
}

public sealed class XFormGenerator : IXFormGenerator
{
    private static readonly XNamespace H = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace XFORMS = "http://www.w3.org/2002/xforms";
    private static readonly XNamespace EV = "http://www.w3.org/2001/xml-events";
    private static readonly XNamespace JAVAROSA = "http://openrosa.org/javarosa";
    private static readonly XNamespace ODATA = "http://www.opendatakit.org/xforms";

    public XForm Generate(LayerDefinition layer, string baseUrl)
    {
        if (layer.OpenRosa is not { Enabled: true })
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not have OpenRosa enabled.");
        }

        var openrosa = layer.OpenRosa;
        var formId = openrosa.FormId ?? $"{layer.ServiceId}_{layer.Id}";
        var formTitle = openrosa.FormTitle ?? layer.Title;
        var version = openrosa.FormVersion;

        var instanceName = $"{formId}_instance";
        var modelId = $"{formId}_model";

        // Build XForm structure
        var xform = new XDocument(
            new XElement(H + "html",
                new XAttribute(XNamespace.Xmlns + "h", H),
                new XAttribute(XNamespace.Xmlns + "ev", EV),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "jr", JAVAROSA),
                new XAttribute(XNamespace.Xmlns + "odk", ODATA),

                // Head: Model + UI controls
                new XElement(H + "head",
                    new XElement(H + "title", formTitle),
                    BuildModel(layer, formId, version, instanceName, modelId)
                ),

                // Body: Form fields
                new XElement(H + "body",
                    BuildFormBody(layer, instanceName)
                )
            )
        );

        var xmlString = xform.ToString();
        var hash = ComputeMd5Hash(xmlString);

        return new XForm
        {
            FormId = formId,
            Version = version,
            Title = formTitle,
            LayerId = layer.Id,
            ServiceId = layer.ServiceId,
            Xml = xform,
            Hash = hash
        };
    }

    private XElement BuildModel(LayerDefinition layer, string formId, string version, string instanceName, string modelId)
    {
        var instance = new XElement("data",
            new XAttribute("id", formId),
            new XAttribute("version", version),

            // OpenRosa metadata
            new XElement("meta",
                new XElement("instanceID"),
                new XElement("instanceName"),
                new XElement("formDate", new XAttribute(JAVAROSA + "preload", "timestamp"), new XAttribute(JAVAROSA + "preloadParams", "start")),
                new XElement("deviceID", new XAttribute(JAVAROSA + "preload", "property"), new XAttribute(JAVAROSA + "preloadParams", "deviceid")),
                new XElement("userID", new XAttribute(JAVAROSA + "preload", "property"), new XAttribute(JAVAROSA + "preloadParams", "username")),
                new XElement("layerId", layer.Id),
                new XElement("serviceId", layer.ServiceId)
            ),

            // Geometry field
            BuildGeometryElement(layer),

            // Data fields
            layer.Fields
                .Where(f => !string.Equals(f.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
                .Where(f => !string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase))
                .Select(f => new XElement(SanitizeFieldName(f.Name)))
        );

        var binds = new List<XElement>
        {
            // Metadata binds
            new XElement(XFORMS + "bind", new XAttribute("nodeset", $"/data/meta/instanceID"), new XAttribute("type", "string"), new XAttribute("readonly", "true()"), new XAttribute("calculate", "concat('uuid:', uuid())")),
            new XElement(XFORMS + "bind", new XAttribute("nodeset", $"/data/meta/instanceName"), new XAttribute("type", "string"), new XAttribute("readonly", "true()"), new XAttribute("calculate", $"concat('{layer.Title} - ', /data/{SanitizeFieldName(layer.DisplayField ?? layer.IdField)})")),
            new XElement(XFORMS + "bind", new XAttribute("nodeset", $"/data/meta/layerId"), new XAttribute("type", "string"), new XAttribute("readonly", "true()")),
            new XElement(XFORMS + "bind", new XAttribute("nodeset", $"/data/meta/serviceId"), new XAttribute("type", "string"), new XAttribute("readonly", "true()"))
        };

        // Geometry bind
        binds.Add(BuildGeometryBind(layer));

        // Field binds
        foreach (var field in layer.Fields.Where(f =>
            !string.Equals(f.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase)))
        {
            binds.Add(BuildFieldBind(layer, field));
        }

        return new XElement(XFORMS + "model",
            new XElement(XFORMS + "instance",
                new XAttribute("id", modelId),
                instance
            ),
            binds
        );
    }

    private XElement BuildGeometryElement(LayerDefinition layer)
    {
        var geometryType = layer.GeometryType.ToLowerInvariant();
        return geometryType switch
        {
            "point" or "multipoint" => new XElement(SanitizeFieldName(layer.GeometryField)),
            "linestring" or "multilinestring" => new XElement(SanitizeFieldName(layer.GeometryField)),
            "polygon" or "multipolygon" => new XElement(SanitizeFieldName(layer.GeometryField)),
            _ => new XElement(SanitizeFieldName(layer.GeometryField))
        };
    }

    private XElement BuildGeometryBind(LayerDefinition layer)
    {
        var geometryType = layer.GeometryType.ToLowerInvariant();
        var xformType = geometryType switch
        {
            "point" or "multipoint" => "geopoint",
            "linestring" or "multilinestring" => "geotrace",
            "polygon" or "multipolygon" => "geoshape",
            _ => "geopoint"
        };

        return new XElement(XFORMS + "bind",
            new XAttribute("nodeset", $"/data/{SanitizeFieldName(layer.GeometryField)}"),
            new XAttribute("type", xformType),
            new XAttribute("required", "true()")
        );
    }

    private XElement BuildFieldBind(LayerDefinition layer, FieldDefinition field)
    {
        var bind = new XElement(XFORMS + "bind",
            new XAttribute("nodeset", $"/data/{SanitizeFieldName(field.Name)}"),
            new XAttribute("type", MapDataTypeToXFormType(field.DataType ?? "string"))
        );

        // Check if OpenRosa metadata has custom configuration for this field
        if (layer.OpenRosa?.FieldMappings.TryGetValue(field.Name, out var mapping) == true)
        {
            if (mapping.Required || !field.Nullable)
            {
                bind.Add(new XAttribute("required", "true()"));
            }

            if (mapping.Constraint is not null)
            {
                bind.Add(new XAttribute("constraint", mapping.Constraint));
                if (mapping.ConstraintMessage is not null)
                {
                    bind.Add(new XAttribute(JAVAROSA + "constraintMsg", mapping.ConstraintMessage));
                }
            }

            if (mapping.ReadOnly)
            {
                bind.Add(new XAttribute("readonly", "true()"));
            }

            if (mapping.Relevant is not null)
            {
                bind.Add(new XAttribute("relevant", mapping.Relevant));
            }

            if (mapping.DefaultValue is not null)
            {
                bind.Add(new XAttribute("calculate", $"'{mapping.DefaultValue}'"));
            }
        }
        else if (!field.Nullable)
        {
            bind.Add(new XAttribute("required", "true()"));
        }

        return bind;
    }

    private IEnumerable<XElement> BuildFormBody(LayerDefinition layer, string instanceName)
    {
        yield return new XElement(H + "h1", layer.Title);

        if (!string.IsNullOrWhiteSpace(layer.Description))
        {
            yield return new XElement(H + "p", layer.Description);
        }

        // Geometry input
        yield return BuildGeometryInput(layer);

        // Field inputs
        foreach (var field in layer.Fields.Where(f =>
            !string.Equals(f.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase)))
        {
            yield return BuildFieldInput(layer, field);
        }
    }

    private XElement BuildGeometryInput(LayerDefinition layer)
    {
        var geometryType = layer.GeometryType.ToLowerInvariant();
        var label = geometryType switch
        {
            "point" or "multipoint" => "Location",
            "linestring" or "multilinestring" => "Line/Path",
            "polygon" or "multipolygon" => "Area/Polygon",
            _ => "Geometry"
        };

        return new XElement(H + "input",
            new XAttribute("ref", $"/data/{SanitizeFieldName(layer.GeometryField)}"),
            new XElement(H + "label", label),
            new XElement(H + "hint", "Tap to capture location")
        );
    }

    private XElement BuildFieldInput(LayerDefinition layer, FieldDefinition field)
    {
        OpenRosaFieldMappingDefinition? mapping = null;
        layer.OpenRosa?.FieldMappings.TryGetValue(field.Name, out mapping);

        var label = mapping?.Label ?? field.Alias ?? field.Name;
        var hint = mapping?.Hint;
        var appearance = mapping?.Appearance;
        var inputType = mapping?.Type ?? InferInputTypeFromDataType(field.DataType ?? "string");

        XElement input;

        if (mapping?.Choices is not null)
        {
            // Select field with choices
            input = new XElement(inputType == "select" ? H + "select" : H + "select1",
                new XAttribute("ref", $"/data/{SanitizeFieldName(field.Name)}"),
                new XElement(H + "label", label)
            );

            if (mapping.Choices is Dictionary<string, object> dict)
            {
                foreach (var choice in dict)
                {
                    input.Add(new XElement(H + "item",
                        new XElement(H + "label", choice.Value),
                        new XElement(H + "value", choice.Key)
                    ));
                }
            }
            else if (mapping.Choices is Dictionary<string, string> stringDict)
            {
                foreach (var choice in stringDict)
                {
                    input.Add(new XElement(H + "item",
                        new XElement(H + "label", choice.Value),
                        new XElement(H + "value", choice.Key)
                    ));
                }
            }
        }
        else if (inputType == "upload" || field.DataType == "binary")
        {
            input = new XElement(H + "upload",
                new XAttribute("ref", $"/data/{SanitizeFieldName(field.Name)}"),
                new XAttribute("mediatype", "image/*"),
                new XElement(H + "label", label)
            );
        }
        else
        {
            input = new XElement(H + "input",
                new XAttribute("ref", $"/data/{SanitizeFieldName(field.Name)}"),
                new XElement(H + "label", label)
            );
        }

        if (hint is not null)
        {
            input.Add(new XElement(H + "hint", hint));
        }

        if (appearance is not null)
        {
            input.Add(new XAttribute("appearance", appearance));
        }

        return input;
    }

    private static string MapDataTypeToXFormType(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" or "int32" or "int64" or "long" or "short" => "int",
            "double" or "float" or "decimal" or "number" => "decimal",
            "bool" or "boolean" => "boolean",
            "date" => "date",
            "datetime" or "timestamp" => "dateTime",
            "time" => "time",
            "binary" or "blob" => "binary",
            _ => "string"
        };
    }

    private static string InferInputTypeFromDataType(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" or "number" => "input",
            "date" => "input",
            "datetime" => "input",
            "binary" or "blob" => "upload",
            _ => "input"
        };
    }

    private static string SanitizeFieldName(string fieldName)
    {
        // XForms node names must start with a letter and contain only letters, digits, hyphens, underscores, periods
        var sanitized = fieldName
            .Replace(" ", "_")
            .Replace("-", "_")
            .ToLowerInvariant();

        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "field_" + sanitized;
        }

        return sanitized;
    }

    // MD5 is required by OpenRosa/ODK XForm specification for version hashing
    // Not used for cryptographic security, only for legacy protocol compliance
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
    private static string ComputeMd5Hash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
#pragma warning restore CA5351
}
