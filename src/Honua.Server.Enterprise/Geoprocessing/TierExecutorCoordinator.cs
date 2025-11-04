// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Coordinates execution across tiers with adaptive fallback
/// </summary>
public class TierExecutorCoordinator : ITierExecutor
{
    private readonly INtsExecutor _ntsExecutor;
    private readonly IPostGisExecutor? _postGisExecutor;
    private readonly ICloudBatchExecutor? _cloudBatchExecutor;
    private readonly ILogger<TierExecutorCoordinator> _logger;

    public TierExecutorCoordinator(
        INtsExecutor ntsExecutor,
        ILogger<TierExecutorCoordinator> logger,
        IPostGisExecutor? postGisExecutor = null,
        ICloudBatchExecutor? cloudBatchExecutor = null)
    {
        _ntsExecutor = ntsExecutor ?? throw new ArgumentNullException(nameof(ntsExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _postGisExecutor = postGisExecutor;
        _cloudBatchExecutor = cloudBatchExecutor;
    }

    public async Task<ProcessResult> ExecuteAsync(
        ProcessRun run,
        ProcessDefinition process,
        ProcessExecutionTier tier,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Executing job {JobId} on tier {Tier} for process {ProcessId}",
            run.JobId, tier, run.ProcessId);

        try
        {
            return tier switch
            {
                ProcessExecutionTier.NTS => await _ntsExecutor.ExecuteAsync(run, process, progress, ct),
                ProcessExecutionTier.PostGIS when _postGisExecutor != null => await _postGisExecutor.ExecuteAsync(run, process, progress, ct),
                ProcessExecutionTier.CloudBatch when _cloudBatchExecutor != null => await _cloudBatchExecutor.SubmitAsync(run, process, progress, ct),
                _ => throw new TierUnavailableException(tier, $"Tier {tier} not available or configured")
            };
        }
        catch (TierExecutionException)
        {
            throw; // Don't wrap tier execution exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution failed on tier {Tier} for job {JobId}", tier, run.JobId);
            throw new TierExecutionException(tier, run.ProcessId, $"Execution failed on tier {tier}", ex);
        }
    }

    public async Task<ProcessExecutionTier> SelectTierAsync(
        ProcessDefinition process,
        ProcessExecutionRequest request,
        CancellationToken ct = default)
    {
        // If preferred tier specified and available, use it
        if (request.PreferredTier.HasValue)
        {
            if (await IsTierAvailableAsync(request.PreferredTier.Value, ct))
            {
                return request.PreferredTier.Value;
            }
        }

        // Auto-selection based on process config and input size
        var config = process.ExecutionConfig;

        // Try NTS first for simple, fast operations
        if (config.SupportedTiers.Contains(ProcessExecutionTier.NTS))
        {
            var canUseNts = await _ntsExecutor.CanExecuteAsync(process, request, ct);
            if (canUseNts)
            {
                return ProcessExecutionTier.NTS;
            }
        }

        // Try PostGIS for medium complexity
        if (config.SupportedTiers.Contains(ProcessExecutionTier.PostGIS) && _postGisExecutor != null)
        {
            var canUsePostGis = await _postGisExecutor.CanExecuteAsync(process, request, ct);
            if (canUsePostGis)
            {
                return ProcessExecutionTier.PostGIS;
            }
        }

        // Fall back to Cloud Batch for large/complex jobs
        if (config.SupportedTiers.Contains(ProcessExecutionTier.CloudBatch) && _cloudBatchExecutor != null)
        {
            return ProcessExecutionTier.CloudBatch;
        }

        // Default to NTS if nothing else works
        return ProcessExecutionTier.NTS;
    }

    public Task<bool> IsTierAvailableAsync(ProcessExecutionTier tier, CancellationToken ct = default)
    {
        var isAvailable = tier switch
        {
            ProcessExecutionTier.NTS => true, // Always available
            ProcessExecutionTier.PostGIS => _postGisExecutor != null,
            ProcessExecutionTier.CloudBatch => _cloudBatchExecutor != null,
            _ => false
        };
        return Task.FromResult(isAvailable);
    }

    public async Task<TierStatus> GetTierStatusAsync(ProcessExecutionTier tier, CancellationToken ct = default)
    {
        // TODO: Implement actual health checks
        return new TierStatus
        {
            Tier = tier,
            Available = await IsTierAvailableAsync(tier, ct),
            QueueDepth = 0,
            ActiveJobs = 0,
            CapacityPercent = 100,
            AverageWaitSeconds = 0,
            HealthMessage = "OK",
            LastCheckAt = DateTimeOffset.UtcNow
        };
    }
}
