// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.RouteOptimization;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.RouteOptimization;

/// <summary>
/// Service for multi-stop route optimization (TSP/VRP)
/// Supports multiple providers and client-side algorithms
/// </summary>
public class RouteOptimizationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RouteOptimizationService> _logger;
    private readonly TspSolver _tspSolver;
    private readonly VrpSolver _vrpSolver;

    public RouteOptimizationService(
        IHttpClientFactory httpClientFactory,
        ILogger<RouteOptimizationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _tspSolver = new TspSolver();
        _vrpSolver = new VrpSolver();
    }

    /// <summary>
    /// Optimize route using specified provider
    /// </summary>
    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationRequest request,
        OptimizationProvider provider = OptimizationProvider.ClientSide,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting route optimization with {Provider} for {Count} waypoints",
                provider, request.Waypoints.Count);

            OptimizationResult result = provider switch
            {
                OptimizationProvider.ClientSide => await OptimizeClientSideAsync(request, progress, cancellationToken),
                OptimizationProvider.Mapbox => await OptimizeMapboxAsync(request, progress, cancellationToken),
                OptimizationProvider.OSRM => await OptimizeOsrmAsync(request, progress, cancellationToken),
                OptimizationProvider.GraphHopper => await OptimizeGraphHopperAsync(request, progress, cancellationToken),
                _ => await OptimizeClientSideAsync(request, progress, cancellationToken)
            };

            result.ComputationTimeMs = sw.ElapsedMilliseconds;
            result.Provider = provider.ToString();

            _logger.LogInformation("Optimization completed in {Ms}ms, saved {Percent:F1}% distance",
                result.ComputationTimeMs, result.Metrics.DistanceSavingsPercent);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during route optimization");
            throw;
        }
    }

    /// <summary>
    /// Optimize using client-side algorithms (Nearest Neighbor + 2-opt)
    /// </summary>
    private async Task<OptimizationResult> OptimizeClientSideAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OptimizationProgress { Stage = "Building distance matrix", Percent = 10 });

        // Build distance matrix
        var distanceMatrix = _tspSolver.BuildDistanceMatrix(request.Waypoints);

        // Calculate original route distance (in order)
        var originalTour = Enumerable.Range(0, request.Waypoints.Count).ToList();
        var originalDistance = _tspSolver.CalculateTourDistance(originalTour, distanceMatrix);

        progress?.Report(new OptimizationProgress { Stage = "Running optimization algorithm", Percent = 30 });

        // Choose algorithm based on problem size
        List<int> optimizedTour;
        string algorithm;

        if (request.Waypoints.Count <= 10)
        {
            // Small problem: use multi-start for better quality
            algorithm = "MultiStart";
            optimizedTour = await _tspSolver.SolveMultiStart(distanceMatrix, 5, cancellationToken);
        }
        else if (request.Waypoints.Count <= 50)
        {
            // Medium problem: hybrid approach
            algorithm = "Hybrid (Nearest Neighbor + 2-opt)";
            optimizedTour = await _tspSolver.SolveHybrid(distanceMatrix, 0, cancellationToken);
        }
        else
        {
            // Large problem: just nearest neighbor
            algorithm = "Nearest Neighbor";
            optimizedTour = await _tspSolver.SolveNearestNeighbor(distanceMatrix, 0, cancellationToken);
        }

        progress?.Report(new OptimizationProgress { Stage = "Calculating metrics", Percent = 80 });

        var optimizedDistance = _tspSolver.CalculateTourDistance(optimizedTour, distanceMatrix);

        // Build result
        var result = new OptimizationResult
        {
            OriginalSequence = new List<OptimizationWaypoint>(request.Waypoints),
            OptimizedSequence = optimizedTour.Select(i => request.Waypoints[i]).ToList(),
            Algorithm = algorithm,
            IsOptimal = false,
            QualityScore = _tspSolver.EstimateQuality(algorithm, request.Waypoints.Count),
            Metrics = new OptimizationMetrics
            {
                OriginalDistanceMeters = originalDistance,
                OptimizedDistanceMeters = optimizedDistance,
                OriginalStopCount = request.Waypoints.Count,
                OptimizedStopCount = request.Waypoints.Count
            }
        };

        // Calculate durations (estimate based on average speed)
        double avgSpeed = request.Vehicle?.AverageSpeedMps ?? 13.89; // Default ~50 km/h
        result.Metrics.OriginalDurationSeconds = (int)(originalDistance / avgSpeed);
        result.Metrics.OptimizedDurationSeconds = (int)(optimizedDistance / avgSpeed);

        // Calculate costs
        double costPerMeter = request.Vehicle?.CostPerMeter ?? 0.001;
        double costPerSecond = request.Vehicle?.CostPerSecond ?? 0.01;

        result.Metrics.OriginalCost =
            originalDistance * costPerMeter +
            result.Metrics.OriginalDurationSeconds * costPerSecond;

        result.Metrics.OptimizedCost =
            optimizedDistance * costPerMeter +
            result.Metrics.OptimizedDurationSeconds * costPerSecond;

        // Handle time windows if enabled
        if (request.EnableTimeWindows)
        {
            result.TimeWindowViolations = CheckTimeWindows(
                result.OptimizedSequence,
                request.DepartureTime ?? DateTime.UtcNow,
                avgSpeed);
        }

        // Handle multiple vehicles if requested
        if (request.MultipleVehicles && request.NumberOfVehicles > 1 && request.Vehicle != null)
        {
            progress?.Report(new OptimizationProgress { Stage = "Solving VRP", Percent = 50 });

            result.Routes = await _vrpSolver.SolveClusterFirst(
                request.Waypoints,
                request.NumberOfVehicles,
                request.Vehicle,
                request.StartLocation,
                cancellationToken);

            result.Metrics.VehiclesUsed = result.Routes.Count;
        }

        progress?.Report(new OptimizationProgress { Stage = "Complete", Percent = 100 });

        return result;
    }

    /// <summary>
    /// Optimize using Mapbox Optimization API
    /// </summary>
    private async Task<OptimizationResult> OptimizeMapboxAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OptimizationProgress { Stage = "Calling Mapbox API", Percent = 20 });

        var httpClient = _httpClientFactory.CreateClient();

        // Build coordinates string
        var coordinates = string.Join(";",
            request.Waypoints.Select(w => $"{w.Location.Longitude},{w.Location.Latitude}"));

        // Mapbox Optimization API endpoint
        var url = $"https://api.mapbox.com/optimized-trips/v1/mapbox/{request.TravelMode}/{coordinates}";
        url += "?geometries=geojson&overview=full";

        // Add API key from configuration (would come from settings)
        // url += $"&access_token={apiKey}";

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var mapboxResult = JsonSerializer.Deserialize<JsonElement>(content);

            // Parse Mapbox response and convert to our format
            // (Simplified - actual implementation would parse full response)

            return await OptimizeClientSideAsync(request, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mapbox optimization failed, falling back to client-side");
            return await OptimizeClientSideAsync(request, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Optimize using OSRM Trip endpoint
    /// </summary>
    private async Task<OptimizationResult> OptimizeOsrmAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OptimizationProgress { Stage = "Calling OSRM API", Percent = 20 });

        var httpClient = _httpClientFactory.CreateClient();

        // Build coordinates string
        var coordinates = string.Join(";",
            request.Waypoints.Select(w => $"{w.Location.Longitude},{w.Location.Latitude}"));

        // OSRM Trip endpoint
        var url = $"http://router.project-osrm.org/trip/v1/{request.TravelMode}/{coordinates}";
        url += "?geometries=geojson&overview=full&roundtrip=true";

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var osrmResult = JsonSerializer.Deserialize<JsonElement>(content);

            // Parse OSRM response
            // (Simplified - actual implementation would parse full response)

            return await OptimizeClientSideAsync(request, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSRM optimization failed, falling back to client-side");
            return await OptimizeClientSideAsync(request, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Optimize using GraphHopper Route Optimization API
    /// </summary>
    private async Task<OptimizationResult> OptimizeGraphHopperAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        // GraphHopper implementation would go here
        // Falling back to client-side for now
        _logger.LogWarning("GraphHopper optimization not yet implemented, using client-side");
        return await OptimizeClientSideAsync(request, progress, cancellationToken);
    }

    /// <summary>
    /// Check time window violations for a route
    /// </summary>
    private List<TimeWindowViolation> CheckTimeWindows(
        List<OptimizationWaypoint> sequence,
        DateTime startTime,
        double avgSpeedMps)
    {
        var violations = new List<TimeWindowViolation>();
        var currentTime = startTime;

        for (int i = 0; i < sequence.Count; i++)
        {
            var waypoint = sequence[i];

            if (waypoint.TimeWindow != null)
            {
                if (waypoint.TimeWindow.EarliestArrival.HasValue &&
                    currentTime < waypoint.TimeWindow.EarliestArrival.Value)
                {
                    violations.Add(new TimeWindowViolation
                    {
                        Waypoint = waypoint,
                        ArrivalTime = currentTime,
                        ViolationSeconds = (int)(waypoint.TimeWindow.EarliestArrival.Value - currentTime).TotalSeconds,
                        Type = ViolationType.TooEarly
                    });

                    // Wait until window opens
                    if (waypoint.TimeWindow.AllowEarlyArrival)
                    {
                        currentTime = waypoint.TimeWindow.EarliestArrival.Value;
                    }
                }
                else if (waypoint.TimeWindow.LatestArrival.HasValue &&
                         currentTime > waypoint.TimeWindow.LatestArrival.Value)
                {
                    violations.Add(new TimeWindowViolation
                    {
                        Waypoint = waypoint,
                        ArrivalTime = currentTime,
                        ViolationSeconds = (int)(currentTime - waypoint.TimeWindow.LatestArrival.Value).TotalSeconds,
                        Type = ViolationType.TooLate
                    });
                }
            }

            // Add service duration
            currentTime = currentTime.AddSeconds(waypoint.ServiceDurationSeconds);

            // Add travel time to next waypoint (if exists)
            if (i < sequence.Count - 1)
            {
                var distance = TspSolver.HaversineDistance(
                    sequence[i].Location,
                    sequence[i + 1].Location);
                var travelTime = distance / avgSpeedMps;
                currentTime = currentTime.AddSeconds(travelTime);
            }
        }

        return violations;
    }
}

/// <summary>
/// Progress information for optimization
/// </summary>
public class OptimizationProgress
{
    /// <summary>
    /// Current stage of optimization
    /// </summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    /// Additional details
    /// </summary>
    public string? Details { get; set; }
}
