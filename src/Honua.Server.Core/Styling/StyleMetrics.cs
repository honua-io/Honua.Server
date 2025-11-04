// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// OpenTelemetry metrics for style operations
/// </summary>
public sealed class StyleMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _styleCreatedCounter;
    private readonly Counter<long> _styleUpdatedCounter;
    private readonly Counter<long> _styleDeletedCounter;
    private readonly Counter<long> _styleValidationCounter;
    private readonly Histogram<double> _styleOperationDuration;
    private readonly Counter<long> _styleErrorCounter;

    public StyleMetrics()
    {
        _meter = new Meter("Honua.Styles", "1.0.0");

        _styleCreatedCounter = _meter.CreateCounter<long>(
            "honua.styles.created",
            unit: "{style}",
            description: "Number of styles created");

        _styleUpdatedCounter = _meter.CreateCounter<long>(
            "honua.styles.updated",
            unit: "{style}",
            description: "Number of styles updated");

        _styleDeletedCounter = _meter.CreateCounter<long>(
            "honua.styles.deleted",
            unit: "{style}",
            description: "Number of styles deleted");

        _styleValidationCounter = _meter.CreateCounter<long>(
            "honua.styles.validated",
            unit: "{validation}",
            description: "Number of style validations performed");

        _styleOperationDuration = _meter.CreateHistogram<double>(
            "honua.styles.operation.duration",
            unit: "ms",
            description: "Duration of style operations");

        _styleErrorCounter = _meter.CreateCounter<long>(
            "honua.styles.errors",
            unit: "{error}",
            description: "Number of style operation errors");
    }

    public void RecordStyleCreated(string styleId, string format, string? userName = null)
    {
        var tags = new TagList
        {
            { "style.id", styleId },
            { "style.format", format },
            { "user", userName ?? "anonymous" }
        };

        _styleCreatedCounter.Add(1, tags);
    }

    public void RecordStyleUpdated(string styleId, string format, int version, string? userName = null)
    {
        var tags = new TagList
        {
            { "style.id", styleId },
            { "style.format", format },
            { "style.version", version },
            { "user", userName ?? "anonymous" }
        };

        _styleUpdatedCounter.Add(1, tags);
    }

    public void RecordStyleDeleted(string styleId, string? userName = null)
    {
        var tags = new TagList
        {
            { "style.id", styleId },
            { "user", userName ?? "anonymous" }
        };

        _styleDeletedCounter.Add(1, tags);
    }

    public void RecordStyleValidation(string format, bool isValid, int errorCount, int warningCount)
    {
        var tags = new TagList
        {
            { "style.format", format },
            { "validation.result", isValid ? "valid" : "invalid" },
            { "validation.errors", errorCount },
            { "validation.warnings", warningCount }
        };

        _styleValidationCounter.Add(1, tags);
    }

    public void RecordOperationDuration(string operation, double durationMs, bool success = true)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "success", success }
        };

        _styleOperationDuration.Record(durationMs, tags);
    }

    public void RecordError(string operation, string errorType)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "error.type", errorType }
        };

        _styleErrorCounter.Add(1, tags);
    }

    public IDisposable? MeasureOperation(string operation)
    {
        return new OperationTimer(this, operation);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }

    private sealed class OperationTimer : IDisposable
    {
        private readonly StyleMetrics _metrics;
        private readonly string _operation;
        private readonly Stopwatch _stopwatch;
        private bool _success = true;

        public OperationTimer(StyleMetrics metrics, string operation)
        {
            _metrics = metrics;
            _operation = operation;
            _stopwatch = Stopwatch.StartNew();
        }

        public void MarkFailure()
        {
            _success = false;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.RecordOperationDuration(_operation, _stopwatch.Elapsed.TotalMilliseconds, _success);
        }
    }
}
