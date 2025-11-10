// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.IoT.Azure.Configuration;

/// <summary>
/// Configuration for mapping IoT Hub devices to SensorThings API entities
/// Can be loaded from JSON or YAML file
/// </summary>
public sealed class DeviceMappingConfiguration
{
    /// <summary>
    /// Global default mappings (used when no device-specific mapping exists)
    /// </summary>
    public DefaultMappingRules? Defaults { get; set; }

    /// <summary>
    /// Device-specific mapping rules (keyed by device ID pattern)
    /// Supports wildcards: "temp-sensor-*", "device-{location}-*"
    /// </summary>
    public Dictionary<string, DeviceMappingRule> DeviceMappings { get; set; } = new();

    /// <summary>
    /// Tenant mapping rules (for multi-tenancy)
    /// Maps device ID patterns or properties to tenant IDs
    /// </summary>
    public List<TenantMappingRule> TenantMappings { get; set; } = new();
}

/// <summary>
/// Default mapping rules applied to all devices
/// </summary>
public sealed class DefaultMappingRules
{
    /// <summary>
    /// Auto-create Thing entities for new devices
    /// </summary>
    public bool AutoCreateThings { get; set; } = true;

    /// <summary>
    /// Auto-create Sensor entities for new telemetry fields
    /// </summary>
    public bool AutoCreateSensors { get; set; } = true;

    /// <summary>
    /// Auto-create ObservedProperty entities for new telemetry fields
    /// </summary>
    public bool AutoCreateObservedProperties { get; set; } = true;

    /// <summary>
    /// Auto-create Datastream entities for new sensor/property combinations
    /// </summary>
    public bool AutoCreateDatastreams { get; set; } = true;

    /// <summary>
    /// Thing name template (supports {deviceId}, {modelId}, etc.)
    /// </summary>
    public string ThingNameTemplate { get; set; } = "IoT Device: {deviceId}";

    /// <summary>
    /// Thing description template
    /// </summary>
    public string ThingDescriptionTemplate { get; set; } = "Device {deviceId} connected via Azure IoT Hub";

    /// <summary>
    /// Default unit of measurement for numeric telemetry
    /// </summary>
    public UnitOfMeasurementConfig DefaultUnit { get; set; } = new()
    {
        Name = "unitless",
        Symbol = "",
        Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Unitless"
    };
}

/// <summary>
/// Mapping rule for a specific device or device pattern
/// </summary>
public sealed class DeviceMappingRule
{
    /// <summary>
    /// Override Thing name template for this device
    /// </summary>
    public string? ThingNameTemplate { get; set; }

    /// <summary>
    /// Override Thing description template
    /// </summary>
    public string? ThingDescriptionTemplate { get; set; }

    /// <summary>
    /// Additional properties to add to the Thing entity
    /// </summary>
    public Dictionary<string, object>? ThingProperties { get; set; }

    /// <summary>
    /// Telemetry field mappings
    /// Key: telemetry field name
    /// Value: datastream configuration
    /// </summary>
    public Dictionary<string, DatastreamMapping> TelemetryMappings { get; set; } = new();

    /// <summary>
    /// Device twin property paths to include in Thing properties
    /// </summary>
    public List<string>? DeviceTwinProperties { get; set; }

    /// <summary>
    /// Explicit tenant ID for this device (overrides tenant mapping rules)
    /// </summary>
    public string? TenantId { get; set; }
}

/// <summary>
/// Mapping configuration for a telemetry field to a Datastream
/// </summary>
public sealed class DatastreamMapping
{
    /// <summary>
    /// Datastream name (if null, uses telemetry field name)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Datastream description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Observation type (e.g., "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement")
    /// </summary>
    public string? ObservationType { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public UnitOfMeasurementConfig? UnitOfMeasurement { get; set; }

    /// <summary>
    /// Sensor encoding type (e.g., "application/pdf", "text/html")
    /// </summary>
    public string SensorEncodingType { get; set; } = "application/json";

    /// <summary>
    /// Sensor metadata (arbitrary JSON)
    /// </summary>
    public Dictionary<string, object>? SensorMetadata { get; set; }

    /// <summary>
    /// ObservedProperty name
    /// </summary>
    public string? ObservedPropertyName { get; set; }

    /// <summary>
    /// ObservedProperty definition URI
    /// </summary>
    public string? ObservedPropertyDefinition { get; set; }

    /// <summary>
    /// ObservedProperty description
    /// </summary>
    public string? ObservedPropertyDescription { get; set; }

    /// <summary>
    /// Transform expression (simple expressions like "value * 10", "value - 273.15")
    /// </summary>
    public string? Transform { get; set; }
}

/// <summary>
/// Unit of measurement configuration
/// </summary>
public sealed class UnitOfMeasurementConfig
{
    /// <summary>
    /// Unit name (e.g., "Celsius", "meter")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Unit symbol (e.g., "°C", "m")
    /// </summary>
    public string Symbol { get; set; } = default!;

    /// <summary>
    /// Unit definition URI
    /// </summary>
    public string Definition { get; set; } = default!;
}

/// <summary>
/// Tenant mapping rule for multi-tenancy
/// </summary>
public sealed class TenantMappingRule
{
    /// <summary>
    /// Device ID pattern (supports wildcards)
    /// </summary>
    public string? DeviceIdPattern { get; set; }

    /// <summary>
    /// Property path in device message or twin (e.g., "properties.tenant", "customProperties.orgId")
    /// </summary>
    public string? PropertyPath { get; set; }

    /// <summary>
    /// Expected property value (for exact match)
    /// </summary>
    public string? PropertyValue { get; set; }

    /// <summary>
    /// Tenant ID to assign when this rule matches
    /// Can use placeholders: {deviceId}, {propertyValue}
    /// </summary>
    public string TenantId { get; set; } = default!;

    /// <summary>
    /// Priority (higher priority rules are checked first)
    /// </summary>
    public int Priority { get; set; } = 0;
}
