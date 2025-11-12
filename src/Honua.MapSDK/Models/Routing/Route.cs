// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Represents a calculated route between waypoints
/// </summary>
public class Route
{
    /// <summary>
    /// Unique identifier for the route
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Waypoints that define the route
    /// </summary>
    public List<Waypoint> Waypoints { get; set; } = new();

    /// <summary>
    /// Route geometry as GeoJSON LineString
    /// </summary>
    public object Geometry { get; set; } = new { };

    /// <summary>
    /// Total distance in meters
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Total duration in seconds
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Turn-by-turn instructions
    /// </summary>
    public List<RouteInstruction> Instructions { get; set; } = new();

    /// <summary>
    /// Route summary information
    /// </summary>
    public RouteSummary Summary { get; set; } = new();

    /// <summary>
    /// Travel mode used for routing
    /// </summary>
    public TravelMode TravelMode { get; set; }

    /// <summary>
    /// Whether this is an alternative route
    /// </summary>
    public bool IsAlternative { get; set; }

    /// <summary>
    /// Alternative route index (0 = primary, 1+ = alternatives)
    /// </summary>
    public int AlternativeIndex { get; set; }

    /// <summary>
    /// Routing engine that calculated this route
    /// </summary>
    public string? RoutingEngine { get; set; }

    /// <summary>
    /// When the route was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Traffic information if available
    /// </summary>
    public TrafficInfo? Traffic { get; set; }

    /// <summary>
    /// Legs of the route (segments between waypoints)
    /// </summary>
    public List<RouteLeg> Legs { get; set; } = new();
}

/// <summary>
/// Summary information for a route
/// </summary>
public class RouteSummary
{
    /// <summary>
    /// Total distance formatted for display (e.g., "12.5 km")
    /// </summary>
    public string FormattedDistance { get; set; } = string.Empty;

    /// <summary>
    /// Total duration formatted for display (e.g., "25 min")
    /// </summary>
    public string FormattedDuration { get; set; } = string.Empty;

    /// <summary>
    /// Main roads/highways used
    /// </summary>
    public List<string> MainRoads { get; set; } = new();

    /// <summary>
    /// Toll cost if applicable
    /// </summary>
    public decimal? TollCost { get; set; }

    /// <summary>
    /// Estimated fuel consumption in liters
    /// </summary>
    public double? FuelConsumption { get; set; }

    /// <summary>
    /// Estimated CO2 emissions in kg
    /// </summary>
    public double? Co2Emissions { get; set; }

    /// <summary>
    /// Average speed in km/h
    /// </summary>
    public double? AverageSpeed { get; set; }

    /// <summary>
    /// Estimated arrival time
    /// </summary>
    public DateTime? EstimatedArrivalTime { get; set; }
}

/// <summary>
/// Represents a leg of the route (segment between two waypoints)
/// </summary>
public class RouteLeg
{
    /// <summary>
    /// Starting waypoint
    /// </summary>
    public Waypoint? StartWaypoint { get; set; }

    /// <summary>
    /// Ending waypoint
    /// </summary>
    public Waypoint? EndWaypoint { get; set; }

    /// <summary>
    /// Distance for this leg in meters
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Duration for this leg in seconds
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Instructions for this leg
    /// </summary>
    public List<RouteInstruction> Instructions { get; set; } = new();
}

/// <summary>
/// Traffic information for a route
/// </summary>
public class TrafficInfo
{
    /// <summary>
    /// Overall traffic condition
    /// </summary>
    public TrafficCondition Condition { get; set; }

    /// <summary>
    /// Duration with current traffic in seconds
    /// </summary>
    public int DurationInTraffic { get; set; }

    /// <summary>
    /// Time saved/lost compared to typical duration
    /// </summary>
    public int TimeDelta { get; set; }

    /// <summary>
    /// Traffic delays along the route
    /// </summary>
    public List<TrafficDelay> Delays { get; set; } = new();
}

/// <summary>
/// Represents a traffic delay on a route segment
/// </summary>
public class TrafficDelay
{
    /// <summary>
    /// Location of the delay [longitude, latitude]
    /// </summary>
    public double[] Location { get; set; } = new double[2];

    /// <summary>
    /// Delay duration in seconds
    /// </summary>
    public int DelaySeconds { get; set; }

    /// <summary>
    /// Reason for the delay
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Severity of the delay
    /// </summary>
    public TrafficSeverity Severity { get; set; }
}

/// <summary>
/// Overall traffic condition
/// </summary>
public enum TrafficCondition
{
    /// <summary>
    /// Unknown traffic condition
    /// </summary>
    Unknown,

    /// <summary>
    /// Free-flowing traffic
    /// </summary>
    Clear,

    /// <summary>
    /// Light traffic
    /// </summary>
    Light,

    /// <summary>
    /// Moderate traffic
    /// </summary>
    Moderate,

    /// <summary>
    /// Heavy traffic
    /// </summary>
    Heavy,

    /// <summary>
    /// Severe congestion
    /// </summary>
    Severe
}

/// <summary>
/// Traffic delay severity
/// </summary>
public enum TrafficSeverity
{
    /// <summary>
    /// Minor delay
    /// </summary>
    Minor,

    /// <summary>
    /// Moderate delay
    /// </summary>
    Moderate,

    /// <summary>
    /// Major delay
    /// </summary>
    Major,

    /// <summary>
    /// Severe delay or closure
    /// </summary>
    Severe
}
