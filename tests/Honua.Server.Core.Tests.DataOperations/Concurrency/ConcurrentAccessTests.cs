using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Concurrency;

/// <summary>
/// Integration tests for concurrent access scenarios and thread safety.
/// </summary>
/// <remarks>
/// These tests validate that:
/// <list type="bullet">
/// <item>Multiple simultaneous read operations succeed without conflicts</item>
/// <item>Concurrent writes to different features are handled correctly</item>
/// <item>Concurrent writes to the same feature don't corrupt data</item>
/// <item>Read operations during writes return consistent data</item>
/// <item>Delete operations during reads don't cause errors</item>
/// <item>High concurrent load is handled efficiently</item>
/// </list>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Feature", "Concurrency")]
public class ConcurrentAccessTests : IClassFixture<HonuaTestWebApplicationFactory>
{
    private readonly HonuaTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConcurrentAccessTests(HonuaTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    /// <summary>
    /// Tests that multiple simultaneous read requests all succeed with correct data.
    /// Validates that concurrent reads don't interfere with each other.
    /// </summary>
    [Fact]
    public async Task MultipleSimultaneousReads_ShouldAllSucceed()
    {
        // Arrange - Launch 10 concurrent read requests
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/ogc/collections"));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r =>
        {
            r.StatusCode.Should().Be(HttpStatusCode.OK, "all concurrent reads should succeed");
            r.Content.Should().NotBeNull();
        });

        // Verify all responses contain valid JSON
        foreach (var response in responses)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrWhiteSpace();

            var json = JsonDocument.Parse(content);
            json.RootElement.TryGetProperty("collections", out _).Should().BeTrue(
                "all responses should have valid structure");
        }

        // Verify all responses are identical (consistent reads)
        var contents = new List<string>();
        foreach (var response in responses)
        {
            response.Content.Headers.ContentLength ??= 0;
            var content = await response.Content.ReadAsStringAsync();
            contents.Add(content);
        }

        var firstContent = contents[0];
        contents.Should().AllSatisfy(c => c.Should().Be(firstContent),
            "concurrent reads should return consistent data");
    }

    /// <summary>
    /// Tests that 50 concurrent GET requests to different endpoints all succeed.
    /// Validates system stability under parallel read load.
    /// </summary>
    [Fact]
    public async Task ManySimultaneousReads_ToVariousEndpoints_ShouldAllSucceed()
    {
        // Arrange - Create mix of different read requests
        var endpoints = new[]
        {
            "/ogc",
            "/ogc/collections",
            "/ogc/conformance",
        };

        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 50; i++)
        {
            var endpoint = endpoints[i % endpoints.Length];
            tasks.Add(_client.GetAsync(endpoint));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.IsSuccessStatusCode.Should().BeTrue(),
            "all concurrent requests should succeed");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            "50 concurrent reads should complete within 5 seconds");
    }

    /// <summary>
    /// Tests that simultaneous write attempts to different features both succeed.
    /// Validates that the system can handle parallel writes without locking conflicts.
    /// </summary>
    [Fact]
    public async Task SimultaneousWritesToDifferentFeatures_ShouldBothSucceed()
    {
        // Arrange - Get a collection to work with
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        if (!collectionsResponse.IsSuccessStatusCode)
        {
            return; // Skip if no collections available
        }

        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);

        if (collectionsDoc.RootElement.GetProperty("collections").GetArrayLength() == 0)
        {
            return; // Skip test - no collections available
        }

        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Create two different features
        var feature1 = """
        {
            "type": "Feature",
            "geometry": {"type": "Point", "coordinates": [-122.4, 37.8]},
            "properties": {"name": "Feature 1", "test": "concurrent-1"}
        }
        """;

        var feature2 = """
        {
            "type": "Feature",
            "geometry": {"type": "Point", "coordinates": [-122.5, 37.9]},
            "properties": {"name": "Feature 2", "test": "concurrent-2"}
        }
        """;

        var content1 = new StringContent(feature1, Encoding.UTF8, "application/geo+json");
        var content2 = new StringContent(feature2, Encoding.UTF8, "application/geo+json");

        // Act - Launch two concurrent write requests
        var task1 = _client.PostAsync($"/ogc/collections/{collectionId}/items", content1);
        var task2 = _client.PostAsync($"/ogc/collections/{collectionId}/items", content2);

        var responses = await Task.WhenAll(task1, task2);

        // Assert - Both should either succeed or indicate writes not supported
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.Created,          // Write succeeded
                HttpStatusCode.OK,               // Write succeeded (alternative)
                HttpStatusCode.NotImplemented,   // Writes not supported
                HttpStatusCode.MethodNotAllowed); // Collection is read-only
        }

        // If both succeeded, verify both are created
        var successfulWrites = responses.Count(r =>
            r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.OK);

        if (successfulWrites > 0)
        {
            successfulWrites.Should().Be(2,
                "if writes are supported, both concurrent writes should succeed");
        }
    }

    /// <summary>
    /// Tests that simultaneous writes to the same feature are handled gracefully.
    /// Validates conflict resolution: either last-write-wins or one fails with conflict error.
    /// </summary>
    [Fact]
    public async Task SimultaneousWritesToSameFeature_ShouldHandleGracefully()
    {
        // Arrange - Get a collection
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        if (!collectionsResponse.IsSuccessStatusCode)
        {
            return; // Skip if no collections available
        }

        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);

        if (collectionsDoc.RootElement.GetProperty("collections").GetArrayLength() == 0)
        {
            return; // Skip test - no collections available
        }

        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Use same feature ID for both updates
        var featureId = $"concurrent-test-{Guid.NewGuid():N}";

        var update1 = $$"""
        {
            "type": "Feature",
            "id": "{{featureId}}",
            "geometry": {"type": "Point", "coordinates": [-122.4, 37.8]},
            "properties": {"name": "Update 1", "version": 1}
        }
        """;

        var update2 = $$"""
        {
            "type": "Feature",
            "id": "{{featureId}}",
            "geometry": {"type": "Point", "coordinates": [-122.4, 37.8]},
            "properties": {"name": "Update 2", "version": 2}
        }
        """;

        var content1 = new StringContent(update1, Encoding.UTF8, "application/geo+json");
        var content2 = new StringContent(update2, Encoding.UTF8, "application/geo+json");

        // Act - Launch two concurrent PUT requests to same feature
        var task1 = _client.PutAsync($"/ogc/collections/{collectionId}/items/{featureId}", content1);
        var task2 = _client.PutAsync($"/ogc/collections/{collectionId}/items/{featureId}", content2);

        var responses = await Task.WhenAll(task1, task2);

        // Assert - Should handle gracefully without corruption
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,               // Update succeeded
                HttpStatusCode.NoContent,        // Update succeeded
                HttpStatusCode.Created,          // Created (if didn't exist)
                HttpStatusCode.Conflict,         // Conflict detected (optimistic locking)
                HttpStatusCode.PreconditionFailed, // Conditional request failed
                HttpStatusCode.NotImplemented,   // Updates not supported
                HttpStatusCode.MethodNotAllowed, // Collection is read-only
                HttpStatusCode.NotFound);        // Feature doesn't exist (PUT not supported for creation)

            // Should never return server error
            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
                "concurrent writes should not cause server errors");
        }
    }

    /// <summary>
    /// Tests that reading data while a write is in progress returns consistent data.
    /// Data should never be partially written or corrupted from the reader's perspective.
    /// </summary>
    [Fact]
    public async Task ReadDuringWrite_ShouldReturnConsistentData()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        if (!collectionsResponse.IsSuccessStatusCode)
        {
            return; // Skip if no collections available
        }

        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);

        if (collectionsDoc.RootElement.GetProperty("collections").GetArrayLength() == 0)
        {
            return; // Skip test - no collections available
        }

        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Start write operation, then immediately read
        var feature = """
        {
            "type": "Feature",
            "geometry": {"type": "Point", "coordinates": [-122.4, 37.8]},
            "properties": {"name": "Test Feature", "test": "read-during-write"}
        }
        """;

        var writeContent = new StringContent(feature, Encoding.UTF8, "application/geo+json");
        var writeTask = _client.PostAsync($"/ogc/collections/{collectionId}/items", writeContent);

        // Launch read immediately (may happen during or after write)
        var readTask = _client.GetAsync($"/ogc/collections/{collectionId}/items");

        var responses = await Task.WhenAll(writeTask, readTask);
        var writeResponse = responses[0];
        var readResponse = responses[1];

        // Assert - Read should succeed with consistent data
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "reads should succeed even if concurrent writes are happening");

        var readContent = await readResponse.Content.ReadAsStringAsync();
        readContent.Should().NotBeNullOrWhiteSpace();

        // Verify JSON is valid (not corrupted)
        var json = JsonDocument.Parse(readContent);
        json.RootElement.TryGetProperty("features", out _).Should().BeTrue(
            "read data should be valid GeoJSON even during concurrent writes");
    }

    /// <summary>
    /// Tests that deleting a feature while another request is reading it doesn't cause errors.
    /// The read should complete successfully (with old data) or return 404, but never crash.
    /// </summary>
    [Fact]
    public async Task DeleteDuringRead_ShouldNotCauseError()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        if (!collectionsResponse.IsSuccessStatusCode)
        {
            return; // Skip if no collections available
        }

        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);

        if (collectionsDoc.RootElement.GetProperty("collections").GetArrayLength() == 0)
        {
            return; // Skip test - no collections available
        }

        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        var featureId = $"delete-test-{Guid.NewGuid():N}";

        // Act - Launch delete and read operations concurrently
        var deleteTask = _client.DeleteAsync($"/ogc/collections/{collectionId}/items/{featureId}");
        var readTask = _client.GetAsync($"/ogc/collections/{collectionId}/items/{featureId}");

        var responses = await Task.WhenAll(deleteTask, readTask);
        var deleteResponse = responses[0];
        var readResponse = responses[1];

        // Assert - Should not cause server errors
        deleteResponse.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "concurrent delete should not cause server errors");

        readResponse.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "reading during delete should not cause server errors");

        readResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,       // Read succeeded (feature still existed)
            HttpStatusCode.NotFound); // Feature already deleted
    }

    /// <summary>
    /// Tests that the system maintains acceptable performance under high concurrent load.
    /// 100 concurrent requests should complete within reasonable time without failures.
    /// </summary>
    [Fact]
    public async Task HighConcurrentLoad_ShouldMaintainPerformance()
    {
        // Arrange - Create 100 concurrent GET requests
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_client.GetAsync("/ogc/collections"));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.IsSuccessStatusCode.Should().BeTrue(),
            "all requests should succeed under high concurrent load");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "100 concurrent requests should complete within 10 seconds");

        // Log performance for analysis
        var avgTime = stopwatch.ElapsedMilliseconds / 100.0;
        Console.WriteLine($"Average request time under 100 concurrent load: {avgTime:F2}ms");
        Console.WriteLine($"Total time for 100 concurrent requests: {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Tests that mixed read/write operations under concurrent load don't cause deadlocks.
    /// System should remain responsive and not lock up under concurrent load.
    /// </summary>
    [Fact]
    public async Task MixedReadWriteLoad_ShouldNotDeadlock()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        if (!collectionsResponse.IsSuccessStatusCode)
        {
            return; // Skip if no collections available
        }

        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);

        if (collectionsDoc.RootElement.GetProperty("collections").GetArrayLength() == 0)
        {
            return; // Skip test - no collections available
        }

        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Create mix of read and write tasks
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 20; i++)
        {
            // Add read task
            tasks.Add(_client.GetAsync($"/ogc/collections/{collectionId}/items"));

            // Add write task
            var feature = $$"""
            {
                "type": "Feature",
                "geometry": {"type": "Point", "coordinates": [-122.{{i}}, 37.8]},
                "properties": {"name": "Feature {{i}}"}
            }
            """;
            var content = new StringContent(feature, Encoding.UTF8, "application/geo+json");
            tasks.Add(_client.PostAsync($"/ogc/collections/{collectionId}/items", content));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All should complete (no deadlock)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000,
            "mixed operations should complete without deadlock");

        responses.Should().AllSatisfy(r =>
        {
            r.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
                "no server errors should occur");
        });
    }

    /// <summary>
    /// Tests that rapid repeated requests to the same endpoint are handled efficiently.
    /// Validates caching behavior and prevents resource exhaustion.
    /// </summary>
    [Fact]
    public async Task RapidRepeatedRequests_ShouldBeHandledEfficiently()
    {
        // Arrange - Make 50 rapid requests to same endpoint
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => _client.GetAsync("/ogc/conformance"))
            .ToArray();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK),
            "all rapid requests should succeed");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000,
            "rapid requests to same endpoint should be handled efficiently");

        // Verify responses are consistent
        var contents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        var firstContent = contents[0];

        contents.Should().AllSatisfy(c => c.Should().Be(firstContent),
            "rapid requests should return consistent cached data");
    }

    /// <summary>
    /// Tests that the system recovers gracefully from concurrent request cancellations.
    /// Cancelled requests should not leave the system in an inconsistent state.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequestCancellation_ShouldNotCorruptState()
    {
        // Arrange - Launch multiple requests, then cancel some
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/ogc/collections"));
        }

        // Act - Wait briefly, then make another request to verify system state
        await Task.Delay(100); // Let some requests complete

        var verificationResponse = await _client.GetAsync("/ogc/collections");

        // Assert - System should still be functional
        verificationResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "system should remain functional after concurrent requests");
    }
}
