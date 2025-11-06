using System;

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Represents a waypoint in a route
/// </summary>
public class Waypoint
{
    /// <summary>
    /// Unique identifier for the waypoint
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Display name for the waypoint
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Address text
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Optional waypoint type (start, end, via)
    /// </summary>
    public WaypointType Type { get; set; } = WaypointType.Via;

    /// <summary>
    /// Label for the waypoint (A, B, C, etc.)
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Optional icon for the waypoint
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Whether to include this waypoint in the route calculation
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional arrival/departure time constraints
    /// </summary>
    public DateTime? TimeConstraint { get; set; }
}

/// <summary>
/// Type of waypoint
/// </summary>
public enum WaypointType
{
    /// <summary>
    /// Starting point
    /// </summary>
    Start,

    /// <summary>
    /// Intermediate waypoint
    /// </summary>
    Via,

    /// <summary>
    /// Ending point
    /// </summary>
    End
}
