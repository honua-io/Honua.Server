// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Service for executing geoprocessing operations
/// </summary>
public interface IGeoprocessingService
{
    /// <summary>
    /// Executes a geoprocessing job
    /// </summary>
    Task<Dictionary<string, object>> ExecuteJobAsync(GeoprocessingJob job, IProgress<GeoprocessingProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates job parameters before execution
    /// </summary>
    Task<ValidationResult> ValidateJobAsync(GeoprocessingJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates job execution time and resource usage
    /// </summary>
    Task<JobEstimate> EstimateJobAsync(GeoprocessingJob job, CancellationToken cancellationToken = default);
}

/// <summary>
/// Progress information for geoprocessing job
/// </summary>
public class GeoprocessingProgress
{
    public int ProgressPercent { get; set; }
    public string? Message { get; set; }
    public long? FeaturesProcessed { get; set; }
    public long? TotalFeatures { get; set; }
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Job estimate
/// </summary>
public class JobEstimate
{
    public long EstimatedDurationSeconds { get; set; }
    public long EstimatedMemoryMB { get; set; }
    public long EstimatedOutputSizeMB { get; set; }
    public int EstimatedCpuCores { get; set; }
}

/// <summary>
/// Interface for individual geoprocessing operations
/// </summary>
public interface IGeoprocessingOperation
{
    /// <summary>
    /// Operation name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Operation description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the operation
    /// </summary>
    Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates operation parameters
    /// </summary>
    ValidationResult Validate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs);

    /// <summary>
    /// Estimates operation cost
    /// </summary>
    JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs);
}

/// <summary>
/// Operation execution result
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public long FeaturesProcessed { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// Base class for geoprocessing operations
/// </summary>
public abstract class GeoprocessingOperationBase : IGeoprocessingOperation
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    public virtual ValidationResult Validate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var result = new ValidationResult { IsValid = true };

        if (inputs == null || inputs.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("At least one input is required");
        }

        return result;
    }

    public virtual JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        // Default estimate - override in derived classes for more accurate estimates
        return new JobEstimate
        {
            EstimatedDurationSeconds = 30,
            EstimatedMemoryMB = 512,
            EstimatedOutputSizeMB = 10,
            EstimatedCpuCores = 1
        };
    }

    protected void ReportProgress(IProgress<GeoprocessingProgress>? progress, int percent, string? message = null, long? processed = null, long? total = null)
    {
        progress?.Report(new GeoprocessingProgress
        {
            ProgressPercent = percent,
            Message = message,
            FeaturesProcessed = processed,
            TotalFeatures = total
        });
    }
}
