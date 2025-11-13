// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Navigation;
using Honua.MapSDK.Models.Routing;
using Honua.MapSDK.Services.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Navigation;

/// <summary>
/// Service for managing turn-by-turn navigation with multi-provider support
/// </summary>
public class NavigationService
{
    private readonly Dictionary<string, IRoutingService> _routingProviders;
    private readonly ILogger<NavigationService>? _logger;
    private readonly Dictionary<string, NavigationSession> _activeSessions;
    private readonly SemaphoreSlim _sessionsLock;

    public NavigationService(
        IEnumerable<IRoutingService>? routingServices = null,
        ILogger<NavigationService>? logger = null)
    {
        _routingProviders = routingServices?
            .ToDictionary(s => s.ProviderName, s => s)
            ?? new Dictionary<string, IRoutingService>();
        _logger = logger;
        _activeSessions = new Dictionary<string, NavigationSession>();
        _sessionsLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Start a new navigation session
    /// </summary>
    /// <param name="route">Route to navigate</param>
    /// <param name="options">Navigation options</param>
    /// <returns>Navigation session</returns>
    public async Task<NavigationSession> StartNavigationAsync(
        Route route,
        NavigationOptions? options = null)
    {
        if (route == null)
        {
            throw new ArgumentNullException(nameof(route));
        }

        var session = new NavigationSession
        {
            Route = route,
            Options = options ?? new NavigationOptions(),
            State = NavigationState.NotStarted,
            TotalDistanceRemaining = route.Distance,
            TotalTimeRemaining = route.Duration,
            EstimatedArrival = DateTime.UtcNow.AddSeconds(route.Duration),
            StartTime = DateTime.UtcNow
        };

        // Generate voice instructions for the route
        if (session.Options.EnableVoiceGuidance)
        {
            session.VoiceInstructions = GenerateVoiceInstructions(route, session.Options);
        }

        // Add to active sessions
        await _sessionsLock.WaitAsync();
        try
        {
            _activeSessions[session.Id] = session;
        }
        finally
        {
            _sessionsLock.Release();
        }

        _logger?.LogInformation("Navigation session started: {SessionId}", session.Id);

        return session;
    }

    /// <summary>
    /// Update navigation with current location
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="location">Current location [longitude, latitude]</param>
    /// <param name="heading">Current heading in degrees (optional)</param>
    /// <param name="speed">Current speed in m/s (optional)</param>
    /// <returns>Navigation progress</returns>
    public async Task<NavigationProgress> UpdateLocationAsync(
        string sessionId,
        double[] location,
        double? heading = null,
        double? speed = null)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session not found: {sessionId}");
        }

        if (session.State == NavigationState.Arrived || session.State == NavigationState.Cancelled)
        {
            throw new InvalidOperationException($"Navigation session has ended: {sessionId}");
        }

        // Update current position
        session.CurrentLocation = location;
        session.CurrentHeading = heading;
        session.CurrentSpeed = speed;

        // Start navigation if not started
        if (session.State == NavigationState.NotStarted)
        {
            session.State = NavigationState.Navigating;
        }

        // Calculate progress
        var progress = CalculateProgress(session, location);

        // Check if off route
        var isOffRoute = CheckIfOffRoute(session, location);
        session.IsOffRoute = isOffRoute;

        if (isOffRoute && session.Options?.EnableRerouting == true)
        {
            // Check if we should reroute (respect min interval)
            var shouldReroute = session.LastRerouteTime == null ||
                (DateTime.UtcNow - session.LastRerouteTime.Value).TotalSeconds >= session.Options.MinRerouteInterval;

            if (shouldReroute)
            {
                session.State = NavigationState.OffRoute;
                _logger?.LogInformation("User off route, triggering reroute: {SessionId}", sessionId);
                // Rerouting will be handled by the caller
            }
        }

        // Check if arrived
        if (progress.DistanceRemaining < 10) // Within 10 meters of destination
        {
            session.State = NavigationState.Arrived;
            session.EndTime = DateTime.UtcNow;
            _logger?.LogInformation("Navigation arrived at destination: {SessionId}", sessionId);
        }

        return progress;
    }

    /// <summary>
    /// Reroute from current location
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="currentLocation">Current location</param>
    /// <param name="providerName">Routing provider to use (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated navigation session with new route</returns>
    public async Task<NavigationSession> RerouteAsync(
        string sessionId,
        double[] currentLocation,
        string? providerName = null,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session not found: {sessionId}");
        }

        if (session.Route == null)
        {
            throw new InvalidOperationException("Session has no route");
        }

        session.State = NavigationState.Rerouting;
        session.LastRerouteTime = DateTime.UtcNow;
        session.RerouteCount++;

        _logger?.LogInformation("Rerouting navigation session: {SessionId}, count: {Count}",
            sessionId, session.RerouteCount);

        try
        {
            // Get routing provider
            var provider = GetRoutingProvider(providerName ?? session.Route.RoutingEngine);
            if (provider == null)
            {
                throw new InvalidOperationException($"Routing provider not available: {providerName}");
            }

            // Create waypoints: current location + remaining waypoints
            var waypoints = new List<Waypoint>
            {
                new Waypoint
                {
                    Coordinates = currentLocation,
                    Type = WaypointType.Start
                }
            };

            // Add remaining waypoints from original route
            var remainingWaypoints = session.Route.Waypoints
                .Where(w => w.Type == WaypointType.End || w.Type == WaypointType.Via)
                .ToList();
            waypoints.AddRange(remainingWaypoints);

            // Calculate new route
            var options = new RouteOptions
            {
                TravelMode = session.Route.TravelMode,
                IncludeInstructions = true,
                IncludeTraffic = true,
                Units = session.Options?.Units ?? Models.Routing.DistanceUnit.Metric
            };

            var newRoute = await provider.CalculateRouteAsync(waypoints, options, cancellationToken);

            // Update session with new route
            session.Route = newRoute;
            session.CurrentStepIndex = 0;
            session.TotalDistanceRemaining = newRoute.Distance;
            session.TotalTimeRemaining = newRoute.Duration;
            session.EstimatedArrival = DateTime.UtcNow.AddSeconds(newRoute.Duration);
            session.IsOffRoute = false;
            session.State = NavigationState.Navigating;

            // Regenerate voice instructions
            if (session.Options?.EnableVoiceGuidance == true)
            {
                session.VoiceInstructions = GenerateVoiceInstructions(newRoute, session.Options);
                session.AnnouncedInstructions.Clear(); // Reset announced instructions
            }

            _logger?.LogInformation("Reroute completed successfully: {SessionId}", sessionId);

            return session;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Reroute failed for session: {SessionId}", sessionId);
            session.State = NavigationState.Navigating; // Resume with old route
            throw;
        }
    }

    /// <summary>
    /// Get voice instructions that should be announced based on current location
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="distanceToManeuver">Distance to next maneuver in meters</param>
    /// <returns>List of voice instructions to announce</returns>
    public async Task<List<VoiceInstruction>> GetVoiceInstructionsAsync(
        string sessionId,
        double distanceToManeuver)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null || !session.Options?.EnableVoiceGuidance == true)
        {
            return new List<VoiceInstruction>();
        }

        var instructionsToAnnounce = new List<VoiceInstruction>();

        foreach (var instruction in session.VoiceInstructions)
        {
            if (!instruction.HasBeenAnnounced &&
                instruction.StepIndex == session.CurrentStepIndex &&
                distanceToManeuver <= instruction.TriggerDistance)
            {
                // Check if we haven't announced this exact instruction before
                var key = $"{instruction.StepIndex}_{instruction.TriggerDistance}";
                if (!session.AnnouncedInstructions.Contains(key))
                {
                    instruction.HasBeenAnnounced = true;
                    session.AnnouncedInstructions.Add(key);
                    instructionsToAnnounce.Add(instruction);
                }
            }
        }

        return instructionsToAnnounce.OrderByDescending(i => i.Priority).ToList();
    }

    /// <summary>
    /// Pause navigation
    /// </summary>
    public async Task PauseNavigationAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null && session.State == NavigationState.Navigating)
        {
            session.State = NavigationState.Paused;
            _logger?.LogInformation("Navigation paused: {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Resume navigation
    /// </summary>
    public async Task ResumeNavigationAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null && session.State == NavigationState.Paused)
        {
            session.State = NavigationState.Navigating;
            _logger?.LogInformation("Navigation resumed: {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Stop navigation
    /// </summary>
    public async Task StopNavigationAsync(string sessionId)
    {
        await _sessionsLock.WaitAsync();
        try
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.State = NavigationState.Cancelled;
                session.EndTime = DateTime.UtcNow;
                _activeSessions.Remove(sessionId);
                _logger?.LogInformation("Navigation stopped: {SessionId}", sessionId);
            }
        }
        finally
        {
            _sessionsLock.Release();
        }
    }

    /// <summary>
    /// Get active session
    /// </summary>
    public async Task<NavigationSession?> GetSessionAsync(string sessionId)
    {
        await _sessionsLock.WaitAsync();
        try
        {
            return _activeSessions.TryGetValue(sessionId, out var session) ? session : null;
        }
        finally
        {
            _sessionsLock.Release();
        }
    }

    /// <summary>
    /// Get all active sessions
    /// </summary>
    public async Task<List<NavigationSession>> GetActiveSessionsAsync()
    {
        await _sessionsLock.WaitAsync();
        try
        {
            return _activeSessions.Values.ToList();
        }
        finally
        {
            _sessionsLock.Release();
        }
    }

    /// <summary>
    /// Calculate ETA with current traffic
    /// </summary>
    public DateTime CalculateETA(NavigationSession session)
    {
        if (session.Route?.Traffic != null)
        {
            return DateTime.UtcNow.AddSeconds(session.Route.Traffic.DurationInTraffic);
        }
        return DateTime.UtcNow.AddSeconds(session.TotalTimeRemaining);
    }

    // Private helper methods

    private NavigationProgress CalculateProgress(NavigationSession session, double[] currentLocation)
    {
        if (session.Route == null || session.Route.Instructions.Count == 0)
        {
            return new NavigationProgress
            {
                SessionId = session.Id,
                ProgressPercentage = 0
            };
        }

        var currentStep = session.Route.Instructions
            .Skip(session.CurrentStepIndex)
            .FirstOrDefault();

        // Calculate distance to next maneuver
        var distanceToManeuver = 0.0;
        if (currentStep != null && currentStep.Coordinate.Length == 2)
        {
            distanceToManeuver = CalculateDistance(
                currentLocation[0], currentLocation[1],
                currentStep.Coordinate[0], currentStep.Coordinate[1]
            );
        }

        session.DistanceToNextManeuver = distanceToManeuver;

        // Check if we should advance to next step
        if (distanceToManeuver < 20 && session.CurrentStepIndex < session.Route.Instructions.Count - 1)
        {
            session.CurrentStepIndex++;
            _logger?.LogDebug("Advanced to step {Step} in session {SessionId}",
                session.CurrentStepIndex, session.Id);
        }

        // Calculate remaining distance and time
        var remainingDistance = session.Route.Instructions
            .Skip(session.CurrentStepIndex)
            .Sum(i => i.Distance);
        var remainingTime = session.Route.Instructions
            .Skip(session.CurrentStepIndex)
            .Sum(i => i.Time);

        session.TotalDistanceRemaining = remainingDistance;
        session.TotalTimeRemaining = remainingTime;
        session.EstimatedArrival = DateTime.UtcNow.AddSeconds(remainingTime);

        var totalDistance = session.Route.Distance;
        var progressPct = totalDistance > 0 ? ((totalDistance - remainingDistance) / totalDistance) * 100 : 0;

        var nextStep = session.Route.Instructions
            .Skip(session.CurrentStepIndex + 1)
            .FirstOrDefault();

        return new NavigationProgress
        {
            SessionId = session.Id,
            CurrentStepIndex = session.CurrentStepIndex,
            TotalSteps = session.Route.Instructions.Count,
            DistanceToNextManeuver = distanceToManeuver,
            DistanceRemaining = remainingDistance,
            TimeRemaining = remainingTime,
            CurrentInstruction = currentStep?.Text,
            NextInstruction = nextStep?.Text,
            CurrentSpeed = session.CurrentSpeed,
            CurrentHeading = session.CurrentHeading,
            EstimatedArrival = session.EstimatedArrival,
            ProgressPercentage = progressPct
        };
    }

    private bool CheckIfOffRoute(NavigationSession session, double[] location)
    {
        if (session.Route?.Geometry == null || session.Options == null)
        {
            return false;
        }

        // Simple implementation: check distance to current route segment
        // In production, you'd want a more sophisticated algorithm
        var currentStep = session.Route.Instructions
            .Skip(session.CurrentStepIndex)
            .FirstOrDefault();

        if (currentStep == null || currentStep.Coordinate.Length != 2)
        {
            return false;
        }

        var distanceFromRoute = CalculateDistance(
            location[0], location[1],
            currentStep.Coordinate[0], currentStep.Coordinate[1]
        );

        session.DistanceFromRoute = distanceFromRoute;

        return distanceFromRoute > session.Options.OffRouteThreshold;
    }

    private List<VoiceInstruction> GenerateVoiceInstructions(Route route, NavigationOptions options)
    {
        var instructions = new List<VoiceInstruction>();

        for (int i = 0; i < route.Instructions.Count; i++)
        {
            var step = route.Instructions[i];

            // Generate instructions at different distances
            var distances = GetInstructionDistances(step, options.Units);

            foreach (var distance in distances)
            {
                var text = FormatVoiceInstruction(step, distance, options.Units);
                instructions.Add(new VoiceInstruction
                {
                    StepIndex = i,
                    Text = text,
                    TriggerDistance = distance,
                    Priority = GetInstructionPriority(distance),
                    Type = VoiceInstructionType.Maneuver
                });
            }
        }

        return instructions;
    }

    private List<double> GetInstructionDistances(RouteInstruction step, Models.Routing.DistanceUnit units)
    {
        // Different distances for different maneuver types
        return step.Maneuver switch
        {
            ManeuverType.Depart => new List<double> { 0 },
            ManeuverType.Arrive => new List<double> { 100, 50 },
            _ => new List<double> { 500, 200, 100, 50 }
        };
    }

    private string FormatVoiceInstruction(RouteInstruction step, double distance, Models.Routing.DistanceUnit units)
    {
        if (distance == 0)
        {
            return step.Text;
        }

        var distanceText = FormatDistance(distance, units);
        var action = GetManeuverAction(step.Maneuver);

        if (!string.IsNullOrEmpty(step.StreetName))
        {
            return $"In {distanceText}, {action} onto {step.StreetName}";
        }

        return $"In {distanceText}, {action}";
    }

    private string GetManeuverAction(ManeuverType maneuver)
    {
        return maneuver switch
        {
            ManeuverType.TurnLeft => "turn left",
            ManeuverType.TurnRight => "turn right",
            ManeuverType.TurnSlightLeft => "turn slight left",
            ManeuverType.TurnSlightRight => "turn slight right",
            ManeuverType.TurnSharpLeft => "turn sharp left",
            ManeuverType.TurnSharpRight => "turn sharp right",
            ManeuverType.UTurn => "make a U-turn",
            ManeuverType.Straight or ManeuverType.Continue => "continue straight",
            ManeuverType.Merge => "merge",
            ManeuverType.Fork => "take the fork",
            ManeuverType.Roundabout => "enter the roundabout",
            ManeuverType.Arrive => "arrive at your destination",
            ManeuverType.Depart => "depart",
            _ => "continue"
        };
    }

    private string FormatDistance(double meters, DistanceUnit units)
    {
        if (units == DistanceUnit.Imperial)
        {
            var feet = meters * 3.28084;
            if (feet < 528) // Less than 0.1 mile
            {
                return $"{(int)feet} feet";
            }
            var miles = feet / 5280;
            return miles < 10 ? $"{miles:F1} miles" : $"{(int)miles} miles";
        }
        else
        {
            if (meters < 1000)
            {
                return $"{(int)meters} meters";
            }
            var km = meters / 1000;
            return km < 10 ? $"{km:F1} kilometers" : $"{(int)km} kilometers";
        }
    }

    private int GetInstructionPriority(double distance)
    {
        // Higher priority for closer instructions
        return distance switch
        {
            < 50 => 10,
            < 100 => 8,
            < 200 => 6,
            < 500 => 4,
            _ => 2
        };
    }

    private double CalculateDistance(double lon1, double lat1, double lon2, double lat2)
    {
        // Haversine formula
        const double R = 6371000; // Earth radius in meters
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private IRoutingService? GetRoutingProvider(string? providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return _routingProviders.Values.FirstOrDefault();
        }

        return _routingProviders.TryGetValue(providerName, out var provider) ? provider : null;
    }
}
