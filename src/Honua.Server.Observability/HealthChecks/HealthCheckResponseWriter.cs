// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Server.Observability.HealthChecks;

/// <summary>
/// Writes health check responses in the IETF RFC Health Check Response Format for HTTP APIs.
/// Implements the specification from: https://datatracker.ietf.org/doc/html/draft-inadarei-api-health-check-06
/// </summary>
public static class HealthCheckResponseWriter
{
    private const string ContentType = "application/health+json";

    /// <summary>
    /// Writes a health check response in RFC-compliant format.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="report">The health report containing check results.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = ContentType;

        // Map health status to HTTP status code per RFC requirements
        context.Response.StatusCode = MapHealthStatusToHttpCode(report.Status);

        var response = CreateHealthCheckResponse(report);

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(response, options);
        return context.Response.WriteAsync(json, Encoding.UTF8);
    }

    /// <summary>
    /// Creates a health check response object following RFC format.
    /// </summary>
    private static object CreateHealthCheckResponse(HealthReport report)
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly?.GetName().Version?.ToString()
                      ?? "1.0.0";

        var serviceName = assembly?.GetName().Name ?? "Honua.Server";
        var serviceId = GetOrCreateServiceInstanceId();

        var response = new Dictionary<string, object?>
        {
            ["status"] = MapHealthStatus(report.Status),
            ["version"] = GetMajorVersion(version),
            ["releaseId"] = version,
            ["serviceId"] = serviceId,
            ["description"] = $"Health status for {serviceName}",
        };

        // Add checks if there are any component health checks
        if (report.Entries.Count > 0)
        {
            response["checks"] = CreateChecksObject(report);
        }

        // Add notes for warnings or failures
        if (report.Status != HealthStatus.Healthy)
        {
            var notes = new List<string>();
            foreach (var entry in report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
            {
                notes.Add($"{entry.Key}: {entry.Value.Description ?? entry.Value.Status.ToString()}");
            }

            if (notes.Count > 0)
            {
                response["notes"] = notes;
            }
        }

        // Add output for failures
        if (report.Status == HealthStatus.Unhealthy)
        {
            var errors = report.Entries
                .Where(e => e.Value.Exception != null)
                .Select(e => $"{e.Key}: {e.Value.Exception?.Message}")
                .ToList();

            if (errors.Count > 0)
            {
                response["output"] = string.Join("; ", errors);
            }
        }

        return response;
    }

    /// <summary>
    /// Creates the checks object containing detailed health information for each dependency.
    /// </summary>
    private static Dictionary<string, object> CreateChecksObject(HealthReport report)
    {
        var checks = new Dictionary<string, object>();

        foreach (var entry in report.Entries)
        {
            var checkName = entry.Key;
            var checkResult = entry.Value;

            var componentDetails = new List<object>
            {
                CreateComponentDetail(checkResult, entry.Key)
            };

            checks[checkName] = componentDetails;
        }

        return checks;
    }

    /// <summary>
    /// Creates a component detail object for a health check entry.
    /// </summary>
    private static object CreateComponentDetail(HealthCheckResult result, string componentName)
    {
        var detail = new Dictionary<string, object?>
        {
            ["componentType"] = InferComponentType(componentName, result.Tags),
            ["status"] = MapHealthStatus(result.Status),
            ["time"] = DateTime.UtcNow.ToString("O")
        };

        // Add observedValue and observedUnit from data if available
        if (result.Data.Count > 0)
        {
            foreach (var (key, value) in result.Data)
            {
                if (key.EndsWith("Ms", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("Time", StringComparison.OrdinalIgnoreCase))
                {
                    detail["observedValue"] = value;
                    detail["observedUnit"] = "ms";
                    break;
                }
            }
        }

        // Add description/output
        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            if (result.Status == HealthStatus.Healthy)
            {
                // For healthy checks, use description
                detail["output"] = result.Description;
            }
            else
            {
                // For unhealthy/degraded, include in output
                detail["output"] = result.Description;
            }
        }

        // Add exception information if present
        if (result.Exception != null)
        {
            var output = detail.ContainsKey("output")
                ? $"{detail["output"]}; {result.Exception.Message}"
                : result.Exception.Message;
            detail["output"] = output;
        }

        return detail;
    }

    /// <summary>
    /// Infers the component type based on the component name and tags.
    /// </summary>
    private static string InferComponentType(string componentName, IEnumerable<string> tags)
    {
        var name = componentName.ToLowerInvariant();
        var tagList = tags.ToList();

        if (tagList.Contains("database") || name.Contains("database") || name.Contains("db") ||
            name.Contains("postgres") || name.Contains("sql") || name.Contains("mongo"))
        {
            return "datastore";
        }

        if (tagList.Contains("cache") || name.Contains("cache") || name.Contains("redis"))
        {
            return "datastore";
        }

        if (tagList.Contains("storage") || name.Contains("storage") || name.Contains("s3") ||
            name.Contains("blob") || name.Contains("gcs"))
        {
            return "system";
        }

        if (tagList.Contains("queue") || name.Contains("queue") || name.Contains("sns") ||
            name.Contains("eventgrid"))
        {
            return "system";
        }

        if (tagList.Contains("oidc") || name.Contains("oidc") || name.Contains("auth"))
        {
            return "system";
        }

        // Default to component
        return "component";
    }

    /// <summary>
    /// Maps ASP.NET Core HealthStatus to RFC status values.
    /// </summary>
    private static string MapHealthStatus(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "pass",
            HealthStatus.Degraded => "warn",
            HealthStatus.Unhealthy => "fail",
            _ => "fail"
        };
    }

    /// <summary>
    /// Maps health status to appropriate HTTP status code per RFC requirements.
    /// </summary>
    private static int MapHealthStatusToHttpCode(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => StatusCodes.Status200OK,      // 2xx for pass
            HealthStatus.Degraded => StatusCodes.Status200OK,     // 2xx for warn
            HealthStatus.Unhealthy => StatusCodes.Status503ServiceUnavailable, // 5xx for fail
            _ => StatusCodes.Status503ServiceUnavailable
        };
    }

    /// <summary>
    /// Extracts major version from full version string.
    /// </summary>
    private static string GetMajorVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length > 0 ? parts[0] : "1";
    }

    /// <summary>
    /// Gets or creates a persistent service instance ID.
    /// In production, this should be stored in configuration or derived from environment.
    /// </summary>
    private static string GetOrCreateServiceInstanceId()
    {
        // Try to get from environment (common in containerized environments)
        var instanceId = Environment.GetEnvironmentVariable("SERVICE_INSTANCE_ID")
                        ?? Environment.GetEnvironmentVariable("HOSTNAME")
                        ?? Environment.GetEnvironmentVariable("COMPUTERNAME");

        if (!string.IsNullOrEmpty(instanceId))
        {
            return instanceId;
        }

        // Fallback: generate a stable ID based on machine name
        var machineName = Environment.MachineName;
        var processId = Environment.ProcessId;
        return $"{machineName}-{processId}";
    }
}
