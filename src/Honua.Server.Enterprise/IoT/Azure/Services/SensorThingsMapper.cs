// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Honua.Server.Enterprise.IoT.Azure.Mapping;
using Honua.Server.Enterprise.IoT.Azure.Models;
using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Maps IoT Hub messages to SensorThings API entities
/// Handles auto-creation of Things, Sensors, ObservedProperties, and Datastreams
/// </summary>
public sealed class SensorThingsMapper : ISensorThingsMapper
{
    private readonly ISensorThingsRepository _repository;
    private readonly IDeviceMappingService _mappingService;
    private readonly IOptionsMonitor<AzureIoTHubOptions> _options;
    private readonly ILogger<SensorThingsMapper> _logger;

    // Caches for entity lookups (device ID -> Thing ID, etc.)
    private readonly Dictionary<string, string> _thingCache = new();
    private readonly Dictionary<string, string> _sensorCache = new();
    private readonly Dictionary<string, string> _observedPropertyCache = new();
    private readonly Dictionary<string, string> _datastreamCache = new();

    public SensorThingsMapper(
        ISensorThingsRepository repository,
        IDeviceMappingService mappingService,
        IOptionsMonitor<AzureIoTHubOptions> options,
        ILogger<SensorThingsMapper> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MessageProcessingResult> ProcessMessagesAsync(
        IReadOnlyList<IoTHubMessage> messages,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var errors = new List<ProcessingError>();
        var observations = new List<Observation>();
        var thingsCreated = 0;
        var datastreamsCreated = 0;

        foreach (var message in messages)
        {
            try
            {
                var messageObservations = await ProcessSingleMessageAsync(message, ct);
                observations.AddRange(messageObservations);

                // Track if new entities were created (approximate)
                if (!_thingCache.ContainsKey(message.DeviceId))
                {
                    thingsCreated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message from device {DeviceId}", message.DeviceId);
                errors.Add(new ProcessingError
                {
                    DeviceId = message.DeviceId,
                    MessageId = message.MessageId,
                    Message = ex.Message,
                    ExceptionDetails = ex.ToString()
                });
            }
        }

        // Batch create observations for performance
        IReadOnlyList<Observation> createdObservations = Array.Empty<Observation>();
        if (observations.Count > 0)
        {
            try
            {
                createdObservations = await _repository.CreateObservationsBatchAsync(observations, ct);
                _logger.LogInformation("Created {Count} observations from {MessageCount} messages",
                    createdObservations.Count,
                    messages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch create observations");
                errors.Add(new ProcessingError
                {
                    DeviceId = "BATCH",
                    Message = "Batch observation creation failed",
                    ExceptionDetails = ex.ToString()
                });
            }
        }

        sw.Stop();

        return new MessageProcessingResult
        {
            SuccessCount = messages.Count - errors.Count,
            FailureCount = errors.Count,
            ObservationsCreated = createdObservations.Count,
            ThingsCreated = thingsCreated,
            DatastreamsCreated = datastreamsCreated,
            Duration = sw.Elapsed,
            Errors = errors
        };
    }

    private async Task<List<Observation>> ProcessSingleMessageAsync(IoTHubMessage message, CancellationToken ct)
    {
        // 1. Get or create Thing for device
        var thingId = await GetOrCreateThingAsync(message, ct);

        // 2. Process each telemetry field
        var observations = new List<Observation>();
        var mappingRule = _mappingService.GetMappingForDevice(message.DeviceId);
        var config = _mappingService.GetConfiguration();

        foreach (var (fieldName, fieldValue) in message.Telemetry)
        {
            try
            {
                // Skip null values
                if (fieldValue == null)
                    continue;

                // Get datastream for this field
                var datastreamId = await GetOrCreateDatastreamAsync(
                    thingId,
                    message.DeviceId,
                    fieldName,
                    fieldValue,
                    mappingRule,
                    config.Defaults,
                    ct);

                // Create observation
                var observation = CreateObservation(datastreamId, fieldName, fieldValue, message);
                observations.Add(observation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process telemetry field {Field} from device {DeviceId}",
                    fieldName, message.DeviceId);
            }
        }

        return observations;
    }

    private async Task<string> GetOrCreateThingAsync(IoTHubMessage message, CancellationToken ct)
    {
        var deviceId = message.DeviceId;

        // Check cache first
        if (_thingCache.TryGetValue(deviceId, out var cachedThingId))
        {
            return cachedThingId;
        }

        // Try to find existing Thing by device ID property
        var existingThings = await _repository.GetThingsAsync(new Query.QueryOptions
        {
            Filter = $"properties/deviceId eq '{deviceId}'"
        }, ct);

        if (existingThings.Values.Count > 0)
        {
            var thingId = existingThings.Values[0].Id;
            _thingCache[deviceId] = thingId;
            return thingId;
        }

        // Create new Thing
        var mappingRule = _mappingService.GetMappingForDevice(deviceId);
        var thing = CreateThing(message, mappingRule);

        var createdThing = await _repository.CreateThingAsync(thing, ct);
        _thingCache[deviceId] = createdThing.Id;

        _logger.LogInformation("Created Thing {ThingId} for device {DeviceId}", createdThing.Id, deviceId);

        return createdThing.Id;
    }

    private async Task<string> GetOrCreateDatastreamAsync(
        string thingId,
        string deviceId,
        string fieldName,
        object fieldValue,
        DeviceMappingRule mappingRule,
        DefaultMappingRules? defaults,
        CancellationToken ct)
    {
        var cacheKey = $"{deviceId}:{fieldName}";

        // Check cache
        if (_datastreamCache.TryGetValue(cacheKey, out var cachedDatastreamId))
        {
            return cachedDatastreamId;
        }

        // Try to find existing Datastream
        var existingDatastreams = await _repository.GetThingDatastreamsAsync(thingId, new Query.QueryOptions
        {
            Filter = $"properties/telemetryField eq '{fieldName}'"
        }, ct);

        if (existingDatastreams.Values.Count > 0)
        {
            var datastreamId = existingDatastreams.Values[0].Id;
            _datastreamCache[cacheKey] = datastreamId;
            return datastreamId;
        }

        // Create new Sensor, ObservedProperty, and Datastream
        var sensorId = await GetOrCreateSensorAsync(deviceId, fieldName, mappingRule, ct);
        var observedPropertyId = await GetOrCreateObservedPropertyAsync(fieldName, mappingRule, ct);
        var datastream = CreateDatastream(thingId, sensorId, observedPropertyId, fieldName, fieldValue, mappingRule, defaults);

        var createdDatastream = await _repository.CreateDatastreamAsync(datastream, ct);
        _datastreamCache[cacheKey] = createdDatastream.Id;

        _logger.LogInformation("Created Datastream {DatastreamId} for field {Field} on device {DeviceId}",
            createdDatastream.Id, fieldName, deviceId);

        return createdDatastream.Id;
    }

    private async Task<string> GetOrCreateSensorAsync(
        string deviceId,
        string fieldName,
        DeviceMappingRule mappingRule,
        CancellationToken ct)
    {
        var cacheKey = $"{deviceId}:{fieldName}";

        if (_sensorCache.TryGetValue(cacheKey, out var cachedSensorId))
        {
            return cachedSensorId;
        }

        // Try to find existing Sensor
        var sensorName = $"{deviceId}-{fieldName}";
        var existingSensors = await _repository.GetSensorsAsync(new Query.QueryOptions
        {
            Filter = $"name eq '{sensorName}'"
        }, ct);

        if (existingSensors.Values.Count > 0)
        {
            var sensorId = existingSensors.Values[0].Id;
            _sensorCache[cacheKey] = sensorId;
            return sensorId;
        }

        // Get sensor metadata from mapping
        var telemetryMapping = mappingRule.TelemetryMappings.GetValueOrDefault(fieldName);

        var sensor = new Sensor
        {
            Id = Guid.NewGuid().ToString(),
            Name = sensorName,
            Description = $"Sensor measuring {fieldName} on device {deviceId}",
            EncodingType = telemetryMapping?.SensorEncodingType ?? "application/json",
            Metadata = telemetryMapping?.SensorMetadata != null
                ? System.Text.Json.JsonSerializer.Serialize(telemetryMapping.SensorMetadata)
                : "{}",
            Properties = new Dictionary<string, object>
            {
                ["deviceId"] = deviceId,
                ["telemetryField"] = fieldName
            }
        };

        var createdSensor = await _repository.CreateSensorAsync(sensor, ct);
        _sensorCache[cacheKey] = createdSensor.Id;

        return createdSensor.Id;
    }

    private async Task<string> GetOrCreateObservedPropertyAsync(
        string fieldName,
        DeviceMappingRule mappingRule,
        CancellationToken ct)
    {
        if (_observedPropertyCache.TryGetValue(fieldName, out var cachedId))
        {
            return cachedId;
        }

        // Get mapping for this field
        var telemetryMapping = mappingRule.TelemetryMappings.GetValueOrDefault(fieldName);
        var propertyName = telemetryMapping?.ObservedPropertyName ?? fieldName;

        // Try to find existing ObservedProperty
        var existingProperties = await _repository.GetObservedPropertiesAsync(new Query.QueryOptions
        {
            Filter = $"name eq '{propertyName}'"
        }, ct);

        if (existingProperties.Values.Count > 0)
        {
            var propertyId = existingProperties.Values[0].Id;
            _observedPropertyCache[fieldName] = propertyId;
            return propertyId;
        }

        // Create new ObservedProperty
        var observedProperty = new ObservedProperty
        {
            Id = Guid.NewGuid().ToString(),
            Name = propertyName,
            Description = telemetryMapping?.ObservedPropertyDescription ?? $"Property: {propertyName}",
            Definition = telemetryMapping?.ObservedPropertyDefinition ?? $"http://honua.io/properties/{fieldName}"
        };

        var created = await _repository.CreateObservedPropertyAsync(observedProperty, ct);
        _observedPropertyCache[fieldName] = created.Id;

        return created.Id;
    }

    private Thing CreateThing(IoTHubMessage message, DeviceMappingRule mappingRule)
    {
        var name = InterpolateTemplate(mappingRule.ThingNameTemplate ?? "IoT Device: {deviceId}", message);
        var description = InterpolateTemplate(
            mappingRule.ThingDescriptionTemplate ?? "Device {deviceId} connected via Azure IoT Hub",
            message);

        var properties = new Dictionary<string, object>
        {
            ["deviceId"] = message.DeviceId,
            ["source"] = "Azure IoT Hub"
        };

        if (!string.IsNullOrWhiteSpace(message.ModuleId))
        {
            properties["moduleId"] = message.ModuleId;
        }

        // Add custom properties from mapping
        if (mappingRule.ThingProperties != null)
        {
            foreach (var (key, value) in mappingRule.ThingProperties)
            {
                properties[key] = value;
            }
        }

        // Add tenant ID if available
        var tenantId = _mappingService.ResolveTenantId(message);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            properties["tenantId"] = tenantId;
        }

        return new Thing
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Properties = properties
        };
    }

    private Datastream CreateDatastream(
        string thingId,
        string sensorId,
        string observedPropertyId,
        string fieldName,
        object fieldValue,
        DeviceMappingRule mappingRule,
        DefaultMappingRules? defaults)
    {
        var telemetryMapping = mappingRule.TelemetryMappings.GetValueOrDefault(fieldName);

        var name = telemetryMapping?.Name ?? fieldName;
        var description = telemetryMapping?.Description ?? $"Datastream for {fieldName}";
        var observationType = telemetryMapping?.ObservationType
            ?? "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement";

        // Determine unit of measurement
        var unit = telemetryMapping?.UnitOfMeasurement != null
            ? new UnitOfMeasurement
            {
                Name = telemetryMapping.UnitOfMeasurement.Name,
                Symbol = telemetryMapping.UnitOfMeasurement.Symbol,
                Definition = telemetryMapping.UnitOfMeasurement.Definition
            }
            : (defaults?.DefaultUnit != null
                ? new UnitOfMeasurement
                {
                    Name = defaults.DefaultUnit.Name,
                    Symbol = defaults.DefaultUnit.Symbol,
                    Definition = defaults.DefaultUnit.Definition
                }
                : new UnitOfMeasurement
                {
                    Name = "unitless",
                    Symbol = "",
                    Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Unitless"
                });

        return new Datastream
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            ObservationType = observationType,
            UnitOfMeasurement = unit,
            ThingId = thingId,
            SensorId = sensorId,
            ObservedPropertyId = observedPropertyId,
            Properties = new Dictionary<string, object>
            {
                ["telemetryField"] = fieldName
            }
        };
    }

    private Observation CreateObservation(
        string datastreamId,
        string fieldName,
        object fieldValue,
        IoTHubMessage message)
    {
        var options = _options.CurrentValue.TelemetryParsing;

        // Determine phenomenon time (prefer custom timestamp from telemetry, fallback to enqueued time)
        var phenomenonTime = message.EnqueuedTime;
        if (!string.IsNullOrWhiteSpace(options.TimestampProperty) &&
            message.Telemetry.TryGetValue(options.TimestampProperty, out var timestampValue))
        {
            if (timestampValue is DateTime dt)
            {
                phenomenonTime = dt;
            }
            else if (DateTime.TryParse(timestampValue?.ToString(), out var parsed))
            {
                phenomenonTime = parsed;
            }
        }

        // Build observation parameters (preserve metadata)
        var parameters = new Dictionary<string, object>();

        if (options.PreserveSystemProperties)
        {
            parameters["iotHub_systemProperties"] = message.SystemProperties;
        }

        if (options.PreserveApplicationProperties && message.ApplicationProperties.Count > 0)
        {
            parameters["iotHub_applicationProperties"] = message.ApplicationProperties;
        }

        parameters["iotHub_deviceId"] = message.DeviceId;
        parameters["iotHub_enqueuedTime"] = message.EnqueuedTime;
        parameters["iotHub_sequenceNumber"] = message.SequenceNumber;

        if (!string.IsNullOrWhiteSpace(message.MessageId))
        {
            parameters["iotHub_messageId"] = message.MessageId;
        }

        return new Observation
        {
            Id = Guid.NewGuid().ToString(),
            PhenomenonTime = phenomenonTime,
            ResultTime = message.EnqueuedTime,
            Result = fieldValue,
            DatastreamId = datastreamId,
            Parameters = parameters,
            ServerTimestamp = DateTime.UtcNow
        };
    }

    private string InterpolateTemplate(string template, IoTHubMessage message)
    {
        return template
            .Replace("{deviceId}", message.DeviceId, StringComparison.OrdinalIgnoreCase)
            .Replace("{moduleId}", message.ModuleId ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
