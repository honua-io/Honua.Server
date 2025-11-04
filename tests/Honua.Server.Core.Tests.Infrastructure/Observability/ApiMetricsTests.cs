using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Honua.Server.Core.Observability;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Observability;

/// <summary>
/// Comprehensive tests for ApiMetrics including HTTP error rate tracking and latency metrics.
/// Tests cover P3 #42 - Metrics for Error Rates implementation.
/// </summary>
[Trait("Category", "Unit")]
public class ApiMetricsTests : IDisposable
{
    private readonly ApiMetrics _metrics;
    private readonly MeterListener _meterListener;
    private readonly List<MeasurementData> _measurements;

    public ApiMetricsTests()
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
    public void RecordHttpRequest_RecordsLatencyHistogram()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/collections/123";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var histogramMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.request.duration");

        Assert.NotNull(histogramMeasurement);
        Assert.Equal(150.0, histogramMeasurement.Value);
        Assert.Contains(histogramMeasurement.Tags, t => t.Key == "http.method" && (string)t.Value! == "GET");
        Assert.Contains(histogramMeasurement.Tags, t => t.Key == "http.status_code" && (string)t.Value! == "200");
        Assert.Contains(histogramMeasurement.Tags, t => t.Key == "http.status_class" && (string)t.Value! == "2xx");
    }

    [Fact]
    public void RecordHttpRequest_RecordsRequestCounter()
    {
        // Arrange
        var method = "POST";
        var endpoint = "/api/items";
        var statusCode = 201;
        var duration = TimeSpan.FromMilliseconds(250);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var counterMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.requests.total");

        Assert.NotNull(counterMeasurement);
        Assert.Equal(1L, counterMeasurement.Value);
        Assert.Contains(counterMeasurement.Tags, t => t.Key == "http.method" && (string)t.Value! == "POST");
    }

    [Fact]
    public void RecordHttpRequest_With4xxStatus_RecordsError()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/collections/nonexistent";
        var statusCode = 404;
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(errorMeasurement);
        Assert.Equal(1L, errorMeasurement.Value);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_code" && (string)t.Value! == "404");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_class" && (string)t.Value! == "4xx");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "not_found");
    }

    [Fact]
    public void RecordHttpRequest_With5xxStatus_RecordsError()
    {
        // Arrange
        var method = "POST";
        var endpoint = "/api/process";
        var statusCode = 500;
        var duration = TimeSpan.FromMilliseconds(1000);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(errorMeasurement);
        Assert.Equal(1L, errorMeasurement.Value);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_code" && (string)t.Value! == "500");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_class" && (string)t.Value! == "5xx");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "internal_server_error");
    }

    [Fact]
    public void RecordHttpError_RecordsWithCorrectErrorType()
    {
        // Arrange
        var method = "POST";
        var endpoint = "/api/items";
        var statusCode = 400;
        var errorType = "validation";

        // Act
        _metrics.RecordHttpError(method, endpoint, statusCode, errorType);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(errorMeasurement);
        Assert.Equal(1L, errorMeasurement.Value);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "validation");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_class" && (string)t.Value! == "4xx");
    }

    [Fact]
    public void RecordHttpRequest_NormalizesEndpoint()
    {
        // Arrange - endpoint with ID should be normalized
        var method = "GET";
        var endpoint = "/collections/my-collection-12345/items/item-67890";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var histogramMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.request.duration");

        Assert.NotNull(histogramMeasurement);
        // Should normalize to /collections/{id}/items/{id}
        Assert.Contains(histogramMeasurement.Tags, t =>
            t.Key == "http.endpoint" &&
            t.Value!.ToString()!.Contains("{id}"));
    }

    [Fact]
    public void RecordHttpRequest_AuthError_HasAuthErrorType()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/secure/resource";
        var statusCode = 401;
        var duration = TimeSpan.FromMilliseconds(25);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(errorMeasurement);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "unauthorized");
    }

    [Fact]
    public void RecordHttpRequest_ValidationError_HasValidationErrorType()
    {
        // Arrange
        var method = "POST";
        var endpoint = "/api/items";
        var statusCode = 422;
        var duration = TimeSpan.FromMilliseconds(75);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(errorMeasurement);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "validation_error");
    }

    [Fact]
    public void RecordHttpRequest_ServiceUnavailable_HasCorrectErrorType()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/data";
        var statusCode = 503;
        var duration = TimeSpan.FromMilliseconds(5000);

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(errorMeasurement);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "service_unavailable");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_class" && (string)t.Value! == "5xx");
    }

    [Fact]
    public void RecordHttpRequest_MultipleRequests_RecordsAllLatencies()
    {
        // Arrange & Act
        _metrics.RecordHttpRequest("GET", "/api/endpoint1", 200, TimeSpan.FromMilliseconds(100));
        _metrics.RecordHttpRequest("GET", "/api/endpoint1", 200, TimeSpan.FromMilliseconds(200));
        _metrics.RecordHttpRequest("GET", "/api/endpoint1", 200, TimeSpan.FromMilliseconds(300));

        // Assert
        var histogramMeasurements = _measurements.Where(m =>
            m.InstrumentName == "honua.http.request.duration").ToList();

        Assert.Equal(3, histogramMeasurements.Count);
        Assert.Contains(histogramMeasurements, m => Math.Abs((double)m.Value - 100.0) < 0.01);
        Assert.Contains(histogramMeasurements, m => Math.Abs((double)m.Value - 200.0) < 0.01);
        Assert.Contains(histogramMeasurements, m => Math.Abs((double)m.Value - 300.0) < 0.01);
    }

    [Fact]
    public void RecordHttpRequest_DifferentEndpoints_TrackedSeparately()
    {
        // Arrange & Act
        _metrics.RecordHttpRequest("GET", "/api/endpoint1", 200, TimeSpan.FromMilliseconds(100));
        _metrics.RecordHttpRequest("GET", "/api/endpoint2", 200, TimeSpan.FromMilliseconds(200));

        // Assert
        var histogramMeasurements = _measurements.Where(m =>
            m.InstrumentName == "honua.http.request.duration").ToList();

        Assert.Equal(2, histogramMeasurements.Count);

        var endpoint1 = histogramMeasurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == "http.endpoint" && t.Value!.ToString()!.Contains("endpoint1")));
        var endpoint2 = histogramMeasurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == "http.endpoint" && t.Value!.ToString()!.Contains("endpoint2")));

        Assert.NotNull(endpoint1);
        Assert.NotNull(endpoint2);
    }

    [Fact]
    public void RecordHttpRequest_DifferentMethods_TrackedSeparately()
    {
        // Arrange & Act
        _metrics.RecordHttpRequest("GET", "/api/items", 200, TimeSpan.FromMilliseconds(100));
        _metrics.RecordHttpRequest("POST", "/api/items", 201, TimeSpan.FromMilliseconds(250));
        _metrics.RecordHttpRequest("DELETE", "/api/items", 204, TimeSpan.FromMilliseconds(50));

        // Assert
        var counterMeasurements = _measurements.Where(m =>
            m.InstrumentName == "honua.http.requests.total").ToList();

        Assert.Equal(3, counterMeasurements.Count);
        Assert.Contains(counterMeasurements, m => m.Tags.Any(t => t.Key == "http.method" && (string)t.Value! == "GET"));
        Assert.Contains(counterMeasurements, m => m.Tags.Any(t => t.Key == "http.method" && (string)t.Value! == "POST"));
        Assert.Contains(counterMeasurements, m => m.Tags.Any(t => t.Key == "http.method" && (string)t.Value! == "DELETE"));
    }

    [Fact]
    public void RecordHttpRequest_SuccessfulRequest_DoesNotRecordError()
    {
        // Arrange & Act
        _metrics.RecordHttpRequest("GET", "/api/items", 200, TimeSpan.FromMilliseconds(100));

        // Assert - should have histogram and counter, but no error counter
        var errorMeasurements = _measurements.Where(m =>
            m.InstrumentName == "honua.http.errors.total").ToList();

        Assert.Empty(errorMeasurements);
    }

    [Fact]
    public void RecordRequest_RecordsApiLevelMetrics()
    {
        // Arrange
        var apiProtocol = "ogc-api-features";
        var serviceId = "test-service";
        var layerId = "test-layer";

        // Act
        _metrics.RecordRequest(apiProtocol, serviceId, layerId);

        // Assert
        var requestMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.api.requests");

        Assert.NotNull(requestMeasurement);
        Assert.Equal(1L, requestMeasurement.Value);
        Assert.Contains(requestMeasurement.Tags, t => t.Key == "api.protocol" && (string)t.Value! == "ogc-api-features");
        Assert.Contains(requestMeasurement.Tags, t => t.Key == "service.id" && (string)t.Value! == "test-service");
        Assert.Contains(requestMeasurement.Tags, t => t.Key == "layer.id" && (string)t.Value! == "test-layer");
    }

    [Fact]
    public void RecordError_RecordsApiLevelError()
    {
        // Arrange
        var apiProtocol = "wfs";
        var serviceId = "test-service";
        var layerId = "test-layer";
        var errorType = "database_error";

        // Act
        _metrics.RecordError(apiProtocol, serviceId, layerId, errorType);

        // Assert
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.api.errors");

        Assert.NotNull(errorMeasurement);
        Assert.Equal(1L, errorMeasurement.Value);
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "error.type" && (string)t.Value! == "database_error");
    }

    [Fact]
    public void RecordHttpRequest_SlowRequest1Second_RecordsSlowRequestMetric()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/slow-endpoint";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(1500); // >1s

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var slowRequestMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.slow_requests.total");

        Assert.NotNull(slowRequestMeasurement);
        Assert.Equal(1L, slowRequestMeasurement.Value);
        Assert.Contains(slowRequestMeasurement.Tags, t => t.Key == "latency_threshold" && (string)t.Value! == "1s");
        Assert.Contains(slowRequestMeasurement.Tags, t => t.Key == "http.method" && (string)t.Value! == "GET");
    }

    [Fact]
    public void RecordHttpRequest_SlowRequest5Seconds_RecordsCorrectThreshold()
    {
        // Arrange
        var method = "POST";
        var endpoint = "/api/very-slow-endpoint";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(7000); // >5s

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var slowRequestMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.slow_requests.total");

        Assert.NotNull(slowRequestMeasurement);
        Assert.Equal(1L, slowRequestMeasurement.Value);
        Assert.Contains(slowRequestMeasurement.Tags, t => t.Key == "latency_threshold" && (string)t.Value! == "5s");
    }

    [Fact]
    public void RecordHttpRequest_SlowRequest10Seconds_RecordsExtremelySlowThreshold()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/extremely-slow-endpoint";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(12000); // >10s

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var slowRequestMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.slow_requests.total");

        Assert.NotNull(slowRequestMeasurement);
        Assert.Equal(1L, slowRequestMeasurement.Value);
        Assert.Contains(slowRequestMeasurement.Tags, t => t.Key == "latency_threshold" && (string)t.Value! == "10s");
    }

    [Fact]
    public void RecordHttpRequest_FastRequest_DoesNotRecordSlowRequestMetric()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/api/fast-endpoint";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(500); // <1s

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var slowRequestMeasurements = _measurements.Where(m =>
            m.InstrumentName == "honua.http.slow_requests.total").ToList();

        Assert.Empty(slowRequestMeasurements);
    }

    [Fact]
    public void RecordHttpRequest_MultipleDifferentSlowRequests_RecordsAllCorrectly()
    {
        // Arrange & Act
        _metrics.RecordHttpRequest("GET", "/api/endpoint1", 200, TimeSpan.FromMilliseconds(1200)); // >1s
        _metrics.RecordHttpRequest("GET", "/api/endpoint2", 200, TimeSpan.FromMilliseconds(6000)); // >5s
        _metrics.RecordHttpRequest("GET", "/api/endpoint3", 200, TimeSpan.FromMilliseconds(11000)); // >10s
        _metrics.RecordHttpRequest("GET", "/api/endpoint4", 200, TimeSpan.FromMilliseconds(800)); // fast

        // Assert
        var slowRequestMeasurements = _measurements.Where(m =>
            m.InstrumentName == "honua.http.slow_requests.total").ToList();

        Assert.Equal(3, slowRequestMeasurements.Count);

        var threshold1s = slowRequestMeasurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == "latency_threshold" && (string)t.Value! == "1s"));
        var threshold5s = slowRequestMeasurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == "latency_threshold" && (string)t.Value! == "5s"));
        var threshold10s = slowRequestMeasurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == "latency_threshold" && (string)t.Value! == "10s"));

        Assert.NotNull(threshold1s);
        Assert.NotNull(threshold5s);
        Assert.NotNull(threshold10s);
    }

    [Fact]
    public void RecordHttpRequest_SlowRequestWithError_RecordsBothSlowAndError()
    {
        // Arrange
        var method = "POST";
        var endpoint = "/api/slow-error-endpoint";
        var statusCode = 500;
        var duration = TimeSpan.FromMilliseconds(6000); // >5s

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert - should record both slow request and error
        var slowRequestMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.slow_requests.total");
        var errorMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.errors.total");

        Assert.NotNull(slowRequestMeasurement);
        Assert.NotNull(errorMeasurement);
        Assert.Contains(slowRequestMeasurement.Tags, t => t.Key == "latency_threshold" && (string)t.Value! == "5s");
        Assert.Contains(errorMeasurement.Tags, t => t.Key == "http.status_code" && (string)t.Value! == "500");
    }

    [Fact]
    public void RecordHttpRequest_SlowRequestTracksEndpoint()
    {
        // Arrange
        var method = "GET";
        var endpoint = "/collections/test-collection/items/test-item";
        var statusCode = 200;
        var duration = TimeSpan.FromMilliseconds(3000); // >1s

        // Act
        _metrics.RecordHttpRequest(method, endpoint, statusCode, duration);

        // Assert
        var slowRequestMeasurement = _measurements.FirstOrDefault(m =>
            m.InstrumentName == "honua.http.slow_requests.total");

        Assert.NotNull(slowRequestMeasurement);
        // Should have normalized endpoint
        Assert.Contains(slowRequestMeasurement.Tags, t =>
            t.Key == "http.endpoint" && t.Value!.ToString()!.Contains("{id}"));
    }

    public void Dispose()
    {
        _meterListener?.Dispose();
        _metrics?.Dispose();
    }

    private record MeasurementData(string InstrumentName, object Value, KeyValuePair<string, object?>[] Tags);
}
