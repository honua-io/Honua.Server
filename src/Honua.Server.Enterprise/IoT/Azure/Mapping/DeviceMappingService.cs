// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.RegularExpressions;
using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Honua.Server.Enterprise.IoT.Azure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.IoT.Azure.Mapping;

/// <summary>
/// Default implementation of device mapping service
/// Supports file-based configuration and in-memory defaults
/// </summary>
public sealed class DeviceMappingService : IDeviceMappingService
{
    private readonly IOptionsMonitor<AzureIoTHubOptions> _options;
    private readonly ILogger<DeviceMappingService> _logger;
    private DeviceMappingConfiguration _configuration;
    private DateTime _lastConfigLoad = DateTime.MinValue;

    public DeviceMappingService(
        IOptionsMonitor<AzureIoTHubOptions> options,
        ILogger<DeviceMappingService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = CreateDefaultConfiguration();
    }

    public DeviceMappingRule GetMappingForDevice(string deviceId)
    {
        var config = GetConfiguration();

        // Check for exact match first
        if (config.DeviceMappings.TryGetValue(deviceId, out var exactMatch))
        {
            return MergeWithDefaults(exactMatch, config.Defaults);
        }

        // Check for pattern match
        foreach (var kvp in config.DeviceMappings)
        {
            if (IsPatternMatch(kvp.Key, deviceId))
            {
                return MergeWithDefaults(kvp.Value, config.Defaults);
            }
        }

        // Return default mapping
        return CreateDefaultRule(config.Defaults);
    }

    public DeviceMappingConfiguration GetConfiguration()
    {
        // Check if configuration needs reloading and trigger background reload
        var configPath = _options.CurrentValue.MappingConfigurationPath;
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var fileInfo = new FileInfo(configPath);
            if (fileInfo.Exists && fileInfo.LastWriteTimeUtc > _lastConfigLoad)
            {
                // Trigger background reload without blocking the hot path
                // Use fire-and-forget pattern - configuration will be updated asynchronously
                _ = ReloadConfigurationInBackgroundAsync(configPath);
            }
        }

        return _configuration;
    }

    private async Task ReloadConfigurationInBackgroundAsync(string configPath)
    {
        try
        {
            await ReloadConfigurationAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload mapping configuration from {Path}", configPath);
        }
    }

    public string? ResolveTenantId(IoTHubMessage message)
    {
        var config = GetConfiguration();

        // Check device-specific mapping first
        var deviceMapping = GetMappingForDevice(message.DeviceId);
        if (!string.IsNullOrWhiteSpace(deviceMapping.TenantId))
        {
            return deviceMapping.TenantId;
        }

        // Check tenant mapping rules (sorted by priority)
        var tenantMappings = config.TenantMappings
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var rule in tenantMappings)
        {
            // Check device ID pattern
            if (!string.IsNullOrWhiteSpace(rule.DeviceIdPattern))
            {
                if (IsPatternMatch(rule.DeviceIdPattern, message.DeviceId))
                {
                    return InterpolateTenantId(rule.TenantId, message.DeviceId, null);
                }
            }

            // Check property path
            if (!string.IsNullOrWhiteSpace(rule.PropertyPath))
            {
                var propertyValue = GetPropertyValue(message, rule.PropertyPath);
                if (propertyValue != null)
                {
                    if (string.IsNullOrWhiteSpace(rule.PropertyValue) ||
                        propertyValue.Equals(rule.PropertyValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return InterpolateTenantId(rule.TenantId, message.DeviceId, propertyValue);
                    }
                }
            }
        }

        return null; // No tenant mapping found
    }

    public async Task ReloadConfigurationAsync(CancellationToken ct = default)
    {
        var configPath = _options.CurrentValue.MappingConfigurationPath;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            _logger.LogDebug("No mapping configuration path specified, using defaults");
            return;
        }

        if (!File.Exists(configPath))
        {
            _logger.LogWarning("Mapping configuration file not found: {Path}", configPath);
            return;
        }

        try
        {
            _logger.LogInformation("Loading mapping configuration from {Path}", configPath);

            var json = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<DeviceMappingConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (config != null)
            {
                _configuration = config;
                _lastConfigLoad = DateTime.UtcNow;
                _logger.LogInformation("Loaded {Count} device mappings and {TenantCount} tenant mappings",
                    config.DeviceMappings.Count,
                    config.TenantMappings.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mapping configuration from {Path}", configPath);
            throw;
        }
    }

    private DeviceMappingConfiguration CreateDefaultConfiguration()
    {
        return new DeviceMappingConfiguration
        {
            Defaults = new DefaultMappingRules
            {
                AutoCreateThings = true,
                AutoCreateSensors = true,
                AutoCreateObservedProperties = true,
                AutoCreateDatastreams = true,
                ThingNameTemplate = "IoT Device: {deviceId}",
                ThingDescriptionTemplate = "Device {deviceId} connected via Azure IoT Hub",
                DefaultUnit = new UnitOfMeasurementConfig
                {
                    Name = "unitless",
                    Symbol = "",
                    Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Unitless"
                }
            }
        };
    }

    private DeviceMappingRule CreateDefaultRule(DefaultMappingRules? defaults)
    {
        defaults ??= new DefaultMappingRules();

        return new DeviceMappingRule
        {
            ThingNameTemplate = defaults.ThingNameTemplate,
            ThingDescriptionTemplate = defaults.ThingDescriptionTemplate
        };
    }

    private DeviceMappingRule MergeWithDefaults(DeviceMappingRule rule, DefaultMappingRules? defaults)
    {
        if (defaults == null)
            return rule;

        return new DeviceMappingRule
        {
            ThingNameTemplate = rule.ThingNameTemplate ?? defaults.ThingNameTemplate,
            ThingDescriptionTemplate = rule.ThingDescriptionTemplate ?? defaults.ThingDescriptionTemplate,
            ThingProperties = rule.ThingProperties,
            TelemetryMappings = rule.TelemetryMappings,
            DeviceTwinProperties = rule.DeviceTwinProperties,
            TenantId = rule.TenantId
        };
    }

    private bool IsPatternMatch(string pattern, string value)
    {
        // Convert simple wildcard pattern to regex
        // Supports: "device-*", "temp-sensor-{id}", etc.
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\{[^}]+\\}", "[^-]+") + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    private string InterpolateTenantId(string template, string deviceId, string? propertyValue)
    {
        return template
            .Replace("{deviceId}", deviceId, StringComparison.OrdinalIgnoreCase)
            .Replace("{propertyValue}", propertyValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetPropertyValue(IoTHubMessage message, string propertyPath)
    {
        // Simple property path resolution (e.g., "properties.tenant", "customProperties.orgId")
        var parts = propertyPath.Split('.');

        // Check application properties
        if (parts[0].Equals("properties", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
        {
            if (message.ApplicationProperties.TryGetValue(parts[1], out var value))
            {
                return value?.ToString();
            }
        }

        // Check telemetry
        if (parts[0].Equals("telemetry", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
        {
            if (message.Telemetry.TryGetValue(parts[1], out var value))
            {
                return value?.ToString();
            }
        }

        return null;
    }
}
