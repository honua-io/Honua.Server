// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Honua.Server.Core.BlueGreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;

namespace Honua.Server.Gateway.Endpoints;

/// <summary>
/// API endpoints for managing blue-green deployment traffic switching in the gateway.
/// These endpoints allow administrators to control traffic distribution between deployments.
/// </summary>
public static class TrafficManagementEndpoints
{
    /// <summary>
    /// Maps all traffic management endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapTrafficManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/traffic")
            .WithTags("Traffic Management")
            .RequireAuthorization()
            .RequireRateLimiting("per-ip");

        // POST /admin/traffic/switch - Gradual traffic switch
        group.MapPost("/switch", SwitchTrafficAsync)
            .WithName("SwitchTraffic")
            .WithOpenApi(op =>
            {
                op.Summary = "Switch traffic between blue and green deployments";
                op.Description = "Adjusts the traffic split between blue (current) and green (new) deployments. " +
                                "Allows gradual migration by specifying the percentage of traffic to route to green.";
                return op;
            })
            .Produces<TrafficSwitchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST /admin/traffic/canary - Automated canary deployment
        group.MapPost("/canary", PerformCanaryDeploymentAsync)
            .WithName("PerformCanaryDeployment")
            .WithOpenApi(op =>
            {
                op.Summary = "Perform automated canary deployment";
                op.Description = "Executes a gradual canary deployment with health checks at each stage. " +
                                "Automatically rolls back if health checks fail.";
                return op;
            })
            .Produces<CanaryDeploymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST /admin/traffic/cutover - Instant switch to green
        group.MapPost("/cutover", PerformCutoverAsync)
            .WithName("PerformCutover")
            .WithOpenApi(op =>
            {
                op.Summary = "Instant cutover to green deployment";
                op.Description = "Immediately switches 100% of traffic to the green (new) deployment.";
                return op;
            })
            .Produces<TrafficSwitchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST /admin/traffic/rollback - Instant rollback to blue
        group.MapPost("/rollback", PerformRollbackAsync)
            .WithName("PerformRollback")
            .WithOpenApi(op =>
            {
                op.Summary = "Rollback to blue deployment";
                op.Description = "Immediately switches 100% of traffic back to the blue (current) deployment.";
                return op;
            })
            .Produces<TrafficSwitchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /admin/traffic/status - Get current proxy configuration
        group.MapGet("/status", GetTrafficStatusAsync)
            .WithName("GetTrafficStatus")
            .WithOpenApi(op =>
            {
                op.Summary = "Get current traffic configuration";
                op.Description = "Returns the current proxy configuration including all clusters, destinations, and traffic weights.";
                return op;
            })
            .Produces<TrafficStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> SwitchTrafficAsync(
        HttpContext context,
        [FromBody] TrafficSwitchRequest request,
        [FromServices] BlueGreenTrafficManager trafficManager,
        [FromServices] ILogger<BlueGreenTrafficManager> logger)
    {
        try
        {
            // Validate request
            var validationResult = ValidateTrafficSwitchRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            var username = context.User.Identity?.Name ?? "unknown";
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            logger.LogInformation(
                "User {Username} ({UserId}) initiating traffic switch for {Service}: {GreenPercentage}% to green",
                username, userId, request.ServiceName, request.GreenPercentage);

            // Perform traffic switch
            var result = await trafficManager.SwitchTrafficAsync(
                request.ServiceName,
                request.BlueEndpoint,
                request.GreenEndpoint,
                request.GreenPercentage,
                context.RequestAborted);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Traffic switch failed for {Service}: {Message}",
                    request.ServiceName, result.Message);

                return Results.Problem(
                    detail: result.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Traffic switch failed");
            }

            logger.LogInformation(
                "Traffic switch completed successfully for {Service}: Blue={BluePercentage}%, Green={GreenPercentage}%",
                request.ServiceName, result.BlueTrafficPercentage, result.GreenTrafficPercentage);

            var response = new TrafficSwitchResponse
            {
                Success = result.Success,
                ServiceName = request.ServiceName,
                BlueEndpoint = request.BlueEndpoint,
                GreenEndpoint = request.GreenEndpoint,
                BlueTrafficPercentage = result.BlueTrafficPercentage,
                GreenTrafficPercentage = result.GreenTrafficPercentage,
                Message = result.Message,
                Timestamp = DateTime.UtcNow,
                PerformedBy = username
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error switching traffic for {Service}", request.ServiceName);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Traffic switch error");
        }
    }

    private static async Task<IResult> PerformCanaryDeploymentAsync(
        HttpContext context,
        [FromBody] CanaryDeploymentRequest request,
        [FromServices] BlueGreenTrafficManager trafficManager,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] ILogger<BlueGreenTrafficManager> logger)
    {
        try
        {
            // Validate request
            var validationResult = ValidateCanaryDeploymentRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            var username = context.User.Identity?.Name ?? "unknown";
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            logger.LogInformation(
                "User {Username} ({UserId}) initiating canary deployment for {Service}",
                username, userId, request.ServiceName);

            // Build canary strategy
            var strategy = request.Strategy ?? new CanaryStrategy
            {
                TrafficSteps = new List<int> { 10, 25, 50, 100 },
                SoakDurationSeconds = 60,
                AutoRollback = true
            };

            // Create health check function
            var httpClient = httpClientFactory.CreateClient();
            async Task<bool> HealthCheckFunc(CancellationToken ct)
            {
                try
                {
                    var healthUrl = $"{request.GreenEndpoint.TrimEnd('/')}/health";
                    logger.LogDebug("Performing health check on {HealthUrl}", healthUrl);

                    var response = await httpClient.GetAsync(healthUrl, ct);
                    var isHealthy = response.IsSuccessStatusCode;

                    logger.LogInformation(
                        "Health check for {Service}: {Status}",
                        request.ServiceName,
                        isHealthy ? "Healthy" : "Unhealthy");

                    return isHealthy;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Health check failed for {Service}", request.ServiceName);
                    return false;
                }
            }

            // Perform canary deployment
            var result = await trafficManager.PerformCanaryDeploymentAsync(
                request.ServiceName,
                request.BlueEndpoint,
                request.GreenEndpoint,
                strategy,
                HealthCheckFunc,
                context.RequestAborted);

            logger.LogInformation(
                "Canary deployment completed for {Service}: Success={Success}, RolledBack={RolledBack}",
                request.ServiceName, result.Success, result.RolledBack);

            var response = new CanaryDeploymentResponse
            {
                Success = result.Success,
                RolledBack = result.RolledBack,
                ServiceName = request.ServiceName,
                BlueEndpoint = request.BlueEndpoint,
                GreenEndpoint = request.GreenEndpoint,
                Stages = result.Stages.Select(s => new CanaryStageInfo
                {
                    GreenTrafficPercentage = s.GreenTrafficPercentage,
                    IsHealthy = s.IsHealthy,
                    Timestamp = s.Timestamp
                }).ToList(),
                Message = result.Message,
                CompletedAt = DateTime.UtcNow,
                PerformedBy = username
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing canary deployment for {Service}", request.ServiceName);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Canary deployment error");
        }
    }

    private static async Task<IResult> PerformCutoverAsync(
        HttpContext context,
        [FromBody] CutoverRequest request,
        [FromServices] BlueGreenTrafficManager trafficManager,
        [FromServices] ILogger<BlueGreenTrafficManager> logger)
    {
        try
        {
            // Validate request
            var validationResult = ValidateCutoverRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            var username = context.User.Identity?.Name ?? "unknown";
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            logger.LogWarning(
                "User {Username} ({UserId}) initiating INSTANT CUTOVER for {Service} to green",
                username, userId, request.ServiceName);

            // Perform instant cutover
            var result = await trafficManager.PerformInstantCutoverAsync(
                request.ServiceName,
                request.BlueEndpoint,
                request.GreenEndpoint,
                context.RequestAborted);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Cutover failed for {Service}: {Message}",
                    request.ServiceName, result.Message);

                return Results.Problem(
                    detail: result.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Cutover failed");
            }

            logger.LogInformation(
                "Instant cutover completed successfully for {Service}: 100% traffic on green",
                request.ServiceName);

            var response = new TrafficSwitchResponse
            {
                Success = result.Success,
                ServiceName = request.ServiceName,
                BlueEndpoint = request.BlueEndpoint,
                GreenEndpoint = request.GreenEndpoint,
                BlueTrafficPercentage = result.BlueTrafficPercentage,
                GreenTrafficPercentage = result.GreenTrafficPercentage,
                Message = result.Message,
                Timestamp = DateTime.UtcNow,
                PerformedBy = username
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing cutover for {Service}", request.ServiceName);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Cutover error");
        }
    }

    private static async Task<IResult> PerformRollbackAsync(
        HttpContext context,
        [FromBody] RollbackRequest request,
        [FromServices] BlueGreenTrafficManager trafficManager,
        [FromServices] ILogger<BlueGreenTrafficManager> logger)
    {
        try
        {
            // Validate request
            var validationResult = ValidateRollbackRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            var username = context.User.Identity?.Name ?? "unknown";
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            logger.LogWarning(
                "User {Username} ({UserId}) initiating ROLLBACK for {Service} to blue",
                username, userId, request.ServiceName);

            // Perform rollback
            var result = await trafficManager.RollbackToBlueAsync(
                request.ServiceName,
                request.BlueEndpoint,
                request.GreenEndpoint,
                context.RequestAborted);

            if (!result.Success)
            {
                logger.LogError(
                    "ROLLBACK FAILED for {Service}: {Message}",
                    request.ServiceName, result.Message);

                return Results.Problem(
                    detail: result.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Rollback failed");
            }

            logger.LogInformation(
                "Rollback completed successfully for {Service}: 100% traffic on blue",
                request.ServiceName);

            var response = new TrafficSwitchResponse
            {
                Success = result.Success,
                ServiceName = request.ServiceName,
                BlueEndpoint = request.BlueEndpoint,
                GreenEndpoint = request.GreenEndpoint,
                BlueTrafficPercentage = result.BlueTrafficPercentage,
                GreenTrafficPercentage = result.GreenTrafficPercentage,
                Message = result.Message,
                Timestamp = DateTime.UtcNow,
                PerformedBy = username
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing rollback for {Service}", request.ServiceName);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Rollback error");
        }
    }

    private static async Task<IResult> GetTrafficStatusAsync(
        HttpContext context,
        [FromServices] IProxyConfigProvider proxyConfigProvider,
        [FromServices] ILogger<BlueGreenTrafficManager> logger)
    {
        try
        {
            logger.LogDebug("Retrieving current traffic status");

            // Get current proxy configuration
            var proxyConfig = proxyConfigProvider.GetConfig();

            var clusters = new List<ClusterInfo>();
            foreach (var cluster in proxyConfig.Clusters)
            {
                var destinations = new List<DestinationInfo>();
                foreach (var destination in cluster.Destinations)
                {
                    var weight = 100; // Default weight
                    if (destination.Value.Metadata?.TryGetValue("weight", out var weightStr) == true)
                    {
                        int.TryParse(weightStr, out weight);
                    }

                    destinations.Add(new DestinationInfo
                    {
                        Name = destination.Key,
                        Address = destination.Value.Address,
                        Weight = weight,
                        HealthCheckPath = destination.Value.Health ?? "/health"
                    });
                }

                clusters.Add(new ClusterInfo
                {
                    ClusterId = cluster.ClusterId,
                    LoadBalancingPolicy = cluster.LoadBalancingPolicy ?? "RoundRobin",
                    Destinations = destinations,
                    HealthCheckEnabled = cluster.HealthCheck?.Active?.Enabled ?? false
                });
            }

            var routes = proxyConfig.Routes.Select(r => new RouteInfo
            {
                RouteId = r.RouteId,
                ClusterId = r.ClusterId ?? "unknown",
                MatchPath = r.Match.Path ?? "/**"
            }).ToList();

            var response = new TrafficStatusResponse
            {
                Clusters = clusters,
                Routes = routes,
                Timestamp = DateTime.UtcNow
            };

            logger.LogDebug("Retrieved status for {ClusterCount} clusters and {RouteCount} routes",
                clusters.Count, routes.Count);

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving traffic status");
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Traffic status error");
        }
    }

    #region Validation Helpers

    private static IResult? ValidateTrafficSwitchRequest(TrafficSwitchRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ServiceName))
            errors.Add("ServiceName is required");

        if (string.IsNullOrWhiteSpace(request.BlueEndpoint))
            errors.Add("BlueEndpoint is required");

        if (string.IsNullOrWhiteSpace(request.GreenEndpoint))
            errors.Add("GreenEndpoint is required");

        if (request.GreenPercentage < 0 || request.GreenPercentage > 100)
            errors.Add("GreenPercentage must be between 0 and 100");

        if (!Uri.TryCreate(request.BlueEndpoint, UriKind.Absolute, out _))
            errors.Add("BlueEndpoint must be a valid absolute URL");

        if (!Uri.TryCreate(request.GreenEndpoint, UriKind.Absolute, out _))
            errors.Add("GreenEndpoint must be a valid absolute URL");

        if (errors.Any())
        {
            return Results.ValidationProblem(errors.ToDictionary(e => e, e => new[] { e }));
        }

        return null;
    }

    private static IResult? ValidateCanaryDeploymentRequest(CanaryDeploymentRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ServiceName))
            errors.Add("ServiceName is required");

        if (string.IsNullOrWhiteSpace(request.BlueEndpoint))
            errors.Add("BlueEndpoint is required");

        if (string.IsNullOrWhiteSpace(request.GreenEndpoint))
            errors.Add("GreenEndpoint is required");

        if (!Uri.TryCreate(request.BlueEndpoint, UriKind.Absolute, out _))
            errors.Add("BlueEndpoint must be a valid absolute URL");

        if (!Uri.TryCreate(request.GreenEndpoint, UriKind.Absolute, out _))
            errors.Add("GreenEndpoint must be a valid absolute URL");

        if (request.Strategy != null)
        {
            if (request.Strategy.TrafficSteps.Count == 0)
                errors.Add("Strategy.TrafficSteps must contain at least one step");

            if (request.Strategy.TrafficSteps.Any(step => step < 0 || step > 100))
                errors.Add("Strategy.TrafficSteps must contain values between 0 and 100");

            if (request.Strategy.SoakDurationSeconds < 0)
                errors.Add("Strategy.SoakDurationSeconds must be non-negative");
        }

        if (errors.Any())
        {
            return Results.ValidationProblem(errors.ToDictionary(e => e, e => new[] { e }));
        }

        return null;
    }

    private static IResult? ValidateCutoverRequest(CutoverRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ServiceName))
            errors.Add("ServiceName is required");

        if (string.IsNullOrWhiteSpace(request.BlueEndpoint))
            errors.Add("BlueEndpoint is required");

        if (string.IsNullOrWhiteSpace(request.GreenEndpoint))
            errors.Add("GreenEndpoint is required");

        if (!Uri.TryCreate(request.BlueEndpoint, UriKind.Absolute, out _))
            errors.Add("BlueEndpoint must be a valid absolute URL");

        if (!Uri.TryCreate(request.GreenEndpoint, UriKind.Absolute, out _))
            errors.Add("GreenEndpoint must be a valid absolute URL");

        if (errors.Any())
        {
            return Results.ValidationProblem(errors.ToDictionary(e => e, e => new[] { e }));
        }

        return null;
    }

    private static IResult? ValidateRollbackRequest(RollbackRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ServiceName))
            errors.Add("ServiceName is required");

        if (string.IsNullOrWhiteSpace(request.BlueEndpoint))
            errors.Add("BlueEndpoint is required");

        if (string.IsNullOrWhiteSpace(request.GreenEndpoint))
            errors.Add("GreenEndpoint is required");

        if (!Uri.TryCreate(request.BlueEndpoint, UriKind.Absolute, out _))
            errors.Add("BlueEndpoint must be a valid absolute URL");

        if (!Uri.TryCreate(request.GreenEndpoint, UriKind.Absolute, out _))
            errors.Add("GreenEndpoint must be a valid absolute URL");

        if (errors.Any())
        {
            return Results.ValidationProblem(errors.ToDictionary(e => e, e => new[] { e }));
        }

        return null;
    }

    #endregion
}

#region Request DTOs

/// <summary>
/// Request to switch traffic between blue and green deployments.
/// </summary>
public sealed class TrafficSwitchRequest
{
    /// <summary>
    /// Name of the service to switch traffic for.
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Blue (current) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string BlueEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Green (new) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string GreenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of traffic to route to green deployment (0-100).
    /// </summary>
    [Required]
    [Range(0, 100)]
    public int GreenPercentage { get; set; }
}

/// <summary>
/// Request to perform a canary deployment.
/// </summary>
public sealed class CanaryDeploymentRequest
{
    /// <summary>
    /// Name of the service to deploy.
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Blue (current) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string BlueEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Green (new) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string GreenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional canary deployment strategy. If not provided, uses default strategy.
    /// </summary>
    public CanaryStrategy? Strategy { get; set; }
}

/// <summary>
/// Request to perform an instant cutover to green deployment.
/// </summary>
public sealed class CutoverRequest
{
    /// <summary>
    /// Name of the service to cutover.
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Blue (current) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string BlueEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Green (new) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string GreenEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// Request to rollback to blue deployment.
/// </summary>
public sealed class RollbackRequest
{
    /// <summary>
    /// Name of the service to rollback.
    /// </summary>
    [Required]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Blue (current) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string BlueEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Green (new) deployment endpoint URL.
    /// </summary>
    [Required]
    [Url]
    public string GreenEndpoint { get; set; } = string.Empty;
}

#endregion

#region Response DTOs

/// <summary>
/// Response from a traffic switch operation.
/// </summary>
public sealed class TrafficSwitchResponse
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Name of the service that was switched.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Blue deployment endpoint.
    /// </summary>
    public string BlueEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Green deployment endpoint.
    /// </summary>
    public string GreenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Current percentage of traffic on blue deployment.
    /// </summary>
    public int BlueTrafficPercentage { get; set; }

    /// <summary>
    /// Current percentage of traffic on green deployment.
    /// </summary>
    public int GreenTrafficPercentage { get; set; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the operation.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// User who performed the operation.
    /// </summary>
    public string PerformedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response from a canary deployment operation.
/// </summary>
public sealed class CanaryDeploymentResponse
{
    /// <summary>
    /// Indicates whether the deployment was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Indicates whether the deployment was rolled back.
    /// </summary>
    public bool RolledBack { get; set; }

    /// <summary>
    /// Name of the service that was deployed.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Blue deployment endpoint.
    /// </summary>
    public string BlueEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Green deployment endpoint.
    /// </summary>
    public string GreenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// List of canary stages executed.
    /// </summary>
    public List<CanaryStageInfo> Stages { get; set; } = new();

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when deployment completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// User who performed the deployment.
    /// </summary>
    public string PerformedBy { get; set; } = string.Empty;
}

/// <summary>
/// Information about a canary deployment stage.
/// </summary>
public sealed class CanaryStageInfo
{
    /// <summary>
    /// Percentage of traffic on green at this stage.
    /// </summary>
    public int GreenTrafficPercentage { get; set; }

    /// <summary>
    /// Whether the health check passed at this stage.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Timestamp of this stage.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Response containing current traffic status.
/// </summary>
public sealed class TrafficStatusResponse
{
    /// <summary>
    /// List of all configured clusters.
    /// </summary>
    public List<ClusterInfo> Clusters { get; set; } = new();

    /// <summary>
    /// List of all configured routes.
    /// </summary>
    public List<RouteInfo> Routes { get; set; } = new();

    /// <summary>
    /// Timestamp of this status snapshot.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Information about a proxy cluster.
/// </summary>
public sealed class ClusterInfo
{
    /// <summary>
    /// Unique identifier for this cluster.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Load balancing policy used for this cluster.
    /// </summary>
    public string LoadBalancingPolicy { get; set; } = string.Empty;

    /// <summary>
    /// List of destinations in this cluster.
    /// </summary>
    public List<DestinationInfo> Destinations { get; set; } = new();

    /// <summary>
    /// Whether health checking is enabled for this cluster.
    /// </summary>
    public bool HealthCheckEnabled { get; set; }
}

/// <summary>
/// Information about a destination within a cluster.
/// </summary>
public sealed class DestinationInfo
{
    /// <summary>
    /// Name of this destination (e.g., "blue", "green").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL of this destination.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Traffic weight for this destination (used in weighted routing).
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Health check path for this destination.
    /// </summary>
    public string HealthCheckPath { get; set; } = string.Empty;
}

/// <summary>
/// Information about a proxy route.
/// </summary>
public sealed class RouteInfo
{
    /// <summary>
    /// Unique identifier for this route.
    /// </summary>
    public string RouteId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the cluster this route targets.
    /// </summary>
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Path pattern this route matches.
    /// </summary>
    public string MatchPath { get; set; } = string.Empty;
}

#endregion
