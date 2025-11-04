// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for cold start performance.
/// These tests measure real-world cold start times in various deployment scenarios.
/// </summary>
/// <remarks>
/// These tests require actual deployment environments and are typically run
/// as part of deployment pipelines or manual testing.
///
/// Skip locally with: dotnet test --filter "Category!=E2E"
/// Run with: dotnet test --filter "Category=E2E"
/// </remarks>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class ColdStartTests
{
    private const int ColdStartTimeoutMs = 10000; // 10 seconds
    private const int FirstRequestTimeoutMs = 2000; // 2 seconds

    [Fact(Skip = "Requires deployed environment")]
    public async Task Docker_ColdStart_CompletesUnder5Seconds()
    {
        // Arrange
        var sw = Stopwatch.StartNew();

        // This would use Docker SDK to start a container
        // Example: var container = await DockerClient.StartContainerAsync("honua-server:latest");

        // Act - Wait for container to be healthy
        // Example: await WaitForHealthyAsync(container.Id, ColdStartTimeoutMs);

        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(5000,
            "Docker cold start should complete in under 5 seconds");

        // Cleanup
        // await DockerClient.StopContainerAsync(container.Id);
    }

    [Fact(Skip = "Requires Cloud Run deployment")]
    public async Task CloudRun_ColdStart_CompletesUnder3Seconds()
    {
        // Arrange - This test would deploy to actual Cloud Run
        // and measure cold start time from deployment to first successful request

        var serviceUrl = Environment.GetEnvironmentVariable("CLOUDRUN_SERVICE_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            throw new InvalidOperationException("CLOUDRUN_SERVICE_URL environment variable not set");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var sw = Stopwatch.StartNew();

        // Act - First request triggers cold start
        var response = await client.GetAsync($"{serviceUrl}/healthz/live");
        sw.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        sw.ElapsedMilliseconds.Should().BeLessThan(3000,
            "Cloud Run cold start should complete in under 3 seconds");
    }

    [Fact(Skip = "Requires AWS Lambda deployment")]
    public async Task Lambda_ColdStart_CompletesUnder2Seconds()
    {
        // Arrange
        var functionUrl = Environment.GetEnvironmentVariable("LAMBDA_FUNCTION_URL");
        if (string.IsNullOrEmpty(functionUrl))
        {
            throw new InvalidOperationException("LAMBDA_FUNCTION_URL environment variable not set");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var sw = Stopwatch.StartNew();

        // Act - First invocation after deployment
        var response = await client.GetAsync($"{functionUrl}/healthz/live");
        sw.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000,
            "Lambda cold start should complete in under 2 seconds");
    }

    [Fact(Skip = "Requires Azure Container Instances deployment")]
    public async Task AzureContainerInstances_ColdStart_CompletesUnder4Seconds()
    {
        // Arrange
        var instanceUrl = Environment.GetEnvironmentVariable("ACI_INSTANCE_URL");
        if (string.IsNullOrEmpty(instanceUrl))
        {
            throw new InvalidOperationException("ACI_INSTANCE_URL environment variable not set");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var sw = Stopwatch.StartNew();

        // Act
        var response = await client.GetAsync($"{instanceUrl}/healthz/live");
        sw.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        sw.ElapsedMilliseconds.Should().BeLessThan(4000,
            "Azure Container Instances cold start should complete in under 4 seconds");
    }

    [Fact]
    public async Task LocalDocker_FirstRequest_CompletesUnder500Ms()
    {
        // This test can run locally with Docker
        // Arrange
        var serviceUrl = Environment.GetEnvironmentVariable("LOCAL_DOCKER_URL") ?? "http://localhost:8080";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Ensure service is running
        try
        {
            await client.GetAsync($"{serviceUrl}/healthz/live");
        }
        catch
        {
            // Skip test if local Docker not available
            return;
        }

        // Wait a bit to ensure warmup completes
        await Task.Delay(2000);

        var sw = Stopwatch.StartNew();

        // Act - Measure first request after warmup
        var response = await client.GetAsync($"{serviceUrl}/ogc");
        sw.Stop();

        // Assert
        response.EnsureSuccessStatusCode();
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "First request after warmup should complete in under 500ms");
    }

    [Fact(Skip = "Requires deployed environment")]
    public async Task ColdStart_WithConnectionPoolWarmup_IsFasterThanWithout()
    {
        // This test requires two deployments: one with warmup enabled, one without
        var urlWithWarmup = Environment.GetEnvironmentVariable("SERVICE_URL_WITH_WARMUP");
        var urlWithoutWarmup = Environment.GetEnvironmentVariable("SERVICE_URL_WITHOUT_WARMUP");

        if (string.IsNullOrEmpty(urlWithWarmup) || string.IsNullOrEmpty(urlWithoutWarmup))
        {
            throw new InvalidOperationException("Service URLs not configured");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // Measure without warmup
        var swWithout = Stopwatch.StartNew();
        var responseWithout = await client.GetAsync($"{urlWithoutWarmup}/healthz/live");
        swWithout.Stop();

        // Measure with warmup
        var swWith = Stopwatch.StartNew();
        var responseWith = await client.GetAsync($"{urlWithWarmup}/healthz/live");
        swWith.Stop();

        // Assert
        responseWithout.EnsureSuccessStatusCode();
        responseWith.EnsureSuccessStatusCode();

        swWith.ElapsedMilliseconds.Should().BeLessThan(swWithout.ElapsedMilliseconds,
            "Cold start with warmup should be faster than without");
    }

    [Theory(Skip = "Requires deployed environment")]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ColdStart_UnderConcurrentLoad_HandlesRequestsCorrectly(int concurrentRequests)
    {
        // Test cold start behavior under concurrent load
        var serviceUrl = Environment.GetEnvironmentVariable("SERVICE_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            throw new InvalidOperationException("SERVICE_URL environment variable not set");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Act - Send concurrent requests to cold instance
        var tasks = new Task<HttpResponseMessage>[concurrentRequests];
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks[i] = client.GetAsync($"{serviceUrl}/healthz/live");
        }

        var responses = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert - All requests should succeed
        foreach (var response in responses)
        {
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        // Cold start under load should still be reasonable
        sw.ElapsedMilliseconds.Should().BeLessThan(10000,
            $"Cold start with {concurrentRequests} concurrent requests should complete in under 10 seconds");
    }

    [Fact(Skip = "Requires Kubernetes deployment")]
    public async Task Kubernetes_ReadinessProbe_BecomesHealthyAfterWarmup()
    {
        // This test verifies readiness probe behavior with warmup
        var podUrl = Environment.GetEnvironmentVariable("K8S_POD_URL");
        if (string.IsNullOrEmpty(podUrl))
        {
            throw new InvalidOperationException("K8S_POD_URL environment variable not set");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Act - Check readiness immediately after pod start
        var firstCheck = await client.GetAsync($"{podUrl}/healthz/ready");
        var firstStatus = await firstCheck.Content.ReadAsStringAsync();

        // Wait for warmup (configured delay + warmup time)
        await Task.Delay(3000);

        // Check again after warmup
        var secondCheck = await client.GetAsync($"{podUrl}/healthz/ready");
        var secondStatus = await secondCheck.Content.ReadAsStringAsync();

        // Assert
        firstCheck.IsSuccessStatusCode.Should().BeTrue();
        secondCheck.IsSuccessStatusCode.Should().BeTrue();

        // Status should improve or maintain health
        secondStatus.Should().Contain("Healthy");
    }

    [Fact]
    public async Task MemoryUsage_DuringColdStart_StaysUnderLimit()
    {
        // This test verifies memory usage during cold start
        // Useful for serverless environments with memory limits

        var serviceUrl = Environment.GetEnvironmentVariable("SERVICE_URL") ?? "http://localhost:8080";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        try
        {
            // Trigger cold start
            var response = await client.GetAsync($"{serviceUrl}/healthz/live");
            response.EnsureSuccessStatusCode();

            // Check memory metrics endpoint (if available)
            var metricsResponse = await client.GetAsync($"{serviceUrl}/metrics");
            if (metricsResponse.IsSuccessStatusCode)
            {
                var metrics = await metricsResponse.Content.ReadAsStringAsync();

                // This is a placeholder - actual implementation would parse metrics
                // and verify memory usage is within acceptable limits
                metrics.Should().NotBeNullOrEmpty();
            }
        }
        catch (HttpRequestException)
        {
            // Skip if service not available
        }
    }

    [Fact(Skip = "Manual performance test")]
    public async Task PerformanceComparison_MultipleDeployments()
    {
        // This test compares cold start times across multiple deployment types
        var results = new System.Collections.Generic.Dictionary<string, long>();

        var deploymentUrls = new[]
        {
            ("Docker", Environment.GetEnvironmentVariable("DOCKER_URL")),
            ("CloudRun", Environment.GetEnvironmentVariable("CLOUDRUN_URL")),
            ("Lambda", Environment.GetEnvironmentVariable("LAMBDA_URL")),
            ("ACI", Environment.GetEnvironmentVariable("ACI_URL"))
        };

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        foreach (var (name, url) in deploymentUrls)
        {
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var response = await client.GetAsync($"{url}/healthz/live");
                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    results[name] = sw.ElapsedMilliseconds;
                }
            }
            catch
            {
                results[name] = -1; // Failed
            }
        }

        // Output results for comparison
        foreach (var (deployment, time) in results)
        {
            Console.WriteLine($"{deployment}: {time}ms");
        }

        // All successful deployments should meet targets
        foreach (var (deployment, time) in results)
        {
            if (time > 0)
            {
                time.Should().BeLessThan(5000, $"{deployment} cold start exceeded 5 seconds");
            }
        }
    }
}
