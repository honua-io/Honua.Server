// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Honua.MapSDK.Models.Routing;

namespace Honua.MapSDK.Models.Navigation;

/// <summary>
/// Represents a navigation session with turn-by-turn guidance
/// </summary>
public class NavigationSession
{
    /// <summary>
    /// Unique identifier for the navigation session
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The route being navigated
    /// </summary>
    public Route? Route { get; set; }

    /// <summary>
    /// Current navigation state
    /// </summary>
    public NavigationState State { get; set; } = NavigationState.NotStarted;

    /// <summary>
    /// Current step index in the route
    /// </summary>
    public int CurrentStepIndex { get; set; } = 0;

    /// <summary>
    /// Current location [longitude, latitude]
    /// </summary>
    public double[] CurrentLocation { get; set; } = new double[2];

    /// <summary>
    /// Current heading/bearing in degrees (0-360)
    /// </summary>
    public double? CurrentHeading { get; set; }

    /// <summary>
    /// Current speed in meters per second
    /// </summary>
    public double? CurrentSpeed { get; set; }

    /// <summary>
    /// Distance to next maneuver in meters
    /// </summary>
    public double DistanceToNextManeuver { get; set; }

    /// <summary>
    /// Total distance remaining in meters
    /// </summary>
    public double TotalDistanceRemaining { get; set; }

    /// <summary>
    /// Total time remaining in seconds
    /// </summary>
    public int TotalTimeRemaining { get; set; }

    /// <summary>
    /// Estimated time of arrival
    /// </summary>
    public DateTime? EstimatedArrival { get; set; }

    /// <summary>
    /// Whether user is off the route
    /// </summary>
    public bool IsOffRoute { get; set; }

    /// <summary>
    /// Distance from route in meters (when off-route)
    /// </summary>
    public double? DistanceFromRoute { get; set; }

    /// <summary>
    /// Last reroute timestamp
    /// </summary>
    public DateTime? LastRerouteTime { get; set; }

    /// <summary>
    /// Number of reroutes in this session
    /// </summary>
    public int RerouteCount { get; set; }

    /// <summary>
    /// Navigation options
    /// </summary>
    public NavigationOptions? Options { get; set; }

    /// <summary>
    /// Session start time
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session end time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total distance traveled in meters
    /// </summary>
    public double TotalDistanceTraveled { get; set; }

    /// <summary>
    /// Voice instructions queue
    /// </summary>
    public List<VoiceInstruction> VoiceInstructions { get; set; } = new();

    /// <summary>
    /// Previously announced instructions (to avoid repeating)
    /// </summary>
    public HashSet<string> AnnouncedInstructions { get; set; } = new();
}

/// <summary>
/// Navigation state
/// </summary>
public enum NavigationState
{
    /// <summary>
    /// Navigation has not started yet
    /// </summary>
    NotStarted,

    /// <summary>
    /// Actively navigating
    /// </summary>
    Navigating,

    /// <summary>
    /// Paused navigation
    /// </summary>
    Paused,

    /// <summary>
    /// Off route, waiting for reroute
    /// </summary>
    OffRoute,

    /// <summary>
    /// Rerouting in progress
    /// </summary>
    Rerouting,

    /// <summary>
    /// Arrived at destination
    /// </summary>
    Arrived,

    /// <summary>
    /// Navigation cancelled
    /// </summary>
    Cancelled,

    /// <summary>
    /// Navigation failed
    /// </summary>
    Failed
}

/// <summary>
/// Navigation configuration options
/// </summary>
public class NavigationOptions
{
    /// <summary>
    /// Enable voice guidance
    /// </summary>
    public bool EnableVoiceGuidance { get; set; } = true;

    /// <summary>
    /// Enable automatic rerouting when off-route
    /// </summary>
    public bool EnableRerouting { get; set; } = true;

    /// <summary>
    /// Show lane guidance
    /// </summary>
    public bool ShowLaneGuidance { get; set; } = true;

    /// <summary>
    /// Camera follow mode (auto-follow user)
    /// </summary>
    public bool CameraFollowMode { get; set; } = true;

    /// <summary>
    /// Camera zoom level during navigation
    /// </summary>
    public double NavigationZoomLevel { get; set; } = 16.0;

    /// <summary>
    /// Camera pitch/tilt in degrees
    /// </summary>
    public double CameraPitch { get; set; } = 45.0;

    /// <summary>
    /// Maximum distance from route before triggering reroute (meters)
    /// </summary>
    public double OffRouteThreshold { get; set; } = 50.0;

    /// <summary>
    /// Minimum time between reroutes (seconds)
    /// </summary>
    public int MinRerouteInterval { get; set; } = 3;

    /// <summary>
    /// Distance units for voice guidance
    /// </summary>
    public DistanceUnit Units { get; set; } = DistanceUnit.Metric;

    /// <summary>
    /// Voice guidance language (ISO 639-1 code)
    /// </summary>
    public string VoiceLanguage { get; set; } = "en";

    /// <summary>
    /// Voice guidance volume (0.0 to 1.0)
    /// </summary>
    public double VoiceVolume { get; set; } = 1.0;

    /// <summary>
    /// Voice guidance speech rate (0.5 to 2.0)
    /// </summary>
    public double VoiceSpeechRate { get; set; } = 1.0;

    /// <summary>
    /// Voice name/identifier
    /// </summary>
    public string? VoiceName { get; set; }

    /// <summary>
    /// Show speed limit warnings
    /// </summary>
    public bool ShowSpeedLimits { get; set; } = true;

    /// <summary>
    /// Simulate location (for testing)
    /// </summary>
    public bool SimulateLocation { get; set; } = false;

    /// <summary>
    /// Simulation speed multiplier
    /// </summary>
    public double SimulationSpeedMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Keep screen awake during navigation
    /// </summary>
    public bool KeepScreenAwake { get; set; } = true;
}

/// <summary>
/// Voice instruction with distance-based triggering
/// </summary>
public class VoiceInstruction
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Related step index
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    /// Instruction text to speak
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Distance from maneuver when this should be announced (meters)
    /// </summary>
    public double TriggerDistance { get; set; }

    /// <summary>
    /// Priority level (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether this instruction has been announced
    /// </summary>
    public bool HasBeenAnnounced { get; set; } = false;

    /// <summary>
    /// SSML markup for advanced speech synthesis (optional)
    /// </summary>
    public string? SsmlText { get; set; }

    /// <summary>
    /// Type of instruction
    /// </summary>
    public VoiceInstructionType Type { get; set; } = VoiceInstructionType.Maneuver;
}

/// <summary>
/// Type of voice instruction
/// </summary>
public enum VoiceInstructionType
{
    /// <summary>
    /// Turn/maneuver instruction
    /// </summary>
    Maneuver,

    /// <summary>
    /// Distance update
    /// </summary>
    DistanceUpdate,

    /// <summary>
    /// Off-route warning
    /// </summary>
    OffRoute,

    /// <summary>
    /// Arrival announcement
    /// </summary>
    Arrival,

    /// <summary>
    /// Speed limit warning
    /// </summary>
    SpeedLimit,

    /// <summary>
    /// General notification
    /// </summary>
    Notification
}

/// <summary>
/// Lane guidance information for a maneuver
/// </summary>
public class LaneGuidance
{
    /// <summary>
    /// Lanes available at this maneuver
    /// </summary>
    public List<Lane> Lanes { get; set; } = new();

    /// <summary>
    /// Indices of lanes to use for the maneuver
    /// </summary>
    public List<int> ActiveLanes { get; set; } = new();

    /// <summary>
    /// Text description of lane guidance
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Information about a single lane
/// </summary>
public class Lane
{
    /// <summary>
    /// Valid directions for this lane
    /// </summary>
    public List<LaneDirection> Directions { get; set; } = new();

    /// <summary>
    /// Whether this lane is active (recommended) for the maneuver
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Lane type (e.g., turn lane, through lane)
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Lane direction indicators
/// </summary>
public enum LaneDirection
{
    /// <summary>
    /// Lane goes straight
    /// </summary>
    Straight,

    /// <summary>
    /// Lane turns left
    /// </summary>
    Left,

    /// <summary>
    /// Lane turns right
    /// </summary>
    Right,

    /// <summary>
    /// Lane turns slight left
    /// </summary>
    SlightLeft,

    /// <summary>
    /// Lane turns slight right
    /// </summary>
    SlightRight,

    /// <summary>
    /// Lane turns sharp left
    /// </summary>
    SharpLeft,

    /// <summary>
    /// Lane turns sharp right
    /// </summary>
    SharpRight,

    /// <summary>
    /// U-turn lane
    /// </summary>
    UTurn,

    /// <summary>
    /// Merge lane
    /// </summary>
    Merge
}

/// <summary>
/// Navigation step with enhanced information
/// </summary>
public class NavigationStep
{
    /// <summary>
    /// Original route instruction
    /// </summary>
    public required RouteInstruction Instruction { get; set; }

    /// <summary>
    /// Lane guidance for this step
    /// </summary>
    public LaneGuidance? LaneGuidance { get; set; }

    /// <summary>
    /// Voice instructions for this step
    /// </summary>
    public List<VoiceInstruction> VoiceInstructions { get; set; } = new();

    /// <summary>
    /// Current street name
    /// </summary>
    public string? CurrentStreet { get; set; }

    /// <summary>
    /// Next street name
    /// </summary>
    public string? NextStreet { get; set; }

    /// <summary>
    /// Speed limit on this segment (km/h)
    /// </summary>
    public int? SpeedLimit { get; set; }

    /// <summary>
    /// Whether this is a highway/motorway segment
    /// </summary>
    public bool IsHighway { get; set; }

    /// <summary>
    /// Road class (motorway, primary, secondary, etc.)
    /// </summary>
    public string? RoadClass { get; set; }

    /// <summary>
    /// Exit number (for highway exits)
    /// </summary>
    public string? ExitNumber { get; set; }

    /// <summary>
    /// Signposted destinations
    /// </summary>
    public List<string> Destinations { get; set; } = new();
}

/// <summary>
/// Progress update event data
/// </summary>
public class NavigationProgress
{
    /// <summary>
    /// Navigation session ID
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Current step index
    /// </summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Total steps in route
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Distance to next maneuver (meters)
    /// </summary>
    public double DistanceToNextManeuver { get; set; }

    /// <summary>
    /// Distance remaining (meters)
    /// </summary>
    public double DistanceRemaining { get; set; }

    /// <summary>
    /// Time remaining (seconds)
    /// </summary>
    public int TimeRemaining { get; set; }

    /// <summary>
    /// Current instruction
    /// </summary>
    public string? CurrentInstruction { get; set; }

    /// <summary>
    /// Next instruction
    /// </summary>
    public string? NextInstruction { get; set; }

    /// <summary>
    /// Current speed (m/s)
    /// </summary>
    public double? CurrentSpeed { get; set; }

    /// <summary>
    /// Speed limit (km/h)
    /// </summary>
    public int? SpeedLimit { get; set; }

    /// <summary>
    /// Current heading (degrees)
    /// </summary>
    public double? CurrentHeading { get; set; }

    /// <summary>
    /// Estimated arrival time
    /// </summary>
    public DateTime? EstimatedArrival { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage { get; set; }
}

/// <summary>
/// Navigation event types
/// </summary>
public enum NavigationEventType
{
    /// <summary>
    /// Navigation started
    /// </summary>
    Started,

    /// <summary>
    /// Progress update
    /// </summary>
    ProgressUpdate,

    /// <summary>
    /// Step changed
    /// </summary>
    StepChanged,

    /// <summary>
    /// Off route detected
    /// </summary>
    OffRoute,

    /// <summary>
    /// Rerouting started
    /// </summary>
    ReroutingStarted,

    /// <summary>
    /// Rerouting completed
    /// </summary>
    ReroutingCompleted,

    /// <summary>
    /// Rerouting failed
    /// </summary>
    ReroutingFailed,

    /// <summary>
    /// Arrived at destination
    /// </summary>
    Arrived,

    /// <summary>
    /// Navigation paused
    /// </summary>
    Paused,

    /// <summary>
    /// Navigation resumed
    /// </summary>
    Resumed,

    /// <summary>
    /// Navigation cancelled
    /// </summary>
    Cancelled,

    /// <summary>
    /// Faster route found
    /// </summary>
    FasterRouteFound,

    /// <summary>
    /// Voice instruction announced
    /// </summary>
    VoiceInstruction,

    /// <summary>
    /// Speed limit exceeded
    /// </summary>
    SpeedLimitExceeded
}

/// <summary>
/// Routing provider configuration for navigation
/// </summary>
public class NavigationProvider
{
    /// <summary>
    /// Provider name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Provider base URL
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// API key (if required)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Supported travel modes
    /// </summary>
    public List<TravelMode> SupportedModes { get; set; } = new();

    /// <summary>
    /// Supports traffic data
    /// </summary>
    public bool SupportsTraffic { get; set; }

    /// <summary>
    /// Supports lane guidance
    /// </summary>
    public bool SupportsLaneGuidance { get; set; }

    /// <summary>
    /// Supports voice instructions
    /// </summary>
    public bool SupportsVoiceInstructions { get; set; }

    /// <summary>
    /// Maximum requests per minute
    /// </summary>
    public int? RateLimit { get; set; }
}
