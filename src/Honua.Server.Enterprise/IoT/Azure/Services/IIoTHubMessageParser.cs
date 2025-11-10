// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure.Messaging.EventHubs;
using Honua.Server.Enterprise.IoT.Azure.Models;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Service for parsing Azure Event Hub messages from IoT Hub into structured telemetry data
/// </summary>
public interface IIoTHubMessageParser
{
    /// <summary>
    /// Parse an Event Hub event data into an IoT Hub message with telemetry
    /// </summary>
    /// <param name="eventData">Event data from Azure Event Hub</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Parsed IoT Hub message</returns>
    Task<IoTHubMessage> ParseMessageAsync(EventData eventData, CancellationToken ct = default);

    /// <summary>
    /// Parse multiple Event Hub events in batch
    /// </summary>
    /// <param name="events">Collection of event data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of parsed messages</returns>
    Task<IReadOnlyList<IoTHubMessage>> ParseMessagesAsync(
        IEnumerable<EventData> events,
        CancellationToken ct = default);
}
