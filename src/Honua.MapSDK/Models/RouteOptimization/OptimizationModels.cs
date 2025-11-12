// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models.RouteOptimization;

/// <summary>
/// Request for multi-stop route optimization (TSP/VRP)
/// </summary>
public class OptimizationRequest
{
    /// <summary>
    /// List of waypoints to optimize
    /// </summary>
    public List<OptimizationWaypoint> Waypoints { get; set; } = new();

    /// <summary>
    /// Starting location (depot/origin). If null, uses first waypoint.
    /// </summary>
    public Coordinate? StartLocation { get; set; }

    /// <summary>
    /// Ending location (depot return). If null, route ends at last waypoint.
    /// </summary>
    public Coordinate? EndLocation { get; set; }

    /// <summary>
    /// Optimization goal/objective
    /// </summary>
    public OptimizationGoal Goal { get; set; } = OptimizationGoal.MinimizeDistance;

    /// <summary>
    /// Vehicle constraints for VRP
    /// </summary>
    public VehicleConstraints? Vehicle { get; set; }

    /// <summary>
    /// Travel mode for routing
    /// </summary>
    public string TravelMode { get; set; } = "driving";

    /// <summary>
    /// Whether to enable time window constraints
    /// </summary>
    public bool EnableTimeWindows { get; set; }

    /// <summary>
    /// Departure time from start location
    /// </summary>
    public DateTime? DepartureTime { get; set; }

    /// <summary>
    /// Maximum computation time in seconds
    /// </summary>
    public int MaxComputationTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to return multiple vehicle routes (multi-VRP)
    /// </summary>
    public bool MultipleVehicles { get; set; }

    /// <summary>
    /// Number of vehicles (for multi-VRP)
    /// </summary>
    public int NumberOfVehicles { get; set; } = 1;
}

/// <summary>
/// Waypoint in an optimization problem
/// </summary>
public class OptimizationWaypoint
{
    /// <summary>
    /// Unique identifier for the waypoint
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Location of the waypoint
    /// </summary>
    public required Coordinate Location { get; set; }

    /// <summary>
    /// Name/label for the waypoint
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Service/stop duration at this waypoint (seconds)
    /// </summary>
    public int ServiceDurationSeconds { get; set; }

    /// <summary>
    /// Time window constraints
    /// </summary>
    public TimeWindow? TimeWindow { get; set; }

    /// <summary>
    /// Priority of this waypoint (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Demand/load at this waypoint (for capacity constraints)
    /// </summary>
    public double Demand { get; set; }

    /// <summary>
    /// Whether this waypoint is required (if false, can be skipped)
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Original sequence index (before optimization)
    /// </summary>
    public int OriginalIndex { get; set; }
}

/// <summary>
/// Coordinate (latitude, longitude)
/// </summary>
public record Coordinate(double Latitude, double Longitude)
{
    /// <summary>
    /// Convert to [longitude, latitude] array for GeoJSON
    /// </summary>
    public double[] ToLonLatArray() => new[] { Longitude, Latitude };

    /// <summary>
    /// Create from [longitude, latitude] array
    /// </summary>
    public static Coordinate FromLonLatArray(double[] coords) => new(coords[1], coords[0]);
}

/// <summary>
/// Time window constraint for a waypoint
/// </summary>
public class TimeWindow
{
    /// <summary>
    /// Earliest acceptable arrival time
    /// </summary>
    public DateTime? EarliestArrival { get; set; }

    /// <summary>
    /// Latest acceptable arrival time
    /// </summary>
    public DateTime? LatestArrival { get; set; }

    /// <summary>
    /// Whether early arrival is allowed (vehicle waits)
    /// </summary>
    public bool AllowEarlyArrival { get; set; } = true;

    /// <summary>
    /// Whether late arrival is allowed (soft constraint)
    /// </summary>
    public bool AllowLateArrival { get; set; } = false;

    /// <summary>
    /// Penalty for late arrival (per second)
    /// </summary>
    public double LatePenalty { get; set; } = 1.0;

    /// <summary>
    /// Check if time is within window
    /// </summary>
    public bool IsWithinWindow(DateTime time)
    {
        if (EarliestArrival.HasValue && time < EarliestArrival.Value && !AllowEarlyArrival)
            return false;
        if (LatestArrival.HasValue && time > LatestArrival.Value && !AllowLateArrival)
            return false;
        return true;
    }
}

/// <summary>
/// Vehicle constraints for routing
/// </summary>
public class VehicleConstraints
{
    /// <summary>
    /// Maximum capacity/load
    /// </summary>
    public double MaxCapacity { get; set; }

    /// <summary>
    /// Maximum route duration in seconds
    /// </summary>
    public int? MaxDurationSeconds { get; set; }

    /// <summary>
    /// Maximum route distance in meters
    /// </summary>
    public double? MaxDistanceMeters { get; set; }

    /// <summary>
    /// Maximum number of stops
    /// </summary>
    public int? MaxStops { get; set; }

    /// <summary>
    /// Vehicle speed (m/s) for time estimation
    /// </summary>
    public double? AverageSpeedMps { get; set; }

    /// <summary>
    /// Cost per meter traveled
    /// </summary>
    public double CostPerMeter { get; set; } = 0.001;

    /// <summary>
    /// Cost per second of time
    /// </summary>
    public double CostPerSecond { get; set; } = 0.01;

    /// <summary>
    /// Fixed cost per vehicle used
    /// </summary>
    public double FixedCost { get; set; }
}

/// <summary>
/// Result of route optimization
/// </summary>
public class OptimizationResult
{
    /// <summary>
    /// Optimized sequence of waypoints
    /// </summary>
    public List<OptimizationWaypoint> OptimizedSequence { get; set; } = new();

    /// <summary>
    /// Original sequence (before optimization)
    /// </summary>
    public List<OptimizationWaypoint> OriginalSequence { get; set; } = new();

    /// <summary>
    /// Routes (for multi-vehicle VRP)
    /// </summary>
    public List<VehicleRoute> Routes { get; set; } = new();

    /// <summary>
    /// Optimization metrics
    /// </summary>
    public OptimizationMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Algorithm used for optimization
    /// </summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Provider used (Mapbox, OSRM, ClientSide, etc.)
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Computation time in milliseconds
    /// </summary>
    public long ComputationTimeMs { get; set; }

    /// <summary>
    /// Whether the solution is optimal or heuristic
    /// </summary>
    public bool IsOptimal { get; set; }

    /// <summary>
    /// Quality indicator (0-100, higher is better)
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Waypoints that were skipped (if allowed)
    /// </summary>
    public List<OptimizationWaypoint> SkippedWaypoints { get; set; } = new();

    /// <summary>
    /// Detailed route geometry (GeoJSON)
    /// </summary>
    public object? RouteGeometry { get; set; }

    /// <summary>
    /// Time window violations
    /// </summary>
    public List<TimeWindowViolation> TimeWindowViolations { get; set; } = new();

    /// <summary>
    /// Warnings or notes about the optimization
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Route for a single vehicle (VRP)
/// </summary>
public class VehicleRoute
{
    /// <summary>
    /// Vehicle identifier
    /// </summary>
    public int VehicleId { get; set; }

    /// <summary>
    /// Waypoints assigned to this vehicle
    /// </summary>
    public List<OptimizationWaypoint> Waypoints { get; set; } = new();

    /// <summary>
    /// Total distance for this route (meters)
    /// </summary>
    public double TotalDistanceMeters { get; set; }

    /// <summary>
    /// Total duration for this route (seconds)
    /// </summary>
    public int TotalDurationSeconds { get; set; }

    /// <summary>
    /// Total load for this route
    /// </summary>
    public double TotalLoad { get; set; }

    /// <summary>
    /// Route geometry (GeoJSON)
    /// </summary>
    public object? Geometry { get; set; }

    /// <summary>
    /// Estimated arrival times at each waypoint
    /// </summary>
    public List<DateTime> ArrivalTimes { get; set; } = new();
}

/// <summary>
/// Metrics for optimization result
/// </summary>
public class OptimizationMetrics
{
    /// <summary>
    /// Total distance (original route)
    /// </summary>
    public double OriginalDistanceMeters { get; set; }

    /// <summary>
    /// Total distance (optimized route)
    /// </summary>
    public double OptimizedDistanceMeters { get; set; }

    /// <summary>
    /// Distance saved (meters)
    /// </summary>
    public double DistanceSavedMeters => OriginalDistanceMeters - OptimizedDistanceMeters;

    /// <summary>
    /// Distance improvement percentage
    /// </summary>
    public double DistanceSavingsPercent =>
        OriginalDistanceMeters > 0 ? (DistanceSavedMeters / OriginalDistanceMeters) * 100 : 0;

    /// <summary>
    /// Total duration (original route, seconds)
    /// </summary>
    public int OriginalDurationSeconds { get; set; }

    /// <summary>
    /// Total duration (optimized route, seconds)
    /// </summary>
    public int OptimizedDurationSeconds { get; set; }

    /// <summary>
    /// Duration saved (seconds)
    /// </summary>
    public int DurationSavedSeconds => OriginalDurationSeconds - OptimizedDurationSeconds;

    /// <summary>
    /// Duration improvement percentage
    /// </summary>
    public double DurationSavingsPercent =>
        OriginalDurationSeconds > 0 ? ((double)DurationSavedSeconds / OriginalDurationSeconds) * 100 : 0;

    /// <summary>
    /// Total cost (original route)
    /// </summary>
    public double OriginalCost { get; set; }

    /// <summary>
    /// Total cost (optimized route)
    /// </summary>
    public double OptimizedCost { get; set; }

    /// <summary>
    /// Cost saved
    /// </summary>
    public double CostSaved => OriginalCost - OptimizedCost;

    /// <summary>
    /// Cost improvement percentage
    /// </summary>
    public double CostSavingsPercent =>
        OriginalCost > 0 ? (CostSaved / OriginalCost) * 100 : 0;

    /// <summary>
    /// Number of stops (original)
    /// </summary>
    public int OriginalStopCount { get; set; }

    /// <summary>
    /// Number of stops (optimized)
    /// </summary>
    public int OptimizedStopCount { get; set; }

    /// <summary>
    /// Number of vehicles used
    /// </summary>
    public int VehiclesUsed { get; set; } = 1;
}

/// <summary>
/// Time window violation
/// </summary>
public class TimeWindowViolation
{
    /// <summary>
    /// Waypoint with violation
    /// </summary>
    public required OptimizationWaypoint Waypoint { get; set; }

    /// <summary>
    /// Expected arrival time
    /// </summary>
    public DateTime ArrivalTime { get; set; }

    /// <summary>
    /// How early (negative) or late (positive) in seconds
    /// </summary>
    public int ViolationSeconds { get; set; }

    /// <summary>
    /// Type of violation
    /// </summary>
    public ViolationType Type { get; set; }
}

/// <summary>
/// Type of time window violation
/// </summary>
public enum ViolationType
{
    /// <summary>
    /// Arrived too early
    /// </summary>
    TooEarly,

    /// <summary>
    /// Arrived too late
    /// </summary>
    TooLate
}

/// <summary>
/// Optimization goal/objective
/// </summary>
public enum OptimizationGoal
{
    /// <summary>
    /// Minimize total distance
    /// </summary>
    MinimizeDistance,

    /// <summary>
    /// Minimize total time
    /// </summary>
    MinimizeTime,

    /// <summary>
    /// Minimize total cost
    /// </summary>
    MinimizeCost,

    /// <summary>
    /// Balance distance and time
    /// </summary>
    Balanced,

    /// <summary>
    /// Maximize served waypoints (for optional stops)
    /// </summary>
    MaximizeServed
}

/// <summary>
/// Route optimization provider
/// </summary>
public enum OptimizationProvider
{
    /// <summary>
    /// Client-side algorithms (Nearest Neighbor, 2-opt)
    /// </summary>
    ClientSide,

    /// <summary>
    /// Mapbox Optimization API
    /// </summary>
    Mapbox,

    /// <summary>
    /// OSRM Trip endpoint
    /// </summary>
    OSRM,

    /// <summary>
    /// GraphHopper Route Optimization
    /// </summary>
    GraphHopper,

    /// <summary>
    /// Valhalla Optimized Route
    /// </summary>
    Valhalla,

    /// <summary>
    /// Custom optimization service
    /// </summary>
    Custom
}
