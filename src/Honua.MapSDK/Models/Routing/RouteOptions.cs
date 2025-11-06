using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Options for route calculation
/// </summary>
public class RouteOptions
{
    /// <summary>
    /// Travel mode for routing
    /// </summary>
    public TravelMode TravelMode { get; set; } = TravelMode.Driving;

    /// <summary>
    /// Route preference
    /// </summary>
    public RoutePreference Preference { get; set; } = RoutePreference.Fastest;

    /// <summary>
    /// Items to avoid on the route
    /// </summary>
    public List<AvoidOption> Avoid { get; set; } = new();

    /// <summary>
    /// Maximum number of alternative routes to return
    /// </summary>
    public int MaxAlternatives { get; set; } = 0;

    /// <summary>
    /// Whether to include turn-by-turn instructions
    /// </summary>
    public bool IncludeInstructions { get; set; } = true;

    /// <summary>
    /// Whether to include traffic information
    /// </summary>
    public bool IncludeTraffic { get; set; } = false;

    /// <summary>
    /// Departure time (for traffic-aware routing)
    /// </summary>
    public DateTime? DepartureTime { get; set; }

    /// <summary>
    /// Language for instructions (ISO 639-1 code)
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Units for distance (metric or imperial)
    /// </summary>
    public DistanceUnit Units { get; set; } = DistanceUnit.Metric;

    /// <summary>
    /// Whether to continue straight at waypoints or stop
    /// </summary>
    public bool ContinueStraight { get; set; } = true;

    /// <summary>
    /// Geometry format (geojson, polyline, polyline6)
    /// </summary>
    public string GeometryFormat { get; set; } = "geojson";

    /// <summary>
    /// Overview level (full, simplified, none)
    /// </summary>
    public string Overview { get; set; } = "full";

    /// <summary>
    /// Annotations to include (duration, distance, speed, etc.)
    /// </summary>
    public List<string> Annotations { get; set; } = new();

    /// <summary>
    /// Additional custom parameters for specific routing engines
    /// </summary>
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Travel mode for routing
/// </summary>
public enum TravelMode
{
    /// <summary>
    /// Driving by car
    /// </summary>
    Driving,

    /// <summary>
    /// Walking on foot
    /// </summary>
    Walking,

    /// <summary>
    /// Cycling by bicycle
    /// </summary>
    Cycling,

    /// <summary>
    /// Public transit
    /// </summary>
    Transit,

    /// <summary>
    /// Driving with traffic
    /// </summary>
    DrivingTraffic,

    /// <summary>
    /// Motorcycle
    /// </summary>
    Motorcycle,

    /// <summary>
    /// Truck/HGV
    /// </summary>
    Truck,

    /// <summary>
    /// Electric vehicle
    /// </summary>
    ElectricVehicle
}

/// <summary>
/// Route preference/optimization
/// </summary>
public enum RoutePreference
{
    /// <summary>
    /// Fastest route by time
    /// </summary>
    Fastest,

    /// <summary>
    /// Shortest route by distance
    /// </summary>
    Shortest,

    /// <summary>
    /// Most scenic route
    /// </summary>
    Scenic,

    /// <summary>
    /// Recommended route (balance of time, distance, and experience)
    /// </summary>
    Recommended,

    /// <summary>
    /// Most fuel-efficient route
    /// </summary>
    Efficient
}

/// <summary>
/// Items to avoid when routing
/// </summary>
public enum AvoidOption
{
    /// <summary>
    /// Avoid toll roads
    /// </summary>
    Tolls,

    /// <summary>
    /// Avoid highways/motorways
    /// </summary>
    Highways,

    /// <summary>
    /// Avoid ferries
    /// </summary>
    Ferries,

    /// <summary>
    /// Avoid unpaved roads
    /// </summary>
    Unpaved,

    /// <summary>
    /// Avoid tunnels
    /// </summary>
    Tunnels,

    /// <summary>
    /// Avoid bridges
    /// </summary>
    Bridges,

    /// <summary>
    /// Avoid U-turns
    /// </summary>
    UTurns,

    /// <summary>
    /// Avoid steps (for cycling/wheelchair)
    /// </summary>
    Steps,

    /// <summary>
    /// Avoid high traffic areas
    /// </summary>
    HighTraffic
}

/// <summary>
/// Distance units
/// </summary>
public enum DistanceUnit
{
    /// <summary>
    /// Metric system (km, m)
    /// </summary>
    Metric,

    /// <summary>
    /// Imperial system (mi, ft)
    /// </summary>
    Imperial
}

/// <summary>
/// Routing engine providers
/// </summary>
public enum RoutingEngine
{
    /// <summary>
    /// OSRM (Open Source Routing Machine)
    /// </summary>
    OSRM,

    /// <summary>
    /// Mapbox Directions API
    /// </summary>
    Mapbox,

    /// <summary>
    /// GraphHopper
    /// </summary>
    GraphHopper,

    /// <summary>
    /// OpenRouteService
    /// </summary>
    OpenRouteService,

    /// <summary>
    /// Valhalla
    /// </summary>
    Valhalla,

    /// <summary>
    /// Custom routing service
    /// </summary>
    Custom
}
