// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Integration.Azure.Services;

/// <summary>
/// Default implementation of IoT Hub message parser
/// Handles JSON telemetry format from Azure IoT Hub
/// </summary>
public sealed class IoTHubMessageParser : IIoTHubMessageParser
{
    private readonly IOptionsMonitor<AzureIoTHubOptions> _options;
    private readonly ILogger<IoTHubMessageParser> _logger;

    // IoT Hub system property keys
    private const string DeviceIdProperty = "iothub-connection-device-id";
    private const string ModuleIdProperty = "iothub-connection-module-id";
    private const string MessageIdProperty = "message-id";
    private const string CorrelationIdProperty = "correlation-id";
    private const string EnqueuedTimeProperty = "iothub-enqueuedtime";

    public IoTHubMessageParser(
        IOptionsMonitor<AzureIoTHubOptions> options,
        ILogger<IoTHubMessageParser> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IoTHubMessage> ParseMessageAsync(EventData eventData, CancellationToken ct = default)
    {
        try
        {
            var systemProperties = ExtractSystemProperties(eventData);
            var applicationProperties = ExtractApplicationProperties(eventData);
            var telemetry = await ParseTelemetryAsync(eventData.Body.ToArray(), ct);

            return new IoTHubMessage
            {
                DeviceId = GetDeviceId(systemProperties),
                ModuleId = GetStringProperty(systemProperties, ModuleIdProperty),
                MessageId = GetStringProperty(systemProperties, MessageIdProperty),
                CorrelationId = GetStringProperty(systemProperties, CorrelationIdProperty),
                EnqueuedTime = GetEnqueuedTime(eventData, systemProperties),
                Body = eventData.Body.ToArray(),
                Telemetry = telemetry,
                SystemProperties = systemProperties,
                ApplicationProperties = applicationProperties,
                SequenceNumber = eventData.SequenceNumber,
                PartitionKey = eventData.PartitionKey,
                Offset = eventData.Offset.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse IoT Hub message from Event Hub event");
            throw;
        }
    }

    public async Task<IReadOnlyList<IoTHubMessage>> ParseMessagesAsync(
        IEnumerable<EventData> events,
        CancellationToken ct = default)
    {
        var messages = new List<IoTHubMessage>();

        foreach (var eventData in events)
        {
            try
            {
                var message = await ParseMessageAsync(eventData, ct);
                messages.Add(message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed message (Offset: {Offset})", eventData.Offset);
                // Continue processing other messages
            }
        }

        return messages;
    }

    private Dictionary<string, object> ExtractSystemProperties(EventData eventData)
    {
        var properties = new Dictionary<string, object>();

        foreach (var prop in eventData.SystemProperties)
        {
            properties[prop.Key] = prop.Value;
        }

        return properties;
    }

    private Dictionary<string, object> ExtractApplicationProperties(EventData eventData)
    {
        var properties = new Dictionary<string, object>();

        foreach (var prop in eventData.Properties)
        {
            properties[prop.Key] = prop.Value;
        }

        return properties;
    }

    private async Task<Dictionary<string, object>> ParseTelemetryAsync(byte[] body, CancellationToken ct)
    {
        var options = _options.CurrentValue.TelemetryParsing;

        if (options.DefaultFormat.Equals("Json", StringComparison.OrdinalIgnoreCase))
        {
            return await ParseJsonTelemetryAsync(body, ct);
        }
        else if (options.DefaultFormat.Equals("Binary", StringComparison.OrdinalIgnoreCase))
        {
            // For binary format, return raw bytes
            return new Dictionary<string, object>
            {
                ["data"] = body
            };
        }
        else
        {
            throw new NotSupportedException($"Telemetry format '{options.DefaultFormat}' is not supported");
        }
    }

    private async Task<Dictionary<string, object>> ParseJsonTelemetryAsync(byte[] body, CancellationToken ct)
    {
        try
        {
            var json = Encoding.UTF8.GetString(body);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var telemetry = new Dictionary<string, object>();

            // Handle both object and array of objects
            if (root.ValueKind == JsonValueKind.Object)
            {
                ParseJsonObject(root, telemetry);
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                // For arrays, take the first object (typical for some IoT devices)
                if (root.GetArrayLength() > 0)
                {
                    ParseJsonObject(root[0], telemetry);
                }
            }

            return telemetry;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON telemetry");

            // Return raw string if JSON parsing fails
            return new Dictionary<string, object>
            {
                ["raw"] = Encoding.UTF8.GetString(body)
            };
        }
    }

    private void ParseJsonObject(JsonElement element, Dictionary<string, object> telemetry, string prefix = "")
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            var value = property.Value;

            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt64(out var longValue))
                        telemetry[key] = longValue;
                    else
                        telemetry[key] = value.GetDouble();
                    break;

                case JsonValueKind.String:
                    telemetry[key] = value.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    telemetry[key] = value.GetBoolean();
                    break;

                case JsonValueKind.Object:
                    // Flatten nested objects
                    ParseJsonObject(value, telemetry, key);
                    break;

                case JsonValueKind.Array:
                    // Convert arrays to JSON string for simplicity
                    telemetry[key] = value.GetRawText();
                    break;

                case JsonValueKind.Null:
                    telemetry[key] = null!;
                    break;
            }
        }
    }

    private string GetDeviceId(Dictionary<string, object> systemProperties)
    {
        var deviceId = GetStringProperty(systemProperties, DeviceIdProperty);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new InvalidOperationException("Device ID not found in message system properties");
        }
        return deviceId;
    }

    private string? GetStringProperty(Dictionary<string, object> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private DateTime GetEnqueuedTime(EventData eventData, Dictionary<string, object> systemProperties)
    {
        // Try to get from system properties first
        if (systemProperties.TryGetValue(EnqueuedTimeProperty, out var enqueuedTimeValue))
        {
            if (enqueuedTimeValue is DateTime dt)
                return dt;

            if (enqueuedTimeValue is DateTimeOffset dto)
                return dto.UtcDateTime;

            if (DateTime.TryParse(enqueuedTimeValue?.ToString(), out var parsed))
                return parsed;
        }

        // Fallback to EventData's EnqueuedTime
        return eventData.EnqueuedTime.UtcDateTime;
    }
}
