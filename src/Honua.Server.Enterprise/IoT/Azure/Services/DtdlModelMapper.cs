// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Honua.Server.Enterprise.IoT.Azure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Maps Honua layer schemas to Azure Digital Twins DTDL models.
/// </summary>
public interface IDtdlModelMapper
{
    /// <summary>
    /// Generates a DTDL model from a Honua layer schema.
    /// </summary>
    Task<DtdlModel> GenerateModelFromLayerAsync(
        string serviceId,
        string layerId,
        Dictionary<string, object> layerSchema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a DTDL model JSON string from a Honua layer schema.
    /// </summary>
    Task<string> GenerateModelJsonFromLayerAsync(
        string serviceId,
        string layerId,
        Dictionary<string, object> layerSchema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a DTDL model.
    /// </summary>
    Task<bool> ValidateModelAsync(string modelJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps Honua feature attributes to twin properties.
    /// </summary>
    Dictionary<string, object> MapFeatureToTwinProperties(
        Dictionary<string, object?> featureAttributes,
        LayerModelMapping mapping);

    /// <summary>
    /// Maps twin properties back to Honua feature attributes.
    /// </summary>
    Dictionary<string, object?> MapTwinToFeatureProperties(
        Dictionary<string, object> twinProperties,
        LayerModelMapping mapping);
}

/// <summary>
/// Implementation of DTDL model mapper.
/// </summary>
public sealed class DtdlModelMapper : IDtdlModelMapper
{
    private readonly ILogger<DtdlModelMapper> _logger;
    private readonly AzureDigitalTwinsOptions _options;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DtdlModelMapper(
        ILogger<DtdlModelMapper> logger,
        IOptions<AzureDigitalTwinsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<DtdlModel> GenerateModelFromLayerAsync(
        string serviceId,
        string layerId,
        Dictionary<string, object> layerSchema,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Placeholder for async operations

        var modelId = GenerateModelId(serviceId, layerId);
        var displayName = layerSchema.TryGetValue("title", out var title)
            ? title?.ToString()
            : $"{serviceId}.{layerId}";

        var model = new DtdlModel
        {
            Id = modelId,
            Context = _options.UseNgsiLdOntology
                ? "dtmi:dtdl:context;3"
                : "dtmi:dtdl:context;3",
            Type = "Interface",
            DisplayName = displayName,
            Description = layerSchema.TryGetValue("description", out var desc)
                ? desc?.ToString()
                : $"Digital Twin model for Honua layer {serviceId}/{layerId}",
            Contents = new List<DtdlContent>()
        };

        // Add base NGSI-LD properties if enabled
        if (_options.UseNgsiLdOntology)
        {
            model.Contents.Add(new DtdlProperty
            {
                Type = "Property",
                Name = "type",
                DisplayName = "Entity Type",
                Schema = DtdlSchemaType.String,
                Description = "NGSI-LD entity type"
            });

            model.Contents.Add(new DtdlProperty
            {
                Type = "Property",
                Name = "location",
                DisplayName = "Location",
                Schema = new Dictionary<string, object>
                {
                    ["@type"] = "Object",
                    ["fields"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["name"] = "type",
                            ["schema"] = DtdlSchemaType.String
                        },
                        new Dictionary<string, object>
                        {
                            ["name"] = "coordinates",
                            ["schema"] = "Object"
                        }
                    }
                },
                Description = "GeoJSON location"
            });
        }

        // Map layer properties/fields to DTDL properties
        if (layerSchema.TryGetValue("properties", out var propsObj)
            && propsObj is Dictionary<string, object> properties)
        {
            foreach (var (propName, propDef) in properties)
            {
                if (propDef is not Dictionary<string, object> propDefDict)
                    continue;

                var dtdlProperty = MapPropertyToDtdl(propName, propDefDict);
                if (dtdlProperty != null)
                {
                    model.Contents.Add(dtdlProperty);
                }
            }
        }

        // Add geometry property if not already added
        if (!model.Contents.Any(c => c.Name == "geometry"))
        {
            model.Contents.Add(new DtdlProperty
            {
                Type = "Property",
                Name = "geometry",
                DisplayName = "Geometry",
                Schema = "string", // GeoJSON as string for complex geometries
                Description = "Feature geometry in GeoJSON format"
            });
        }

        // Add sync metadata properties
        AddSyncMetadataProperties(model);

        _logger.LogInformation(
            "Generated DTDL model {ModelId} for layer {ServiceId}/{LayerId} with {PropertyCount} properties",
            modelId, serviceId, layerId, model.Contents.Count);

        return model;
    }

    public async Task<string> GenerateModelJsonFromLayerAsync(
        string serviceId,
        string layerId,
        Dictionary<string, object> layerSchema,
        CancellationToken cancellationToken = default)
    {
        var model = await GenerateModelFromLayerAsync(serviceId, layerId, layerSchema, cancellationToken);
        return JsonSerializer.Serialize(model, _jsonOptions);
    }

    public Task<bool> ValidateModelAsync(string modelJson, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic validation - parse as JSON and check required fields
            using var doc = JsonDocument.Parse(modelJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("@id", out _))
            {
                _logger.LogWarning("DTDL model validation failed: Missing @id");
                return Task.FromResult(false);
            }

            if (!root.TryGetProperty("@type", out var typeElement) || typeElement.GetString() != "Interface")
            {
                _logger.LogWarning("DTDL model validation failed: Invalid @type");
                return Task.FromResult(false);
            }

            if (!root.TryGetProperty("@context", out _))
            {
                _logger.LogWarning("DTDL model validation failed: Missing @context");
                return Task.FromResult(false);
            }

            _logger.LogDebug("DTDL model validation succeeded");
            return Task.FromResult(true);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "DTDL model validation failed: Invalid JSON");
            return Task.FromResult(false);
        }
    }

    public Dictionary<string, object> MapFeatureToTwinProperties(
        Dictionary<string, object?> featureAttributes,
        LayerModelMapping mapping)
    {
        var twinProperties = new Dictionary<string, object>();

        foreach (var (attrName, attrValue) in featureAttributes)
        {
            if (attrValue == null)
                continue;

            // Check if there's a custom mapping
            var targetName = mapping.PropertyMappings.TryGetValue(attrName, out var mapped)
                ? mapped
                : SanitizePropertyName(attrName);

            twinProperties[targetName] = attrValue;
        }

        return twinProperties;
    }

    public Dictionary<string, object?> MapTwinToFeatureProperties(
        Dictionary<string, object> twinProperties,
        LayerModelMapping mapping)
    {
        var featureAttributes = new Dictionary<string, object?>();

        // Create reverse mapping
        var reverseMapping = mapping.PropertyMappings
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        foreach (var (propName, propValue) in twinProperties)
        {
            // Skip metadata properties
            if (propName.StartsWith("$"))
                continue;

            var targetName = reverseMapping.TryGetValue(propName, out var mapped)
                ? mapped
                : propName;

            featureAttributes[targetName] = propValue;
        }

        return featureAttributes;
    }

    private string GenerateModelId(string serviceId, string layerId)
    {
        var sanitizedService = SanitizeDtmiComponent(serviceId);
        var sanitizedLayer = SanitizeDtmiComponent(layerId);
        return $"{_options.DefaultNamespace}:{sanitizedService}:{sanitizedLayer};1";
    }

    private static string SanitizeDtmiComponent(string input)
    {
        // DTMI components must match: [A-Za-z](?:[A-Za-z0-9_]*[A-Za-z0-9])?
        var sanitized = Regex.Replace(input, @"[^A-Za-z0-9_]", "_");
        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }
        return sanitized;
    }

    private static string SanitizePropertyName(string input)
    {
        // Property names must match: [A-Za-z_][A-Za-z0-9_]*
        var sanitized = Regex.Replace(input, @"[^A-Za-z0-9_]", "_");
        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }
        return sanitized;
    }

    private DtdlProperty? MapPropertyToDtdl(string name, Dictionary<string, object> definition)
    {
        if (!definition.TryGetValue("type", out var typeObj))
            return null;

        var type = typeObj?.ToString()?.ToLowerInvariant();
        var schema = type switch
        {
            "string" or "text" => DtdlSchemaType.String,
            "integer" or "int" or "int32" => DtdlSchemaType.Integer,
            "long" or "int64" => DtdlSchemaType.Long,
            "double" or "float64" => DtdlSchemaType.Double,
            "float" or "float32" => DtdlSchemaType.Float,
            "boolean" or "bool" => DtdlSchemaType.Boolean,
            "date" => DtdlSchemaType.Date,
            "datetime" or "timestamp" => DtdlSchemaType.DateTime,
            "time" => DtdlSchemaType.Time,
            "point" => DtdlSchemaType.Point,
            "linestring" => DtdlSchemaType.LineString,
            "polygon" => DtdlSchemaType.Polygon,
            "multipoint" => DtdlSchemaType.MultiPoint,
            "multilinestring" => DtdlSchemaType.MultiLineString,
            "multipolygon" => DtdlSchemaType.MultiPolygon,
            _ => (object)DtdlSchemaType.String // Default to string for unknown types
        };

        return new DtdlProperty
        {
            Type = "Property",
            Name = SanitizePropertyName(name),
            DisplayName = definition.TryGetValue("title", out var title) ? title?.ToString() : name,
            Description = definition.TryGetValue("description", out var desc) ? desc?.ToString() : null,
            Schema = schema,
            Writable = true
        };
    }

    private void AddSyncMetadataProperties(DtdlModel model)
    {
        model.Contents.Add(new DtdlProperty
        {
            Type = "Property",
            Name = "honuaServiceId",
            DisplayName = "Honua Service ID",
            Schema = DtdlSchemaType.String,
            Description = "Source Honua service identifier"
        });

        model.Contents.Add(new DtdlProperty
        {
            Type = "Property",
            Name = "honuaLayerId",
            DisplayName = "Honua Layer ID",
            Schema = DtdlSchemaType.String,
            Description = "Source Honua layer identifier"
        });

        model.Contents.Add(new DtdlProperty
        {
            Type = "Property",
            Name = "honuaFeatureId",
            DisplayName = "Honua Feature ID",
            Schema = DtdlSchemaType.String,
            Description = "Source Honua feature identifier"
        });

        model.Contents.Add(new DtdlProperty
        {
            Type = "Property",
            Name = "lastSyncTime",
            DisplayName = "Last Sync Time",
            Schema = DtdlSchemaType.DateTime,
            Description = "Last synchronization timestamp"
        });

        model.Contents.Add(new DtdlProperty
        {
            Type = "Property",
            Name = "syncVersion",
            DisplayName = "Sync Version",
            Schema = DtdlSchemaType.Long,
            Description = "Version number for conflict resolution"
        });
    }
}
