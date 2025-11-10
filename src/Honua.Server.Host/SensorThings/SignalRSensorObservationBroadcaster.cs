using System.Collections.Concurrent;
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.SensorThings;

/// <summary>
/// SignalR implementation of sensor observation broadcaster.
/// Handles real-time streaming of observations to subscribed clients with rate limiting and batching.
/// </summary>
public class SignalRSensorObservationBroadcaster : ISensorObservationBroadcaster
{
    private readonly IHubContext<SensorObservationHub> _hubContext;
    private readonly ILogger<SignalRSensorObservationBroadcaster> _logger;
    private readonly SensorObservationStreamingOptions _options;

    // Rate limiting state - tracks observation counts per group per second
    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimitState = new();

    public SignalRSensorObservationBroadcaster(
        IHubContext<SensorObservationHub> hubContext,
        ILogger<SignalRSensorObservationBroadcaster> logger,
        SensorObservationStreamingOptions options)
    {
        _hubContext = hubContext;
        _logger = logger;
        _options = options;
    }

    public bool IsEnabled => _options.Enabled;
    public int RateLimitPerSecond => _options.RateLimitPerSecond;

    public async Task BroadcastObservationAsync(
        Observation observation,
        Datastream datastream,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            var payload = CreateObservationPayload(observation, datastream);

            // Broadcast to datastream-specific subscribers
            var datastreamGroup = $"datastream:{datastream.Id}";
            if (await CheckRateLimitAsync(datastreamGroup))
            {
                await _hubContext.Clients.Group(datastreamGroup)
                    .SendAsync("ObservationCreated", payload, cancellationToken);
            }

            // Broadcast to thing-specific subscribers
            var thingGroup = $"thing:{datastream.ThingId}";
            if (await CheckRateLimitAsync(thingGroup))
            {
                await _hubContext.Clients.Group(thingGroup)
                    .SendAsync("ObservationCreated", payload, cancellationToken);
            }

            // Broadcast to sensor-specific subscribers
            var sensorGroup = $"sensor:{datastream.SensorId}";
            if (await CheckRateLimitAsync(sensorGroup))
            {
                await _hubContext.Clients.Group(sensorGroup)
                    .SendAsync("ObservationCreated", payload, cancellationToken);
            }

            // Broadcast to all-observations subscribers (admin only)
            if (await CheckRateLimitAsync("all-observations"))
            {
                await _hubContext.Clients.Group("all-observations")
                    .SendAsync("ObservationCreated", payload, cancellationToken);
            }

            _logger.LogDebug(
                "Broadcasted observation {ObservationId} from datastream {DatastreamId}",
                observation.Id,
                datastream.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error broadcasting observation {ObservationId}",
                observation.Id);
        }
    }

    public async Task BroadcastObservationsAsync(
        IReadOnlyList<(Observation Observation, Datastream Datastream)> observations,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || observations.Count == 0)
            return;

        try
        {
            // Group observations by target groups for efficient batching
            var groupedByDatastream = observations
                .GroupBy(o => o.Datastream.Id)
                .ToList();

            // If we have many observations (>100/sec), batch them
            if (_options.BatchingEnabled && observations.Count > _options.BatchingThreshold)
            {
                await BroadcastBatchedObservationsAsync(groupedByDatastream, cancellationToken);
            }
            else
            {
                // Broadcast individually
                foreach (var (observation, datastream) in observations)
                {
                    await BroadcastObservationAsync(observation, datastream, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting batch of {Count} observations", observations.Count);
        }
    }

    private async Task BroadcastBatchedObservationsAsync(
        IEnumerable<IGrouping<string, (Observation Observation, Datastream Datastream)>> groupedObservations,
        CancellationToken cancellationToken)
    {
        foreach (var datastreamGroup in groupedObservations)
        {
            var observations = datastreamGroup.ToList();
            var firstDatastream = observations.First().Datastream;

            var batch = new
            {
                datastreamId = firstDatastream.Id,
                thingId = firstDatastream.ThingId,
                sensorId = firstDatastream.SensorId,
                count = observations.Count,
                observations = observations.Select(o => CreateObservationPayload(o.Observation, o.Datastream)).ToList()
            };

            // Broadcast batched observations
            var group = $"datastream:{firstDatastream.Id}";
            await _hubContext.Clients.Group(group)
                .SendAsync("ObservationsBatch", batch, cancellationToken);

            var thingGroup = $"thing:{firstDatastream.ThingId}";
            await _hubContext.Clients.Group(thingGroup)
                .SendAsync("ObservationsBatch", batch, cancellationToken);

            var sensorGroup = $"sensor:{firstDatastream.SensorId}";
            await _hubContext.Clients.Group(sensorGroup)
                .SendAsync("ObservationsBatch", batch, cancellationToken);

            await _hubContext.Clients.Group("all-observations")
                .SendAsync("ObservationsBatch", batch, cancellationToken);

            _logger.LogInformation(
                "Broadcasted batch of {Count} observations from datastream {DatastreamId}",
                observations.Count,
                firstDatastream.Id);
        }
    }

    private async Task<bool> CheckRateLimitAsync(string groupName)
    {
        if (!_options.RateLimitingEnabled)
            return true;

        var now = DateTime.UtcNow;
        var state = _rateLimitState.GetOrAdd(groupName, _ => new RateLimitState());

        lock (state)
        {
            // Reset counter if we've moved to a new second
            if ((now - state.WindowStart).TotalSeconds >= 1)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            // Check if we've exceeded the rate limit
            if (state.Count >= _options.RateLimitPerSecond)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for group {GroupName}: {Count} observations/sec",
                    groupName,
                    state.Count);
                return false;
            }

            state.Count++;
            return true;
        }
    }

    private object CreateObservationPayload(Observation observation, Datastream datastream)
    {
        return new
        {
            observationId = observation.Id,
            datastreamId = datastream.Id,
            datastreamName = datastream.Name,
            thingId = datastream.ThingId,
            sensorId = datastream.SensorId,
            observedPropertyId = datastream.ObservedPropertyId,
            phenomenonTime = observation.PhenomenonTime,
            resultTime = observation.ResultTime,
            result = observation.Result,
            resultQuality = observation.ResultQuality,
            parameters = observation.Parameters,
            unitOfMeasurement = new
            {
                name = datastream.UnitOfMeasurement.Name,
                symbol = datastream.UnitOfMeasurement.Symbol,
                definition = datastream.UnitOfMeasurement.Definition
            },
            serverTimestamp = observation.ServerTimestamp
        };
    }

    private class RateLimitState
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int Count { get; set; } = 0;
    }
}
