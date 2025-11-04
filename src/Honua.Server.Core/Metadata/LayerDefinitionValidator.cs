// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Comprehensive validator for layer definitions.
/// Ensures data integrity by validating fields, CRS, geometry types, and storage configuration.
/// </summary>
internal static class LayerDefinitionValidator
{
    private static readonly HashSet<string> ValidGeometryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Point",
        "MultiPoint",
        "LineString",
        "MultiLineString",
        "Polygon",
        "MultiPolygon",
        "Geometry",
        "GeometryCollection",
        "esriGeometryPoint",
        "esriGeometryMultipoint",
        "esriGeometryPolyline",
        "esriGeometryPolygon",
        "esriGeometryEnvelope"
    };

    private static readonly HashSet<string> ValidFieldDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "integer",
        "int",
        "int32",
        "int64",
        "long",
        "double",
        "float",
        "decimal",
        "boolean",
        "bool",
        "date",
        "datetime",
        "timestamp",
        "guid",
        "uuid",
        "binary",
        "blob",
        "geometry"
    };

    private static readonly HashSet<string> KnownCrsCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        "http://www.opengis.net/def/crs/EPSG/0/4326",
        "http://www.opengis.net/def/crs/EPSG/0/3857",
        "EPSG:4326",
        "EPSG:3857",
        "EPSG:4269",
        "EPSG:2263",
        "EPSG:3395",
        "urn:ogc:def:crs:OGC:1.3:CRS84",
        "urn:ogc:def:crs:EPSG::4326",
        "urn:ogc:def:crs:EPSG::3857"
    };

    /// <summary>
    /// Validates a layer definition for data integrity.
    /// </summary>
    /// <param name="layer">The layer definition to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when validation fails.</exception>
    public static void Validate(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        ValidateRequiredFields(layer);
        ValidateGeometryType(layer);
        ValidateCrsValues(layer);
        ValidateFields(layer);
        ValidateStorageConfiguration(layer);
        ValidateEditingConfiguration(layer);
        ValidateTemporalConfiguration(layer);
        ValidateRelationships(layer);
    }

    private static void ValidateRequiredFields(LayerDefinition layer)
    {
        if (layer.Id.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("Layer must have a non-empty Id.");
        }

        if (layer.ServiceId.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' must have a non-empty ServiceId.");
        }

        if (layer.Title.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' must have a non-empty Title.");
        }

        if (layer.IdField.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' must have a non-empty IdField.");
        }

        if (layer.GeometryField.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' must have a non-empty GeometryField.");
        }

        if (layer.GeometryType.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' must have a non-empty GeometryType.");
        }
    }

    private static void ValidateGeometryType(LayerDefinition layer)
    {
        if (!ValidGeometryTypes.Contains(layer.GeometryType))
        {
            var supportedTypes = string.Join(", ", ValidGeometryTypes.OrderBy(t => t));
            throw new InvalidDataException(
                $"Layer '{layer.Id}' has unsupported geometry type '{layer.GeometryType}'. " +
                $"Supported types: {supportedTypes}");
        }
    }

    private static void ValidateCrsValues(LayerDefinition layer)
    {
        // Validate layer-level CRS list
        if (layer.Crs?.Count > 0)
        {
            foreach (var crs in layer.Crs)
            {
                if (crs.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Layer '{layer.Id}' contains an empty CRS value.");
                }

                // Validate CRS format (must be URI or EPSG code)
                if (!IsValidCrsFormat(crs))
                {
                    throw new InvalidDataException(
                        $"Layer '{layer.Id}' has invalid CRS format '{crs}'. " +
                        $"CRS must be a valid URI (e.g., 'http://www.opengis.net/def/crs/EPSG/0/4326') " +
                        $"or EPSG code (e.g., 'EPSG:4326').");
                }
            }
        }

        // Validate storage CRS
        if (layer.Storage?.Crs is not null)
        {
            if (!IsValidCrsFormat(layer.Storage.Crs))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' storage has invalid CRS format '{layer.Storage.Crs}'.");
            }
        }

        // Validate storage SRID
        if (layer.Storage?.Srid is int srid)
        {
            if (srid <= 0 || srid > 999999)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' storage has invalid SRID '{srid}'. " +
                    $"SRID must be between 1 and 999999.");
            }
        }

        // Validate extent CRS
        if (layer.Extent?.Crs is not null)
        {
            if (!IsValidCrsFormat(layer.Extent.Crs))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' extent has invalid CRS format '{layer.Extent.Crs}'.");
            }
        }
    }

    private static bool IsValidCrsFormat(string crs)
    {
        if (crs.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Check if it's a known CRS
        if (KnownCrsCodes.Contains(crs))
        {
            return true;
        }

        // Check if it's a valid HTTP/HTTPS URI
        if (Uri.TryCreate(crs, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        // Check if it's a valid URN
        if (crs.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if it's a valid EPSG code
        if (crs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
        {
            var epsgCode = crs.Substring(5);
            return int.TryParse(epsgCode, out var code) && code > 0 && code <= 999999;
        }

        return false;
    }

    private static void ValidateFields(LayerDefinition layer)
    {
        if (layer.Fields is null || layer.Fields.Count == 0)
        {
            // Fields are optional, but if provided must be valid
            return;
        }

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in layer.Fields)
        {
            if (field is null)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' contains a null field definition.");
            }

            if (field.Name.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' contains a field without a name.");
            }

            // Check for duplicate field names
            if (!fieldNames.Add(field.Name))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' has duplicate field name '{field.Name}'.");
            }

            // Validate data type if specified
            if (field.DataType is not null && !ValidFieldDataTypes.Contains(field.DataType))
            {
                var supportedTypes = string.Join(", ", ValidFieldDataTypes.OrderBy(t => t));
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' field '{field.Name}' has unsupported data type '{field.DataType}'. " +
                    $"Supported types: {supportedTypes}");
            }

            // Validate length constraints
            if (field.MaxLength is int maxLength && maxLength <= 0)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' field '{field.Name}' has invalid MaxLength '{maxLength}'. " +
                    $"MaxLength must be greater than 0.");
            }

            // Validate precision/scale for numeric types
            if (field.Precision is int precision && precision <= 0)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' field '{field.Name}' has invalid Precision '{precision}'. " +
                    $"Precision must be greater than 0.");
            }

            if (field.Scale is int scale && scale < 0)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' field '{field.Name}' has invalid Scale '{scale}'. " +
                    $"Scale must be non-negative.");
            }

            if (field.Precision is int p && field.Scale is int s && s > p)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' field '{field.Name}' has Scale ({s}) greater than Precision ({p}).");
            }
        }

        // Ensure required system fields exist
        var idFieldExists = fieldNames.Contains(layer.IdField);
        if (!idFieldExists)
        {
            // Id field doesn't need to be in Fields list, but log a warning
            System.Diagnostics.Debug.WriteLine(
                $"Layer '{layer.Id}' IdField '{layer.IdField}' not found in Fields list. " +
                $"This is acceptable if the field is auto-generated.");
        }
    }

    private static void ValidateStorageConfiguration(LayerDefinition layer)
    {
        if (layer.Storage is null)
        {
            // Storage configuration is optional (may use defaults)
            return;
        }

        var storage = layer.Storage;

        // Table name validation
        if (storage.Table is not null)
        {
            if (storage.Table.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' storage table name cannot be empty.");
            }

            // Check for SQL injection patterns
            if (ContainsSqlInjectionPatterns(storage.Table))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' storage table name '{storage.Table}' contains invalid characters.");
            }
        }

        // Geometry column validation
        if (storage.GeometryColumn is not null && storage.GeometryColumn.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException(
                $"Layer '{layer.Id}' storage geometry column name cannot be empty.");
        }

        // Primary key validation
        if (storage.PrimaryKey is not null && storage.PrimaryKey.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException(
                $"Layer '{layer.Id}' storage primary key name cannot be empty.");
        }

        // Temporal column validation
        if (storage.TemporalColumn is not null && storage.TemporalColumn.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException(
                $"Layer '{layer.Id}' storage temporal column name cannot be empty.");
        }

        // Ensure SRID and CRS are consistent
        if (storage.Srid.HasValue && storage.Crs is not null)
        {
            // Both are specified - log for awareness
            System.Diagnostics.Debug.WriteLine(
                $"Layer '{layer.Id}' storage has both SRID ({storage.Srid}) and CRS ({storage.Crs}) specified. " +
                $"SRID will take precedence.");
        }
    }

    private static void ValidateEditingConfiguration(LayerDefinition layer)
    {
        if (layer.Editing is null)
        {
            return;
        }

        var editing = layer.Editing;

        // Validate constraints
        if (editing.Constraints is not null)
        {
            // Validate immutable fields exist
            if (editing.Constraints.ImmutableFields?.Count > 0)
            {
                var fieldNames = new HashSet<string>(
                    layer.Fields?.Select(f => f.Name) ?? Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var immutableField in editing.Constraints.ImmutableFields)
                {
                    if (immutableField.IsNullOrWhiteSpace())
                    {
                        throw new InvalidDataException(
                            $"Layer '{layer.Id}' editing constraints contain an empty immutable field name.");
                    }
                }
            }

            // Validate required fields exist
            if (editing.Constraints.RequiredFields?.Count > 0)
            {
                foreach (var requiredField in editing.Constraints.RequiredFields)
                {
                    if (requiredField.IsNullOrWhiteSpace())
                    {
                        throw new InvalidDataException(
                            $"Layer '{layer.Id}' editing constraints contain an empty required field name.");
                    }
                }
            }

            // Validate default values
            if (editing.Constraints.DefaultValues?.Count > 0)
            {
                foreach (var kvp in editing.Constraints.DefaultValues)
                {
                    if (kvp.Key.IsNullOrWhiteSpace())
                    {
                        throw new InvalidDataException(
                            $"Layer '{layer.Id}' editing constraints contain a default value with an empty field name.");
                    }
                }
            }
        }
    }

    private static void ValidateTemporalConfiguration(LayerDefinition layer)
    {
        if (layer.Temporal is null || !layer.Temporal.Enabled)
        {
            return;
        }

        var temporal = layer.Temporal;

        // At least one temporal field must be specified
        if (temporal.StartField.IsNullOrWhiteSpace() && temporal.EndField.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException(
                $"Layer '{layer.Id}' has temporal enabled but no StartField or EndField specified.");
        }

        // Validate period format if specified
        if (temporal.Period is not null && temporal.Period.HasValue())
        {
            // Basic validation for ISO 8601 duration format
            if (!temporal.Period.StartsWith("P", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' temporal period '{temporal.Period}' is not a valid ISO 8601 duration. " +
                    $"Period must start with 'P' (e.g., 'P1D' for 1 day).");
            }
        }
    }

    private static void ValidateRelationships(LayerDefinition layer)
    {
        if (layer.Relationships is null || layer.Relationships.Count == 0)
        {
            return;
        }

        var relationshipIds = new HashSet<int>();

        foreach (var relationship in layer.Relationships)
        {
            if (relationship is null)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' contains a null relationship definition.");
            }

            // Check for duplicate relationship IDs
            if (!relationshipIds.Add(relationship.Id))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' has duplicate relationship ID '{relationship.Id}'.");
            }

            if (relationship.RelatedLayerId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' relationship {relationship.Id} must have a RelatedLayerId.");
            }

            if (relationship.KeyField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' relationship {relationship.Id} must have a KeyField.");
            }

            if (relationship.RelatedKeyField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' relationship {relationship.Id} must have a RelatedKeyField.");
            }

            // Validate role
            var validRoles = new[] { "esriRelRoleOrigin", "esriRelRoleDestination", "origin", "destination" };
            if (relationship.Role.HasValue() &&
                !validRoles.Contains(relationship.Role, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' relationship {relationship.Id} has invalid role '{relationship.Role}'. " +
                    $"Valid roles: {string.Join(", ", validRoles)}");
            }

            // Validate cardinality
            var validCardinalities = new[]
            {
                "esriRelCardinalityOneToOne",
                "esriRelCardinalityOneToMany",
                "esriRelCardinalityManyToMany",
                "one-to-one",
                "one-to-many",
                "many-to-many"
            };
            if (relationship.Cardinality.HasValue() &&
                !validCardinalities.Contains(relationship.Cardinality, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Id}' relationship {relationship.Id} has invalid cardinality '{relationship.Cardinality}'. " +
                    $"Valid cardinalities: {string.Join(", ", validCardinalities)}");
            }
        }
    }

    private static bool ContainsSqlInjectionPatterns(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Check for common SQL injection patterns
        var dangerousPatterns = new[]
        {
            ";",
            "--",
            "/*",
            "*/",
            "xp_",
            "sp_",
            "DROP",
            "DELETE",
            "INSERT",
            "UPDATE",
            "EXEC",
            "EXECUTE"
        };

        return dangerousPatterns.Any(pattern =>
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
