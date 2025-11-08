// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.LocationServices.Models;

/// <summary>
/// Request for calculating a route between locations.
/// </summary>
public sealed record RoutingRequest
{
    /// <summary>
    /// List of waypoints [longitude, latitude] defining the route.
    /// Must have at least 2 waypoints (start and end).
    /// </summary>
    public required IReadOnlyList<double[]> Waypoints { get; init; }

    /// <summary>
    /// Travel mode (e.g., "car", "truck", "bicycle", "pedestrian").
    /// </summary>
    public string TravelMode { get; init; } = "car";

    /// <summary>
    /// Whether to avoid tolls.
    /// </summary>
    public bool AvoidTolls { get; init; }

    /// <summary>
    /// Whether to avoid highways/motorways.
    /// </summary>
    public bool AvoidHighways { get; init; }

    /// <summary>
    /// Whether to avoid ferries.
    /// </summary>
    public bool AvoidFerries { get; init; }

    /// <summary>
    /// Whether to include traffic information in the route.
    /// </summary>
    public bool UseTraffic { get; init; }

    /// <summary>
    /// Departure time for traffic-aware routing.
    /// </summary>
    public DateTimeOffset? DepartureTime { get; init; }

    /// <summary>
    /// Vehicle specifications for truck routing.
    /// </summary>
    public VehicleSpecifications? Vehicle { get; init; }

    /// <summary>
    /// Optional language code for instructions (ISO 639-1).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Unit system for distances ("metric" or "imperial").
    /// </summary>
    public string UnitSystem { get; init; } = "metric";
}

/// <summary>
/// Vehicle specifications for commercial vehicle routing.
/// </summary>
public sealed record VehicleSpecifications
{
    /// <summary>
    /// Vehicle weight in kilograms.
    /// </summary>
    public double? WeightKg { get; init; }

    /// <summary>
    /// Vehicle height in meters.
    /// </summary>
    public double? HeightMeters { get; init; }

    /// <summary>
    /// Vehicle width in meters.
    /// </summary>
    public double? WidthMeters { get; init; }

    /// <summary>
    /// Vehicle length in meters.
    /// </summary>
    public double? LengthMeters { get; init; }

    /// <summary>
    /// Vehicle axle count.
    /// </summary>
    public int? AxleCount { get; init; }

    /// <summary>
    /// Whether vehicle is carrying hazardous materials.
    /// </summary>
    public bool? IsHazmat { get; init; }
}

/// <summary>
/// Response from a routing request.
/// </summary>
public sealed record RoutingResponse
{
    /// <summary>
    /// List of calculated routes ordered by preference.
    /// </summary>
    public required IReadOnlyList<Route> Routes { get; init; }

    /// <summary>
    /// Attribution text required by the provider.
    /// </summary>
    public string? Attribution { get; init; }
}

/// <summary>
/// A single route with detailed information.
/// </summary>
public sealed record Route
{
    /// <summary>
    /// Total distance in meters.
    /// </summary>
    public required double DistanceMeters { get; init; }

    /// <summary>
    /// Total duration in seconds (without traffic).
    /// </summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Duration with current traffic conditions in seconds (if available).
    /// </summary>
    public double? DurationWithTrafficSeconds { get; init; }

    /// <summary>
    /// Encoded polyline geometry (Google Polyline format or GeoJSON).
    /// </summary>
    public required string Geometry { get; init; }

    /// <summary>
    /// Format of the geometry field ("polyline" or "geojson").
    /// </summary>
    public string GeometryFormat { get; init; } = "polyline";

    /// <summary>
    /// Bounding box of the route [west, south, east, north].
    /// </summary>
    public double[]? BoundingBox { get; init; }

    /// <summary>
    /// Turn-by-turn navigation instructions.
    /// </summary>
    public IReadOnlyList<RouteInstruction>? Instructions { get; init; }

    /// <summary>
    /// Route legs (segments between waypoints).
    /// </summary>
    public IReadOnlyList<RouteLeg>? Legs { get; init; }

    /// <summary>
    /// Summary description of the route.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Warnings about the route (e.g., toll roads, ferries).
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; init; }
}

/// <summary>
/// A turn-by-turn navigation instruction.
/// </summary>
public sealed record RouteInstruction
{
    /// <summary>
    /// Instruction text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Distance to this instruction from previous instruction (meters).
    /// </summary>
    public required double DistanceMeters { get; init; }

    /// <summary>
    /// Time to reach this instruction from previous instruction (seconds).
    /// </summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Maneuver type (e.g., "turn-left", "turn-right", "straight", "arrive").
    /// </summary>
    public string? ManeuverType { get; init; }

    /// <summary>
    /// Road name or exit information.
    /// </summary>
    public string? RoadName { get; init; }

    /// <summary>
    /// Location of this instruction [longitude, latitude].
    /// </summary>
    public double[]? Location { get; init; }
}

/// <summary>
/// A route leg between two waypoints.
/// </summary>
public sealed record RouteLeg
{
    /// <summary>
    /// Distance of this leg in meters.
    /// </summary>
    public required double DistanceMeters { get; init; }

    /// <summary>
    /// Duration of this leg in seconds.
    /// </summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Start location [longitude, latitude].
    /// </summary>
    public required double[] StartLocation { get; init; }

    /// <summary>
    /// End location [longitude, latitude].
    /// </summary>
    public required double[] EndLocation { get; init; }

    /// <summary>
    /// Start address (if available).
    /// </summary>
    public string? StartAddress { get; init; }

    /// <summary>
    /// End address (if available).
    /// </summary>
    public string? EndAddress { get; init; }

    /// <summary>
    /// Instructions for this leg.
    /// </summary>
    public IReadOnlyList<RouteInstruction>? Instructions { get; init; }
}
