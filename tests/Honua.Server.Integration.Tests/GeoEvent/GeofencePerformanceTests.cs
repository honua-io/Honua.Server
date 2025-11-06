// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Dto;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Integration.Tests.GeoEvent;

/// <summary>
/// Performance tests for geofence evaluation
/// Validates P95 latency targets and throughput requirements
/// </summary>
public class GeofencePerformanceTests : IAsyncLifetime, IClassFixture<GeoEventTestFixture>
{
    private readonly GeoEventTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly List<Guid> _geofenceIds = new();

    public GeofencePerformanceTests(GeoEventTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Create 1000 test geofences for performance testing
        _output.WriteLine("Creating 1000 test geofences...");
        var createTasks = new List<Task<Guid>>();

        for (int i = 0; i < 1000; i++)
        {
            createTasks.Add(CreateTestGeofenceAsync($"PerfTest-{i}", i));

            // Batch in groups of 50 to avoid overwhelming the server
            if (createTasks.Count >= 50)
            {
                var ids = await Task.WhenAll(createTasks);
                _geofenceIds.AddRange(ids);
                createTasks.Clear();
            }
        }

        if (createTasks.Any())
        {
            var ids = await Task.WhenAll(createTasks);
            _geofenceIds.AddRange(ids);
        }

        _output.WriteLine($"Created {_geofenceIds.Count} geofences for testing");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SingleEvaluation_With1000Geofences_ShouldMeetP95Target()
    {
        // Arrange
        var entityId = "perf-test-single";
        var request = new EvaluateLocationRequest
        {
            EntityId = entityId,
            Location = new GeoJsonPoint { Coordinates = new[] { -122.4, 37.8 } }
        };

        // Warmup
        await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);

        var latencies = new List<double>();

        // Act - Run 100 evaluations to get P95
        for (int i = 0; i < 100; i++)
        {
            request.EntityId = $"{entityId}-{i}";

            var sw = Stopwatch.StartNew();
            var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);
            sw.Stop();

            response.EnsureSuccessStatusCode();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert
        var sorted = latencies.OrderBy(x => x).ToList();
        var p50 = sorted[(int)(sorted.Count * 0.50)];
        var p95 = sorted[(int)(sorted.Count * 0.95)];
        var p99 = sorted[(int)(sorted.Count * 0.99)];
        var avg = latencies.Average();

        _output.WriteLine($"Latency Stats (1000 geofences):");
        _output.WriteLine($"  P50: {p50:F2}ms");
        _output.WriteLine($"  P95: {p95:F2}ms");
        _output.WriteLine($"  P99: {p99:F2}ms");
        _output.WriteLine($"  Avg: {avg:F2}ms");

        // Target: P95 < 100ms for 1,000 geofences
        p95.Should().BeLessThan(100, "P95 latency should be under 100ms for 1000 geofences");
    }

    [Fact]
    public async Task BatchEvaluation_100Events_ShouldProcessEfficiently()
    {
        // Arrange
        var requests = new List<EvaluateLocationRequest>();
        for (int i = 0; i < 100; i++)
        {
            requests.Add(new EvaluateLocationRequest
            {
                EntityId = $"batch-entity-{i}",
                Location = new GeoJsonPoint
                {
                    Coordinates = new[] { -122.4 + (i * 0.001), 37.8 + (i * 0.001) }
                }
            });
        }

        // Warmup
        await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate/batch", requests);

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate/batch", requests);
        sw.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BatchEvaluateLocationResponse>();

        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(100);

        var throughput = 100 / sw.Elapsed.TotalSeconds;

        _output.WriteLine($"Batch Performance (100 events):");
        _output.WriteLine($"  Total time: {sw.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Throughput: {throughput:F2} events/sec");

        // Target: 100 events/second sustained
        throughput.Should().BeGreaterThan(100, "Should process at least 100 events per second");
    }

    [Fact]
    public async Task BatchEvaluation_1000Events_MaxBatch_ShouldComplete()
    {
        // Arrange
        var requests = new List<EvaluateLocationRequest>();
        for (int i = 0; i < 1000; i++)
        {
            requests.Add(new EvaluateLocationRequest
            {
                EntityId = $"max-batch-entity-{i}",
                Location = new GeoJsonPoint
                {
                    Coordinates = new[] { -122.4 + (i * 0.0001), 37.8 + (i * 0.0001) }
                }
            });
        }

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate/batch", requests);
        sw.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BatchEvaluateLocationResponse>();

        result.Should().NotBeNull();
        result!.TotalProcessed.Should().Be(1000);

        _output.WriteLine($"Max Batch Performance (1000 events):");
        _output.WriteLine($"  Total time: {sw.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Avg per event: {sw.Elapsed.TotalMilliseconds / 1000:F2}ms");

        // Should complete in reasonable time (< 10 seconds)
        sw.Elapsed.TotalSeconds.Should().BeLessThan(10, "Max batch should complete in under 10 seconds");
    }

    [Fact]
    public async Task ConcurrentEvaluations_10Parallel_ShouldHandleLoad()
    {
        // Arrange
        var tasks = new List<Task<double>>();

        // Act - Send 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            var entityId = $"concurrent-{i}";
            tasks.Add(MeasureEvaluationLatencyAsync(entityId));
        }

        var latencies = await Task.WhenAll(tasks);

        // Assert
        var maxLatency = latencies.Max();
        var avgLatency = latencies.Average();

        _output.WriteLine($"Concurrent Evaluation Performance (10 parallel):");
        _output.WriteLine($"  Max latency: {maxLatency:F2}ms");
        _output.WriteLine($"  Avg latency: {avgLatency:F2}ms");

        // Should handle concurrent load without excessive degradation
        maxLatency.Should().BeLessThan(500, "Max latency under concurrent load should be reasonable");
    }

    [Fact]
    public async Task RepeatedEvaluations_SameEntity_ShouldBeCached()
    {
        // Arrange
        var entityId = "cache-test-entity";
        var request = new EvaluateLocationRequest
        {
            EntityId = entityId,
            Location = new GeoJsonPoint { Coordinates = new[] { -122.4, 37.8 } }
        };

        // First evaluation (cold)
        var sw1 = Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);
        sw1.Stop();

        // Subsequent evaluations (potentially cached state)
        var sw2 = Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);
        sw2.Stop();

        var sw3 = Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);
        sw3.Stop();

        _output.WriteLine($"Cache Performance:");
        _output.WriteLine($"  First eval: {sw1.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Second eval: {sw2.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Third eval: {sw3.Elapsed.TotalMilliseconds:F2}ms");

        // Subsequent evaluations should generally be faster or similar
        // (Note: This is not a strict requirement, but good to measure)
        _output.WriteLine($"  Speedup: {sw1.Elapsed.TotalMilliseconds / sw2.Elapsed.TotalMilliseconds:F2}x");
    }

    [Fact]
    public async Task SpatialQuery_Performance_WithManyGeofences()
    {
        // Arrange - Test spatial query performance
        var request = new EvaluateLocationRequest
        {
            EntityId = "spatial-query-test",
            Location = new GeoJsonPoint { Coordinates = new[] { -122.4, 37.8 } }
        };

        var latencies = new List<double>();

        // Act - Run 50 evaluations with different locations
        for (int i = 0; i < 50; i++)
        {
            request.Location.Coordinates = new[]
            {
                -122.4 + (i * 0.01),
                37.8 + (i * 0.01)
            };

            var sw = Stopwatch.StartNew();
            await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);
            sw.Stop();

            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert
        var avg = latencies.Average();
        var max = latencies.Max();

        _output.WriteLine($"Spatial Query Performance:");
        _output.WriteLine($"  Avg: {avg:F2}ms");
        _output.WriteLine($"  Max: {max:F2}ms");

        // Spatial queries should be efficient with GIST index
        avg.Should().BeLessThan(100, "Average spatial query should be fast with proper indexing");
    }

    // Helper methods

    private async Task<double> MeasureEvaluationLatencyAsync(string entityId)
    {
        var request = new EvaluateLocationRequest
        {
            EntityId = entityId,
            Location = new GeoJsonPoint { Coordinates = new[] { -122.4, 37.8 } }
        };

        var sw = Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/v1/geoevent/evaluate", request);
        sw.Stop();

        return sw.Elapsed.TotalMilliseconds;
    }

    private async Task<Guid> CreateTestGeofenceAsync(string name, int index)
    {
        // Distribute geofences across a grid
        var lonOffset = (index % 100) * 0.1;
        var latOffset = (index / 100) * 0.1;

        var request = new CreateGeofenceRequest
        {
            Name = name,
            Geometry = new GeoJsonGeometry
            {
                Type = "Polygon",
                Coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.0 + lonOffset, 37.0 + latOffset },
                        new[] { -122.0 + lonOffset + 0.05, 37.0 + latOffset },
                        new[] { -122.0 + lonOffset + 0.05, 37.0 + latOffset + 0.05 },
                        new[] { -122.0 + lonOffset, 37.0 + latOffset + 0.05 },
                        new[] { -122.0 + lonOffset, 37.0 + latOffset }
                    }
                }
            },
            IsActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/v1/geofences", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeofenceResponse>();
        return result!.Id;
    }
}
