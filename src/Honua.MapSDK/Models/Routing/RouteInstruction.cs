// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Represents a single turn-by-turn instruction in a route
/// </summary>
public class RouteInstruction
{
    /// <summary>
    /// Instruction text (e.g., "Turn left onto Main Street")
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Distance for this step in meters
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Time for this step in seconds
    /// </summary>
    public int Time { get; set; }

    /// <summary>
    /// Type of maneuver
    /// </summary>
    public ManeuverType Maneuver { get; set; }

    /// <summary>
    /// Icon identifier for the maneuver
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Coordinate where the maneuver occurs [longitude, latitude]
    /// </summary>
    public double[] Coordinate { get; set; } = new double[2];

    /// <summary>
    /// Street name
    /// </summary>
    public string? StreetName { get; set; }

    /// <summary>
    /// Modifier for the maneuver (left, right, slight left, etc.)
    /// </summary>
    public string? Modifier { get; set; }

    /// <summary>
    /// Exit number (for roundabouts, off-ramps)
    /// </summary>
    public int? ExitNumber { get; set; }

    /// <summary>
    /// Index of this instruction in the list
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Starting index in the route geometry for this instruction
    /// </summary>
    public int GeometryStartIndex { get; set; }

    /// <summary>
    /// Ending index in the route geometry for this instruction
    /// </summary>
    public int GeometryEndIndex { get; set; }
}

/// <summary>
/// Types of maneuvers/turns
/// </summary>
public enum ManeuverType
{
    /// <summary>
    /// Depart from origin
    /// </summary>
    Depart,

    /// <summary>
    /// Arrive at destination
    /// </summary>
    Arrive,

    /// <summary>
    /// Turn left
    /// </summary>
    TurnLeft,

    /// <summary>
    /// Turn right
    /// </summary>
    TurnRight,

    /// <summary>
    /// Turn slight left
    /// </summary>
    TurnSlightLeft,

    /// <summary>
    /// Turn slight right
    /// </summary>
    TurnSlightRight,

    /// <summary>
    /// Turn sharp left
    /// </summary>
    TurnSharpLeft,

    /// <summary>
    /// Turn sharp right
    /// </summary>
    TurnSharpRight,

    /// <summary>
    /// Make a U-turn
    /// </summary>
    UTurn,

    /// <summary>
    /// Continue straight
    /// </summary>
    Straight,

    /// <summary>
    /// Continue on current road
    /// </summary>
    Continue,

    /// <summary>
    /// Merge onto road
    /// </summary>
    Merge,

    /// <summary>
    /// Take fork
    /// </summary>
    Fork,

    /// <summary>
    /// Take on-ramp
    /// </summary>
    OnRamp,

    /// <summary>
    /// Take off-ramp
    /// </summary>
    OffRamp,

    /// <summary>
    /// Enter roundabout
    /// </summary>
    Roundabout,

    /// <summary>
    /// Turn left at roundabout
    /// </summary>
    RoundaboutLeft,

    /// <summary>
    /// Turn right at roundabout
    /// </summary>
    RoundaboutRight,

    /// <summary>
    /// Exit roundabout
    /// </summary>
    RoundaboutExit,

    /// <summary>
    /// End of road
    /// </summary>
    EndOfRoad,

    /// <summary>
    /// Ferry crossing
    /// </summary>
    Ferry,

    /// <summary>
    /// New name (road name changes)
    /// </summary>
    NewName,

    /// <summary>
    /// Notification only
    /// </summary>
    Notification
}
