// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// Enriched error information for workflow failures
/// </summary>
public class WorkflowError
{
    /// <summary>
    /// Error identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Workflow run ID
    /// </summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>
    /// Node ID where error occurred
    /// </summary>
    public string? NodeId { get; set; }

    /// <summary>
    /// Node type
    /// </summary>
    public string? NodeType { get; set; }

    /// <summary>
    /// Error category
    /// </summary>
    public ErrorCategory Category { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Full exception stack trace
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Exception type name
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Input data that caused the error (if applicable)
    /// </summary>
    public Dictionary<string, object>? InputData { get; set; }

    /// <summary>
    /// Node state at time of failure
    /// </summary>
    public Dictionary<string, object>? NodeState { get; set; }

    /// <summary>
    /// System metrics at time of failure
    /// </summary>
    public SystemMetrics? SystemMetrics { get; set; }

    /// <summary>
    /// Suggested resolution
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// When error occurred
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Retry attempt number (0 for initial attempt)
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Related error IDs (for error patterns)
    /// </summary>
    public List<Guid>? RelatedErrors { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Create from exception
    /// </summary>
    public static WorkflowError FromException(
        Exception ex,
        Guid workflowRunId,
        string? nodeId = null,
        string? nodeType = null,
        int attemptNumber = 0,
        Dictionary<string, object>? inputData = null)
    {
        var category = ErrorCategorizer.Categorize(ex);
        var messageCategory = ErrorCategorizer.CategorizeByMessage(ex.Message);

        // Use message-based category if it's more specific
        if (category == ErrorCategory.Unknown && messageCategory != ErrorCategory.Unknown)
        {
            category = messageCategory;
        }

        return new WorkflowError
        {
            WorkflowRunId = workflowRunId,
            NodeId = nodeId,
            NodeType = nodeType,
            Category = category,
            Message = ex.Message,
            StackTrace = ex.StackTrace,
            ExceptionType = ex.GetType().FullName,
            InputData = inputData,
            Suggestion = ErrorCategorizer.GetSuggestion(category),
            AttemptNumber = attemptNumber,
            SystemMetrics = SystemMetrics.Capture()
        };
    }
}

/// <summary>
/// System metrics at time of error
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Memory usage in MB
    /// </summary>
    public long MemoryUsageMB { get; set; }

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Thread count
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// GC generation 0 collections
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// GC generation 1 collections
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// GC generation 2 collections
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Capture current system metrics
    /// </summary>
    public static SystemMetrics Capture()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();

        return new SystemMetrics
        {
            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
            ThreadCount = process.Threads.Count,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            CpuUsagePercent = 0 // Would need performance counter for accurate CPU %
        };
    }
}
