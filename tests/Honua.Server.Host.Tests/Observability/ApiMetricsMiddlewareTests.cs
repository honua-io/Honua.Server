using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Honua.Server.Host.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Host.Tests.Observability;

/// <summary>
/// Tests for ApiMetricsMiddleware covering HTTP error tracking and status code handling.
/// Tests P3 #42 - Metrics for Error Rates implementation.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
[Trait("Speed", "Slow")]
public class ApiMetricsMiddlewareTests : IDisposable
{
    private readonly ApiMetrics _metrics;
    private readonly MeterListener _meterListener;
    private readonly List<MeasurementData> _measurements;

    public ApiMetricsMiddlewareTests()
    {
        _metrics = new ApiMetrics();
        _measurements = new List<MeasurementData>();

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Honua.Server.Api")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            _measurements.Add(new MeasurementData(instrument.Name, measurement, tags.ToArray()));
        });

        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            _measurements.Add(new MeasurementData(instrument.Name, measurement, tags.ToArray()));
        });

        _meterListener.Start();
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulApiRequest_RecordsMetrics()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/ogc/collections";
        context.Response.StatusCode = 200;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should record HTTP metrics
        var requestCounter = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.requests.total");
        Assert.NotNull(requestCounter);

        var durationHistogram = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.request.duration");
        Assert.NotNull(durationHistogram);

        // Should NOT record error
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.Empty(errorMetrics);
    }

    [Fact]
    public async Task InvokeAsync_404NotFound_RecordsError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/ogc/collections/nonexistent";
        context.Response.StatusCode = 404;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 404;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should record error
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.NotEmpty(errorMetrics);

        var errorMetric = errorMetrics.First();
        Assert.Contains(errorMetric.Tags, t => t.Key == "http.status_code" && (string)t.Value! == "404");
        Assert.Contains(errorMetric.Tags, t => t.Key == "error.type" && (string)t.Value! == "not_found");
    }

    [Fact]
    public async Task InvokeAsync_500InternalServerError_RecordsError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/wfs";
        context.Response.StatusCode = 500;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 500;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.NotEmpty(errorMetrics);

        var errorMetric = errorMetrics.First();
        Assert.Contains(errorMetric.Tags, t => t.Key == "http.status_code" && (string)t.Value! == "500");
        Assert.Contains(errorMetric.Tags, t => t.Key == "http.status_class" && (string)t.Value! == "5xx");
    }

    [Fact]
    public async Task InvokeAsync_Exception_RecordsErrorMetrics()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/ogc/collections";

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) => throw new InvalidOperationException("Test exception"),
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(context));

        // Assert error metrics were recorded
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.NotEmpty(errorMetrics);

        var httpErrors = errorMetrics.Where(e =>
            e.Tags.Any(t => t.Key == "http.status_code" && (string)t.Value! == "500")).ToList();
        Assert.NotEmpty(httpErrors);
    }

    [Fact]
    public async Task InvokeAsync_ValidationError422_RecordsValidationErrorType()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/ogc/collections/test/items";
        context.Response.StatusCode = 422;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 422;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.NotEmpty(errorMetrics);

        var errorMetric = errorMetrics.First();
        Assert.Contains(errorMetric.Tags, t => t.Key == "error.type" && (string)t.Value! == "validation");
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedError_RecordsAuthErrorType()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/ogc/collections";
        context.Response.StatusCode = 401;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.NotEmpty(errorMetrics);

        var errorMetric = errorMetrics.First();
        Assert.Contains(errorMetric.Tags, t => t.Key == "error.type" && (string)t.Value! == "auth");
    }

    [Fact]
    public async Task InvokeAsync_NonApiRequest_StillRecordsHttpMetrics()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/health";
        context.Response.StatusCode = 200;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should still record HTTP-level metrics even for non-API requests
        var requestCounter = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.requests.total");
        Assert.NotNull(requestCounter);

        var durationHistogram = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.request.duration");
        Assert.NotNull(durationHistogram);
    }

    [Fact]
    public async Task InvokeAsync_TracksLatencyCorrectly()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/wms";
        context.Response.StatusCode = 200;

        var middleware = new ApiMetricsMiddleware(
            next: async (ctx) =>
            {
                await Task.Delay(100); // Simulate some processing time
                ctx.Response.StatusCode = 200;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - latency should be recorded and be > 100ms
        var durationHistogram = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.request.duration");
        Assert.NotNull(durationHistogram);

        var latency = (double)durationHistogram.Value;
        Assert.True(latency >= 100, $"Expected latency >= 100ms, but got {latency}ms");
    }

    [Fact]
    public async Task InvokeAsync_DifferentEndpoints_TrackedSeparately()
    {
        // Arrange
        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        var context1 = new DefaultHttpContext();
        context1.Request.Method = "GET";
        context1.Request.Path = "/wfs";
        context1.Response.StatusCode = 200;

        var context2 = new DefaultHttpContext();
        context2.Request.Method = "GET";
        context2.Request.Path = "/wms";
        context2.Response.StatusCode = 200;

        // Act
        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        // Assert
        var histograms = _measurements.Where(m =>
            m.InstrumentName == "honua.http.request.duration").ToList();

        Assert.Equal(2, histograms.Count);

        var wfsMeasurement = histograms.FirstOrDefault(h =>
            h.Tags.Any(t => t.Key == "http.endpoint" && t.Value!.ToString()!.Contains("wfs")));
        var wmsMeasurement = histograms.FirstOrDefault(h =>
            h.Tags.Any(t => t.Key == "http.endpoint" && t.Value!.ToString()!.Contains("wms")));

        Assert.NotNull(wfsMeasurement);
        Assert.NotNull(wmsMeasurement);
    }

    [Fact]
    public async Task InvokeAsync_RateLimitError_RecordsCorrectErrorType()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/ogc/collections";
        context.Response.StatusCode = 429;

        var middleware = new ApiMetricsMiddleware(
            next: (ctx) =>
            {
                ctx.Response.StatusCode = 429;
                return Task.CompletedTask;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();
        Assert.NotEmpty(errorMetrics);

        var errorMetric = errorMetrics.First();
        Assert.Contains(errorMetric.Tags, t => t.Key == "error.type" && (string)t.Value! == "rate_limit");
    }

    [Fact]
    public async Task InvokeAsync_SlowRequest_RecordsSlowRequestMetric()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/wms";
        context.Response.StatusCode = 200;

        var middleware = new ApiMetricsMiddleware(
            next: async (ctx) =>
            {
                await Task.Delay(1100); // Simulate slow request >1s
                ctx.Response.StatusCode = 200;
            },
            _metrics,
            NullLogger<ApiMetricsMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should record slow request metric
        var slowRequestMetrics = _measurements.Where(m =>
            m.InstrumentName == "honua.http.slow_requests.total").ToList();
        Assert.NotEmpty(slowRequestMetrics);

        var slowRequestMetric = slowRequestMetrics.First();
        Assert.Contains(slowRequestMetric.Tags, t => t.Key == "latency_threshold" && (string)t.Value! == "1s");
    }

    public void Dispose()
    {
        _meterListener?.Dispose();
        _metrics?.Dispose();
    }

    private record MeasurementData(string InstrumentName, object Value, KeyValuePair<string, object?>[] Tags);
}
