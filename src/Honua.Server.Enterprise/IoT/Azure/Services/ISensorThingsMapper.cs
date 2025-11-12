// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.IoT.Azure.Models;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Service for mapping IoT Hub messages to SensorThings API entities and creating observations
/// </summary>
public interface ISensorThingsMapper
{
    /// <summary>
    /// Process a batch of IoT Hub messages and create observations in SensorThings API
    /// Handles auto-creation of Things, Sensors, ObservedProperties, and Datastreams
    /// </summary>
    /// <param name="messages">IoT Hub messages to process</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Processing result with statistics and errors</returns>
    Task<MessageProcessingResult> ProcessMessagesAsync(
        IReadOnlyList<IoTHubMessage> messages,
        CancellationToken ct = default);
}
