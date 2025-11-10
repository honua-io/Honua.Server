// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Models;

namespace Honua.Integration.Azure.Mapping;

/// <summary>
/// Service for managing device-to-SensorThings mapping configuration
/// </summary>
public interface IDeviceMappingService
{
    /// <summary>
    /// Get mapping rule for a specific device
    /// Returns default rules if no device-specific rule exists
    /// </summary>
    DeviceMappingRule GetMappingForDevice(string deviceId);

    /// <summary>
    /// Get or load the full mapping configuration
    /// </summary>
    DeviceMappingConfiguration GetConfiguration();

    /// <summary>
    /// Determine tenant ID for a device based on mapping rules
    /// </summary>
    string? ResolveTenantId(IoTHubMessage message);

    /// <summary>
    /// Reload configuration from file (if file-based)
    /// </summary>
    Task ReloadConfigurationAsync(CancellationToken ct = default);
}
