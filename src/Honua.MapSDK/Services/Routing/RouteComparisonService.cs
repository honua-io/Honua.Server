// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Honua.MapSDK.Models.Routing;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// Service for comparing multiple routes from different providers or with different options.
/// </summary>
public sealed class RouteComparisonService
{
    /// <summary>
    /// Compares multiple routes and generates comparison metrics.
    /// </summary>
    /// <param name="routes">Routes to compare with provider information.</param>
    /// <returns>List of comparison metrics for each route.</returns>
    public List<RouteComparisonMetrics> CompareRoutes(
        IReadOnlyList<(string Provider, Route Route)> routes)
    {
        return routes.Select((r, index) => new RouteComparisonMetrics
        {
            RouteId = $"route-{index}",
            Provider = r.Provider,
            DistanceMeters = r.Route.DistanceMeters,
            DurationSeconds = r.Route.DurationSeconds,
            DurationWithTrafficSeconds = r.Route.DurationWithTrafficSeconds,
            TollRoadCount = CountWarningType(r.Route.Warnings, "toll"),
            FerryCount = CountWarningType(r.Route.Warnings, "ferry"),
            Warnings = r.Route.Warnings
        }).ToList();
    }

    /// <summary>
    /// Finds the fastest route from a list of comparisons.
    /// </summary>
    /// <param name="comparisons">List of route comparison metrics.</param>
    /// <param name="considerTraffic">Whether to consider traffic in the comparison.</param>
    /// <returns>The fastest route metrics.</returns>
    public RouteComparisonMetrics FindFastestRoute(
        IReadOnlyList<RouteComparisonMetrics> comparisons,
        bool considerTraffic = true)
    {
        return comparisons
            .OrderBy(r => considerTraffic && r.DurationWithTrafficSeconds.HasValue
                ? r.DurationWithTrafficSeconds.Value
                : r.DurationSeconds)
            .First();
    }

    /// <summary>
    /// Finds the shortest route by distance.
    /// </summary>
    /// <param name="comparisons">List of route comparison metrics.</param>
    /// <returns>The shortest route metrics.</returns>
    public RouteComparisonMetrics FindShortestRoute(
        IReadOnlyList<RouteComparisonMetrics> comparisons)
    {
        return comparisons
            .OrderBy(r => r.DistanceMeters)
            .First();
    }

    /// <summary>
    /// Finds the most economical route (avoiding tolls and considering distance).
    /// </summary>
    /// <param name="comparisons">List of route comparison metrics.</param>
    /// <param name="fuelCostPerKm">Fuel cost per kilometer.</param>
    /// <returns>The most economical route metrics.</returns>
    public RouteComparisonMetrics FindMostEconomicalRoute(
        IReadOnlyList<RouteComparisonMetrics> comparisons,
        double fuelCostPerKm = 0.15)
    {
        return comparisons
            .OrderBy(r =>
            {
                var fuelCost = (r.DistanceMeters / 1000.0) * fuelCostPerKm;
                var tollCost = r.EstimatedTollCost ?? (r.TollRoadCount * 5.0); // Rough estimate
                return fuelCost + tollCost;
            })
            .First();
    }

    /// <summary>
    /// Calculates time vs distance trade-off analysis.
    /// </summary>
    /// <param name="comparison">Route comparison to analyze.</param>
    /// <param name="baseline">Baseline route for comparison.</param>
    /// <returns>Trade-off analysis results.</returns>
    public TradeOffAnalysis AnalyzeTimeDistanceTradeOff(
        RouteComparisonMetrics comparison,
        RouteComparisonMetrics baseline)
    {
        var distanceDiffMeters = comparison.DistanceMeters - baseline.DistanceMeters;
        var timeDiffSeconds = comparison.DurationSeconds - baseline.DurationSeconds;
        var distanceDiffPercent = (distanceDiffMeters / baseline.DistanceMeters) * 100;
        var timeDiffPercent = (timeDiffSeconds / baseline.DurationSeconds) * 100;

        var recommendation = GenerateTradeOffRecommendation(
            distanceDiffPercent,
            timeDiffPercent,
            comparison.TollRoadCount,
            baseline.TollRoadCount);

        return new TradeOffAnalysis
        {
            DistanceDifferenceMeters = distanceDiffMeters,
            TimeDifferenceSeconds = timeDiffSeconds,
            DistanceDifferencePercent = distanceDiffPercent,
            TimeDifferencePercent = timeDiffPercent,
            TollDifference = comparison.TollRoadCount - baseline.TollRoadCount,
            Recommendation = recommendation
        };
    }

    /// <summary>
    /// Generates a comparison report for all routes.
    /// </summary>
    /// <param name="comparisons">List of route comparisons.</param>
    /// <returns>Formatted comparison report.</returns>
    public ComparisonReport GenerateComparisonReport(
        IReadOnlyList<RouteComparisonMetrics> comparisons)
    {
        var fastest = FindFastestRoute(comparisons);
        var shortest = FindShortestRoute(comparisons);
        var mostEconomical = FindMostEconomicalRoute(comparisons);

        var recommendations = new List<string>();

        if (fastest.RouteId == shortest.RouteId && fastest.RouteId == mostEconomical.RouteId)
        {
            recommendations.Add($"The route from {fastest.Provider} is optimal in all categories.");
        }
        else
        {
            if (fastest.RouteId != shortest.RouteId)
            {
                var timeSaved = shortest.DurationSeconds - fastest.DurationSeconds;
                var extraDistance = fastest.DistanceMeters - shortest.DistanceMeters;
                recommendations.Add(
                    $"Fastest route ({fastest.Provider}) saves {FormatDuration(timeSaved)} " +
                    $"but adds {FormatDistance(extraDistance)}.");
            }

            if (mostEconomical.TollRoadCount < fastest.TollRoadCount)
            {
                recommendations.Add(
                    $"Most economical route ({mostEconomical.Provider}) avoids " +
                    $"{fastest.TollRoadCount - mostEconomical.TollRoadCount} toll roads.");
            }
        }

        return new ComparisonReport
        {
            TotalRoutesCompared = comparisons.Count,
            FastestRoute = fastest,
            ShortestRoute = shortest,
            MostEconomicalRoute = mostEconomical,
            Recommendations = recommendations
        };
    }

    /// <summary>
    /// Estimates fuel consumption for a route.
    /// </summary>
    /// <param name="distanceMeters">Route distance in meters.</param>
    /// <param name="fuelEfficiencyLitersPer100Km">Vehicle fuel efficiency.</param>
    /// <returns>Estimated fuel consumption in liters.</returns>
    public double EstimateFuelConsumption(
        double distanceMeters,
        double fuelEfficiencyLitersPer100Km = 8.0)
    {
        var distanceKm = distanceMeters / 1000.0;
        return (distanceKm / 100.0) * fuelEfficiencyLitersPer100Km;
    }

    /// <summary>
    /// Estimates carbon emissions for a route.
    /// </summary>
    /// <param name="distanceMeters">Route distance in meters.</param>
    /// <param name="fuelType">Fuel type ("gasoline", "diesel", "electric").</param>
    /// <returns>Estimated CO2 emissions in kg.</returns>
    public double EstimateCarbonEmissions(
        double distanceMeters,
        string fuelType = "gasoline")
    {
        var distanceKm = distanceMeters / 1000.0;

        // CO2 emissions in kg per km
        var emissionFactor = fuelType.ToLowerInvariant() switch
        {
            "gasoline" => 0.192,  // kg CO2 per km
            "diesel" => 0.171,
            "hybrid" => 0.120,
            "electric" => 0.050,  // Varies by grid
            _ => 0.192
        };

        return distanceKm * emissionFactor;
    }

    /// <summary>
    /// Formats distance for display.
    /// </summary>
    private static string FormatDistance(double meters)
    {
        if (Math.Abs(meters) < 1000)
            return $"{meters:F0} m";
        return $"{meters / 1000.0:F1} km";
    }

    /// <summary>
    /// Formats duration for display.
    /// </summary>
    private static string FormatDuration(double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(Math.Abs(seconds));
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.TotalHours:F1} hours";
        return $"{timeSpan.TotalMinutes:F0} minutes";
    }

    /// <summary>
    /// Counts warnings of a specific type.
    /// </summary>
    private static int CountWarningType(IReadOnlyList<string>? warnings, string type)
    {
        if (warnings == null) return 0;
        return warnings.Count(w => w.Contains(type, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Generates trade-off recommendation.
    /// </summary>
    private static string GenerateTradeOffRecommendation(
        double distanceDiffPercent,
        double timeDiffPercent,
        int comparisonTolls,
        int baselineTolls)
    {
        if (Math.Abs(timeDiffPercent) < 5 && Math.Abs(distanceDiffPercent) < 5)
            return "Routes are very similar. Choose based on personal preference.";

        if (timeDiffPercent < -10 && distanceDiffPercent > 0)
            return "This route is significantly faster but longer. Good for time-sensitive trips.";

        if (distanceDiffPercent < -10 && timeDiffPercent > 0)
            return "This route is significantly shorter but slower. Good for fuel economy.";

        if (comparisonTolls > baselineTolls)
            return "This route includes toll roads. Consider if time savings justify the cost.";

        if (comparisonTolls < baselineTolls)
            return "This route avoids toll roads, potentially saving money.";

        return "Routes have different characteristics. Review the details to make the best choice.";
    }
}

/// <summary>
/// Trade-off analysis between two routes.
/// </summary>
public sealed record TradeOffAnalysis
{
    /// <summary>
    /// Distance difference in meters (positive means longer).
    /// </summary>
    public required double DistanceDifferenceMeters { get; init; }

    /// <summary>
    /// Time difference in seconds (positive means slower).
    /// </summary>
    public required double TimeDifferenceSeconds { get; init; }

    /// <summary>
    /// Distance difference as percentage.
    /// </summary>
    public required double DistanceDifferencePercent { get; init; }

    /// <summary>
    /// Time difference as percentage.
    /// </summary>
    public required double TimeDifferencePercent { get; init; }

    /// <summary>
    /// Toll road count difference.
    /// </summary>
    public required int TollDifference { get; init; }

    /// <summary>
    /// Recommendation text.
    /// </summary>
    public required string Recommendation { get; init; }
}

/// <summary>
/// Comprehensive comparison report for multiple routes.
/// </summary>
public sealed record ComparisonReport
{
    /// <summary>
    /// Total number of routes compared.
    /// </summary>
    public required int TotalRoutesCompared { get; init; }

    /// <summary>
    /// Fastest route.
    /// </summary>
    public required RouteComparisonMetrics FastestRoute { get; init; }

    /// <summary>
    /// Shortest route by distance.
    /// </summary>
    public required RouteComparisonMetrics ShortestRoute { get; init; }

    /// <summary>
    /// Most economical route.
    /// </summary>
    public required RouteComparisonMetrics MostEconomicalRoute { get; init; }

    /// <summary>
    /// Recommendations for route selection.
    /// </summary>
    public required List<string> Recommendations { get; init; }
}
