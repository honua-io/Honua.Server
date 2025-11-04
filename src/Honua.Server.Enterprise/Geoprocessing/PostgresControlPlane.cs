// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// PostgreSQL implementation of Control Plane
/// Central orchestrator for admission control, scheduling, and auditing
/// </summary>
public partial class PostgresControlPlane : IControlPlane
{
    private readonly string _connectionString;
    private readonly IProcessRegistry _processRegistry;
    private readonly ITierExecutor _tierExecutor;
    private readonly ILogger<PostgresControlPlane> _logger;

    /// <summary>
    /// Maximum allowed length for job ID
    /// </summary>
    private const int MaxJobIdLength = 100;

    /// <summary>
    /// Maximum allowed length for cancellation reason
    /// </summary>
    private const int MaxReasonLength = 500;

    /// <summary>
    /// Regex pattern for valid job ID format: job-YYYYMMDD-{guid}
    /// </summary>
    private static readonly Regex JobIdPattern = JobIdRegex();

    public PostgresControlPlane(
        string connectionString,
        IProcessRegistry processRegistry,
        ITierExecutor tierExecutor,
        ILogger<PostgresControlPlane> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
        _tierExecutor = tierExecutor ?? throw new ArgumentNullException(nameof(tierExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdmissionDecision> AdmitAsync(ProcessExecutionRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Evaluating admission for process {ProcessId} from tenant {TenantId}",
            request.ProcessId, request.TenantId);

        var decision = new AdmissionDecision
        {
            Request = request,
            Admitted = false,
            DenialReasons = new List<string>(),
            Warnings = new List<string>()
        };

        // Verify process exists
        var process = await _processRegistry.GetProcessAsync(request.ProcessId, ct);
        if (process == null)
        {
            decision.DenialReasons.Add($"Process '{request.ProcessId}' not found");
            return decision;
        }

        if (!process.Enabled)
        {
            decision.DenialReasons.Add($"Process '{request.ProcessId}' is disabled");
            return decision;
        }

        // Check concurrent job limits for tenant
        await using var connection = new NpgsqlConnection(_connectionString);
        var runningCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM process_runs WHERE tenant_id = @TenantId AND status = 'running'",
            new { TenantId = request.TenantId.ToString() });

        const int maxConcurrent = 10; // TODO: Make configurable per tenant
        if (runningCount >= maxConcurrent)
        {
            decision.DenialReasons.Add($"Maximum concurrent jobs ({maxConcurrent}) reached for tenant");
            return decision;
        }

        // Check rate limits (jobs per minute)
        var recentCount = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM process_runs
              WHERE tenant_id = @TenantId
              AND created_at >= NOW() - INTERVAL '1 minute'",
            new { TenantId = request.TenantId.ToString() });

        const int rateLimit = 100; // TODO: Make configurable per tenant
        if (recentCount >= rateLimit)
        {
            decision.DenialReasons.Add($"Rate limit ({rateLimit} jobs/minute) exceeded");
            return decision;
        }

        // Select execution tier
        var selectedTier = await _tierExecutor.SelectTierAsync(process, request, ct);
        decision.SelectedTier = selectedTier;

        // Determine execution mode (sync vs async)
        if (request.Mode == ExecutionMode.Sync)
        {
            decision.ExecutionMode = ExecutionMode.Sync;
        }
        else if (request.Mode == ExecutionMode.Async)
        {
            decision.ExecutionMode = ExecutionMode.Async;
        }
        else
        {
            // Auto mode - use tier to decide
            decision.ExecutionMode = selectedTier == ProcessExecutionTier.NTS
                ? ExecutionMode.Sync
                : ExecutionMode.Async;
        }

        // Validate inputs
        var validationErrors = ValidateInputs(process, request.Inputs);
        if (validationErrors.Count > 0)
        {
            decision.DenialReasons.AddRange(validationErrors);
            return decision;
        }

        // Estimation
        decision.EstimatedDurationSeconds = process.ExecutionConfig.EstimatedDurationSeconds ?? 30;
        decision.EstimatedCost = CalculateEstimatedCost(selectedTier, decision.EstimatedDurationSeconds.Value);
        decision.ValidatedInputs = request.Inputs; // TODO: Actual validation and normalization

        // Admission granted!
        decision.Admitted = true;

        _logger.LogInformation(
            "Admission granted for process {ProcessId}, tier={Tier}, mode={Mode}, estimated={EstimatedSeconds}s",
            request.ProcessId, selectedTier, decision.ExecutionMode, decision.EstimatedDurationSeconds);

        return decision;
    }

    public TenantPolicyOverride GetTenantPolicyOverride(Guid tenantId, string processId)
    {
        // TODO: Load from database
        return new TenantPolicyOverride { TenantId = tenantId, ProcessId = processId };
    }

    public async Task<ProcessRun> EnqueueAsync(AdmissionDecision decision, CancellationToken ct = default)
    {
        var jobId = GenerateJobId();
        var request = decision.Request;

        var run = new ProcessRun
        {
            JobId = jobId,
            ProcessId = request.ProcessId,
            TenantId = request.TenantId,
            UserId = request.UserId,
            UserEmail = request.UserEmail,
            Status = ProcessRunStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Priority = request.Priority,
            Inputs = request.Inputs,
            ResponseFormat = request.ResponseFormat,
            WebhookUrl = request.WebhookUrl,
            ApiSurface = "OGC", // TODO: Pass from request
            IpAddress = request.Metadata?.GetValueOrDefault("ip_address")?.ToString(),
            UserAgent = request.Metadata?.GetValueOrDefault("user_agent")?.ToString(),
            Metadata = request.Metadata
        };

        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            INSERT INTO process_runs (
                job_id, process_id, tenant_id, user_id, user_email,
                status, created_at, priority, inputs, response_format,
                webhook_url, api_surface, ip_address, user_agent, metadata
            ) VALUES (
                @JobId, @ProcessId, @TenantId, @UserId, @UserEmail,
                @Status, @CreatedAt, @Priority, @Inputs::jsonb, @ResponseFormat,
                @WebhookUrl, @ApiSurface, @IpAddress, @UserAgent, @Metadata::jsonb
            )";

        await connection.ExecuteAsync(sql, new
        {
            run.JobId,
            run.ProcessId,
            TenantId = run.TenantId.ToString(),
            run.UserId,
            run.UserEmail,
            Status = run.Status.ToString().ToLowerInvariant(),
            run.CreatedAt,
            run.Priority,
            Inputs = System.Text.Json.JsonSerializer.Serialize(run.Inputs),
            run.ResponseFormat,
            run.WebhookUrl,
            run.ApiSurface,
            run.IpAddress,
            run.UserAgent,
            Metadata = run.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(run.Metadata) : null
        });

        _logger.LogInformation("Enqueued job {JobId} for process {ProcessId}", jobId, request.ProcessId);

        return run;
    }

    public async Task<ProcessResult> ExecuteInlineAsync(AdmissionDecision decision, CancellationToken ct = default)
    {
        var jobId = GenerateJobId();
        var request = decision.Request;

        _logger.LogInformation("Executing inline job {JobId} for process {ProcessId}", jobId, request.ProcessId);

        // Create a ProcessRun record for tracking
        var run = new ProcessRun
        {
            JobId = jobId,
            ProcessId = request.ProcessId,
            TenantId = request.TenantId,
            UserId = request.UserId,
            UserEmail = request.UserEmail,
            Status = ProcessRunStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            Priority = request.Priority,
            Inputs = request.Inputs,
            ResponseFormat = request.ResponseFormat,
            ApiSurface = "OGC"
        };

        // Save to database
        await SaveProcessRunAsync(run, ct);

        try
        {
            // Get process definition
            var process = await _processRegistry.GetProcessAsync(request.ProcessId, ct);
            if (process == null)
            {
                throw new InvalidOperationException($"Process '{request.ProcessId}' not found");
            }

            // Execute on selected tier
            var result = await _tierExecutor.ExecuteAsync(
                run,
                process,
                decision.SelectedTier ?? ProcessExecutionTier.NTS,
                null,
                ct);

            // Record completion
            await RecordCompletionAsync(
                jobId,
                result,
                decision.SelectedTier ?? ProcessExecutionTier.NTS,
                TimeSpan.FromMilliseconds(result.DurationMs ?? 0),
                ct);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inline execution failed for job {JobId}", jobId);

            await RecordFailureAsync(
                jobId,
                ex,
                decision.SelectedTier ?? ProcessExecutionTier.NTS,
                TimeSpan.FromMilliseconds((DateTimeOffset.UtcNow - run.CreatedAt).TotalMilliseconds),
                ct);

            throw;
        }
    }

    public async Task<ProcessRun?> GetJobStatusAsync(string jobId, Guid tenantId, CancellationToken ct = default)
    {
        // Validate job ID format and length to prevent SQL injection
        ValidateJobId(jobId);

        await using var connection = new NpgsqlConnection(_connectionString);

        // SECURITY: Add tenant filter to enforce tenant isolation
        const string sql = @"
            SELECT * FROM process_runs
            WHERE job_id = @JobId AND tenant_id = @TenantId";

        var row = await connection.QuerySingleOrDefaultAsync(sql, new
        {
            JobId = jobId,
            TenantId = tenantId.ToString()
        });

        if (row == null)
        {
            _logger.LogWarning("Job {JobId} not found or does not belong to tenant {TenantId}", jobId, tenantId);
            return null;
        }

        _logger.LogDebug("Retrieved job status for {JobId} in tenant {TenantId}", jobId, tenantId);
        return MapToProcessRun(row);
    }

    public async Task<bool> CancelJobAsync(string jobId, Guid tenantId, string? reason = null, CancellationToken ct = default)
    {
        // Validate job ID format and length to prevent SQL injection
        ValidateJobId(jobId);

        // Validate reason length to prevent abuse
        if (!string.IsNullOrEmpty(reason) && reason.Length > MaxReasonLength)
        {
            _logger.LogWarning(
                "Cancellation reason exceeds maximum length of {MaxLength} characters. Truncating.",
                MaxReasonLength);

            reason = reason.Substring(0, MaxReasonLength);
        }

        await using var connection = new NpgsqlConnection(_connectionString);

        // SECURITY: Add tenant filter to enforce tenant isolation
        const string sql = @"
            UPDATE process_runs
            SET status = 'cancelled',
                completed_at = NOW(),
                cancellation_reason = @Reason,
                duration_ms = EXTRACT(EPOCH FROM (NOW() - created_at)) * 1000
            WHERE job_id = @JobId
            AND tenant_id = @TenantId
            AND status IN ('pending', 'running')";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            JobId = jobId,
            TenantId = tenantId.ToString(),
            Reason = reason
        });

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Cancelled job {JobId} for tenant {TenantId}, reason: {Reason}",
                jobId, tenantId, reason);
        }
        else
        {
            _logger.LogWarning(
                "Failed to cancel job {JobId} for tenant {TenantId} - job not found or not cancellable",
                jobId, tenantId);
        }

        return rowsAffected > 0;
    }

    public async Task<ProcessRunQueryResult> QueryRunsAsync(ProcessRunQuery query, bool isSystemAdmin = false, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // SECURITY: For non-admin users, TenantId is REQUIRED
        if (!isSystemAdmin)
        {
            if (!query.TenantId.HasValue)
            {
                _logger.LogError("QueryRunsAsync called by non-admin without TenantId - security violation");
                throw new ArgumentException("TenantId is required for process run queries", nameof(query));
            }

            conditions.Add("tenant_id = @TenantId");
            parameters.Add("TenantId", query.TenantId.Value.ToString());
            _logger.LogDebug("Querying process runs for tenant {TenantId}", query.TenantId.Value);
        }
        else if (query.TenantId.HasValue)
        {
            // Admin can optionally filter by tenant
            conditions.Add("tenant_id = @TenantId");
            parameters.Add("TenantId", query.TenantId.Value.ToString());
            _logger.LogDebug("Admin querying process runs for tenant {TenantId}", query.TenantId.Value);
        }
        else
        {
            _logger.LogDebug("Admin querying process runs across all tenants");
        }

        if (query.UserId.HasValue)
        {
            conditions.Add("user_id = @UserId");
            parameters.Add("UserId", query.UserId.Value);
        }

        if (!string.IsNullOrEmpty(query.ProcessId))
        {
            conditions.Add("process_id = @ProcessId");
            parameters.Add("ProcessId", query.ProcessId);
        }

        if (query.Status.HasValue)
        {
            conditions.Add("status = @Status");
            parameters.Add("Status", query.Status.Value.ToString().ToLowerInvariant());
        }

        if (query.StartTime.HasValue)
        {
            conditions.Add("created_at >= @StartTime");
            parameters.Add("StartTime", query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            conditions.Add("created_at <= @EndTime");
            parameters.Add("EndTime", query.EndTime.Value);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        // Get total count
        var countSql = $"SELECT COUNT(*) FROM process_runs {whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        // Get paginated results
        var dataSql = $@"
            SELECT * FROM process_runs
            {whereClause}
            ORDER BY {query.SortBy ?? "created_at"} {(query.SortDescending ? "DESC" : "ASC")}
            LIMIT @Limit OFFSET @Offset";

        parameters.Add("Limit", query.Limit);
        parameters.Add("Offset", query.Offset);

        var rows = await connection.QueryAsync(dataSql, parameters);

        var runs = new List<ProcessRun>();
        foreach (var row in rows)
        {
            runs.Add(MapToProcessRun(row));
        }

        return new ProcessRunQueryResult
        {
            Runs = runs,
            TotalCount = totalCount,
            Offset = query.Offset,
            Limit = query.Limit
        };
    }

    public async Task<ProcessRun?> DequeueNextJobAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        // Call stored procedure to atomically dequeue next job
        const string sql = "SELECT * FROM dequeue_process_run()";

        var row = await connection.QuerySingleOrDefaultAsync(sql);

        if (row == null)
        {
            _logger.LogDebug("No pending jobs in queue");
            return null;
        }

        _logger.LogInformation("Dequeued job {JobId} for process {ProcessId}", row.job_id, row.process_id);

        // Fetch full ProcessRun details
        var processRun = await GetJobStatusAsync(row.job_id, Guid.Parse(row.tenant_id), ct);
        return processRun;
    }

    public async Task RecordCompletionAsync(string jobId, ProcessResult result, ProcessExecutionTier tier, TimeSpan duration, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            UPDATE process_runs
            SET status = 'completed',
                completed_at = NOW(),
                duration_ms = @DurationMs,
                executed_tier = @Tier,
                output = @Output::jsonb,
                output_url = @OutputUrl,
                features_processed = @FeaturesProcessed,
                compute_cost = @ComputeCost,
                total_cost = @TotalCost
            WHERE job_id = @JobId";

        await connection.ExecuteAsync(sql, new
        {
            JobId = jobId,
            DurationMs = (long)duration.TotalMilliseconds,
            Tier = tier.ToString(),
            Output = result.Output != null ? System.Text.Json.JsonSerializer.Serialize(result.Output) : null,
            result.OutputUrl,
            result.FeaturesProcessed,
            ComputeCost = CalculateComputeCost(tier, duration),
            TotalCost = CalculateComputeCost(tier, duration)
        });

        _logger.LogInformation(
            "Recorded completion for job {JobId}, tier={Tier}, duration={DurationMs}ms",
            jobId, tier, duration.TotalMilliseconds);
    }

    public async Task RecordFailureAsync(string jobId, Exception error, ProcessExecutionTier tier, TimeSpan duration, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            UPDATE process_runs
            SET status = 'failed',
                completed_at = NOW(),
                duration_ms = @DurationMs,
                executed_tier = @Tier,
                error_message = @ErrorMessage,
                error_details = @ErrorDetails
            WHERE job_id = @JobId";

        await connection.ExecuteAsync(sql, new
        {
            JobId = jobId,
            DurationMs = (long)duration.TotalMilliseconds,
            Tier = tier.ToString(),
            ErrorMessage = error.Message,
            ErrorDetails = error.ToString()
        });

        _logger.LogError(error, "Recorded failure for job {JobId}, tier={Tier}", jobId, tier);
    }

    public async Task<ProcessExecutionStatistics> GetStatisticsAsync(Guid? tenantId = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        var stats = await connection.QuerySingleAsync(
            "SELECT * FROM get_process_statistics(@TenantId, @StartTime, @EndTime)",
            new
            {
                TenantId = tenantId?.ToString(),
                StartTime = startTime,
                EndTime = endTime
            });

        return new ProcessExecutionStatistics
        {
            TotalRuns = stats.total_runs,
            SuccessfulRuns = stats.successful_runs,
            FailedRuns = stats.failed_runs,
            CancelledRuns = stats.cancelled_runs,
            PendingRuns = stats.pending_runs,
            RunningRuns = stats.running_runs,
            AverageDurationSeconds = stats.average_duration_seconds ?? 0,
            MedianDurationSeconds = stats.median_duration_seconds ?? 0,
            TotalComputeCost = stats.total_compute_cost ?? 0,
            RunsByTier = new Dictionary<ProcessExecutionTier, long>
            {
                [ProcessExecutionTier.NTS] = stats.runs_by_tier_nts,
                [ProcessExecutionTier.PostGIS] = stats.runs_by_tier_postgis,
                [ProcessExecutionTier.CloudBatch] = stats.runs_by_tier_cloud_batch
            }
        };
    }

    // Helper methods

    private async Task SaveProcessRunAsync(ProcessRun run, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            INSERT INTO process_runs (
                job_id, process_id, tenant_id, user_id, user_email,
                status, created_at, started_at, priority, inputs, response_format, api_surface
            ) VALUES (
                @JobId, @ProcessId, @TenantId, @UserId, @UserEmail,
                @Status, @CreatedAt, @StartedAt, @Priority, @Inputs::jsonb, @ResponseFormat, @ApiSurface
            )";

        await connection.ExecuteAsync(sql, new
        {
            run.JobId,
            run.ProcessId,
            TenantId = run.TenantId.ToString(),
            run.UserId,
            run.UserEmail,
            Status = run.Status.ToString().ToLowerInvariant(),
            run.CreatedAt,
            run.StartedAt,
            run.Priority,
            Inputs = System.Text.Json.JsonSerializer.Serialize(run.Inputs),
            run.ResponseFormat,
            run.ApiSurface
        });
    }

    private static ProcessRun MapToProcessRun(dynamic row)
    {
        return new ProcessRun
        {
            JobId = row.job_id,
            ProcessId = row.process_id,
            TenantId = Guid.Parse((string)row.tenant_id),
            UserId = row.user_id,
            UserEmail = row.user_email,
            Status = Enum.Parse<ProcessRunStatus>(row.status, true),
            CreatedAt = row.created_at,
            StartedAt = row.started_at,
            CompletedAt = row.completed_at,
            DurationMs = row.duration_ms,
            QueueWaitMs = row.queue_wait_ms,
            ExecutedTier = row.executed_tier != null ? Enum.Parse<ProcessExecutionTier>(row.executed_tier) : null,
            WorkerId = row.worker_id,
            CloudBatchJobId = row.cloud_batch_job_id,
            Priority = row.priority,
            Progress = row.progress,
            ProgressMessage = row.progress_message,
            Inputs = row.inputs != null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(row.inputs) : new Dictionary<string, object>(),
            Output = row.output != null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(row.output) : null,
            ResponseFormat = row.response_format,
            OutputUrl = row.output_url,
            OutputSizeBytes = row.output_size_bytes,
            ErrorMessage = row.error_message,
            ErrorDetails = row.error_details,
            RetryCount = row.retry_count,
            MaxRetries = row.max_retries,
            CancellationReason = row.cancellation_reason,
            PeakMemoryMB = row.peak_memory_mb,
            CpuTimeMs = row.cpu_time_ms,
            FeaturesProcessed = row.features_processed,
            InputSizeMB = row.input_size_mb,
            ComputeCost = row.compute_cost,
            StorageCost = row.storage_cost,
            TotalCost = row.total_cost,
            IpAddress = row.ip_address,
            UserAgent = row.user_agent,
            ApiSurface = row.api_surface,
            ClientId = row.client_id,
            WebhookUrl = row.webhook_url,
            NotifyEmail = row.notify_email,
            WebhookSentAt = row.webhook_sent_at,
            WebhookResponseStatus = row.webhook_response_status
        };
    }

    private static string GenerateJobId()
    {
        return $"job-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
    }

    private static List<string> ValidateInputs(ProcessDefinition process, Dictionary<string, object> inputs)
    {
        var errors = new List<string>();

        foreach (var param in process.Inputs)
        {
            if (param.Required && !inputs.ContainsKey(param.Name))
            {
                errors.Add($"Required parameter '{param.Name}' is missing");
            }

            if (inputs.TryGetValue(param.Name, out var value))
            {
                // TODO: Type validation, range validation, etc.
            }
        }

        return errors;
    }

    private static decimal CalculateEstimatedCost(ProcessExecutionTier tier, int estimatedDurationSeconds)
    {
        return tier switch
        {
            ProcessExecutionTier.NTS => estimatedDurationSeconds * 0.001m, // $0.001 per second
            ProcessExecutionTier.PostGIS => estimatedDurationSeconds * 0.01m, // $0.01 per second
            ProcessExecutionTier.CloudBatch => estimatedDurationSeconds * 0.1m, // $0.1 per second
            _ => 0
        };
    }

    private static decimal CalculateComputeCost(ProcessExecutionTier tier, TimeSpan duration)
    {
        var seconds = (decimal)duration.TotalSeconds;
        return tier switch
        {
            ProcessExecutionTier.NTS => seconds * 0.001m,
            ProcessExecutionTier.PostGIS => seconds * 0.01m,
            ProcessExecutionTier.CloudBatch => seconds * 0.1m,
            _ => 0
        };
    }

    /// <summary>
    /// Validates job ID format to prevent SQL injection and ensure proper format
    /// Expected format: job-YYYYMMDD-{guid}
    /// </summary>
    /// <param name="jobId">The job ID to validate</param>
    /// <exception cref="ArgumentException">Thrown when job ID is invalid</exception>
    private void ValidateJobId(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Job ID cannot be null or whitespace.", nameof(jobId));
        }

        if (jobId.Length > MaxJobIdLength)
        {
            _logger.LogWarning(
                "Job ID exceeds maximum length of {MaxLength} characters: {JobId}",
                MaxJobIdLength, jobId);

            throw new ArgumentException(
                $"Job ID exceeds maximum length of {MaxJobIdLength} characters.",
                nameof(jobId));
        }

        if (!JobIdPattern.IsMatch(jobId))
        {
            _logger.LogWarning(
                "Invalid job ID format attempted: {JobId}. This may be a SQL injection attempt.",
                jobId);

            throw new ArgumentException(
                "Invalid job ID format. Expected format: job-YYYYMMDD-{guid}",
                nameof(jobId));
        }
    }

    /// <summary>
    /// Compiled regex for job ID validation
    /// </summary>
    [GeneratedRegex(@"^job-\d{8}-[a-f0-9]{32}$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JobIdRegex();
}
