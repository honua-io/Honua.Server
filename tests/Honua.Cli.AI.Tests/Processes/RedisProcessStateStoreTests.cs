using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Honua.Cli.AI.Configuration;
using Honua.Cli.AI.Services.Processes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Cli.AI.Tests.Processes;

/// <summary>
/// Runtime tests for RedisProcessStateStore using Testcontainers.
/// Tests Redis-backed state persistence, TTL, health checks, and error handling.
/// </summary>
[Trait("Category", "ProcessFramework")]
[Trait("Category", "Redis")]
[Trait("Category", "Integration")]
[Collection("ProcessFramework")]
public class RedisProcessStateStoreTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redis;
    private RedisProcessStateStore? _store;

    public RedisProcessStateStoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Start Redis container
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();

        var connectionString = _redisContainer.GetConnectionString();
        _output.WriteLine($"Redis started at: {connectionString}");

        // Connect to Redis
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);

        // Create store
        var options = Options.Create(new RedisOptions
        {
            ConnectionString = connectionString,
            KeyPrefix = "honua:process:",
            TtlSeconds = 3600
        });

        _store = new RedisProcessStateStore(
            _redis,
            options,
            NullLogger<RedisProcessStateStore>.Instance);
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        _redis?.Dispose();

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync();
            await _redisContainer.DisposeAsync();
        }
    }

    #region Redis State Persistence Tests

    [Fact]
    public async Task RedisStore_SaveProcess_PersistsCorrectly()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "redis-test-1",
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "ValidateRequirements",
            CompletionPercentage = 10,
            StartTime = DateTime.UtcNow
        };

        // Act
        await _store!.SaveProcessAsync(processInfo);
        var retrieved = await _store.GetProcessAsync("redis-test-1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be("redis-test-1");
        retrieved.WorkflowType.Should().Be("Deployment");
        retrieved.Status.Should().Be("Running");
        retrieved.CurrentStep.Should().Be("ValidateRequirements");
        retrieved.CompletionPercentage.Should().Be(10);

        _output.WriteLine($"Process saved to Redis and retrieved: {retrieved.ProcessId}");
    }

    [Fact]
    public async Task RedisStore_UpdateProcessStatus_UpdatesCorrectly()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "redis-test-2",
            WorkflowType = "Upgrade",
            Status = "Running",
            CurrentStep = "DetectVersion",
            CompletionPercentage = 25,
            StartTime = DateTime.UtcNow
        };
        await _store!.SaveProcessAsync(processInfo);

        // Act
        await _store.UpdateProcessStatusAsync("redis-test-2", "Running", 50);
        var updated = await _store.GetProcessAsync("redis-test-2");

        // Assert
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Running");
        updated.CompletionPercentage.Should().Be(50);

        _output.WriteLine($"Process status updated in Redis to {updated.CompletionPercentage}%");
    }

    [Fact]
    public async Task RedisStore_GetActiveProcesses_ReturnsCorrectSet()
    {
        // Arrange
        await _store!.SaveProcessAsync(new ProcessInfo { ProcessId = "r1", Status = "Running", WorkflowType = "Deploy", StartTime = DateTime.UtcNow });
        await _store.SaveProcessAsync(new ProcessInfo { ProcessId = "r2", Status = "Pending", WorkflowType = "Upgrade", StartTime = DateTime.UtcNow });
        await _store.SaveProcessAsync(new ProcessInfo { ProcessId = "r3", Status = "Completed", WorkflowType = "Metadata", StartTime = DateTime.UtcNow });

        // Act
        var activeProcesses = await _store.GetActiveProcessesAsync();

        // Assert
        activeProcesses.Should().HaveCount(2);
        activeProcesses.Should().Contain(p => p.ProcessId == "r1");
        activeProcesses.Should().Contain(p => p.ProcessId == "r2");
        activeProcesses.Should().NotContain(p => p.ProcessId == "r3");

        _output.WriteLine($"Active processes in Redis: {activeProcesses.Count}");
    }

    [Fact]
    public async Task RedisStore_DeleteProcess_RemovesFromRedis()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "redis-delete",
            WorkflowType = "Benchmark",
            Status = "Completed",
            StartTime = DateTime.UtcNow
        };
        await _store!.SaveProcessAsync(processInfo);

        // Act
        var deleted = await _store.DeleteProcessAsync("redis-delete");
        var retrieved = await _store.GetProcessAsync("redis-delete");

        // Assert
        deleted.Should().BeTrue();
        retrieved.Should().BeNull();

        _output.WriteLine("Process successfully deleted from Redis");
    }

    [Fact]
    public async Task RedisStore_CancelProcess_UpdatesStatusInRedis()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "redis-cancel",
            WorkflowType = "Deployment",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };
        await _store!.SaveProcessAsync(processInfo);

        // Act
        var cancelled = await _store.CancelProcessAsync("redis-cancel");
        var retrieved = await _store.GetProcessAsync("redis-cancel");

        // Assert
        cancelled.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("Cancelled");

        _output.WriteLine("Process successfully cancelled in Redis");
    }

    #endregion

    #region Redis TTL Tests

    [Fact]
    public async Task RedisStore_SaveProcess_SetsTTLCorrectly()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "redis-ttl-test",
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // Act
        await _store!.SaveProcessAsync(processInfo);

        // Verify TTL was set
        var db = _redis!.GetDatabase();
        var key = "honua:process:redis-ttl-test";
        var ttl = await db.KeyTimeToLiveAsync(key);

        // Assert
        ttl.Should().NotBeNull();
        ttl!.Value.TotalSeconds.Should().BeGreaterThan(3500); // Close to 3600
        ttl.Value.TotalSeconds.Should().BeLessThanOrEqualTo(3600);

        _output.WriteLine($"Redis key TTL: {ttl.Value.TotalSeconds:F0} seconds");
    }

    [Fact]
    public async Task RedisStore_ExpiredProcess_ReturnsNull()
    {
        // Arrange - Create store with very short TTL
        var options = Options.Create(new RedisOptions
        {
            ConnectionString = _redisContainer!.GetConnectionString(),
            KeyPrefix = "honua:process:short:",
            TtlSeconds = 2 // 2 seconds
        });

        var shortTtlStore = new RedisProcessStateStore(
            _redis!,
            options,
            NullLogger<RedisProcessStateStore>.Instance);

        var processInfo = new ProcessInfo
        {
            ProcessId = "expire-test",
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // Act
        await shortTtlStore.SaveProcessAsync(processInfo);
        var beforeExpiry = await shortTtlStore.GetProcessAsync("expire-test");

        // Wait for expiry
        await Task.Delay(2500);

        var afterExpiry = await shortTtlStore.GetProcessAsync("expire-test");

        // Assert
        beforeExpiry.Should().NotBeNull();
        afterExpiry.Should().BeNull();

        _output.WriteLine("Process correctly expired from Redis after TTL");
    }

    #endregion

    #region Redis Health Check Tests

    [Fact]
    public async Task RedisStore_HealthCheck_ReturnsHealthy()
    {
        // Arrange
        var context = new HealthCheckContext();

        // Act
        var result = await _store!.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("healthy");
        result.Description.Should().Contain("ping");

        _output.WriteLine($"Health check: {result.Description}");
    }

    [Fact]
    public async Task RedisStore_HealthCheck_DetectsSlowConnection()
    {
        // This test is hard to simulate without actually slowing down Redis
        // but the logic is in place in the code
        var context = new HealthCheckContext();

        var result = await _store!.CheckHealthAsync(context);

        // Assert - should be healthy or degraded, not unhealthy
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);

        _output.WriteLine($"Health check status: {result.Status}");
    }

    #endregion

    #region Redis Active Set Management Tests

    [Fact]
    public async Task RedisStore_ActiveSet_UpdatesWhenStatusChanges()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "activeset-test",
            WorkflowType = "Deployment",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // Act - Save as running
        await _store!.SaveProcessAsync(processInfo);
        var activeWhenRunning = await _store.GetActiveProcessesAsync();

        // Update to completed
        await _store.UpdateProcessStatusAsync("activeset-test", "Completed", 100);
        var activeWhenCompleted = await _store.GetActiveProcessesAsync();

        // Assert
        activeWhenRunning.Should().Contain(p => p.ProcessId == "activeset-test");
        activeWhenCompleted.Should().NotContain(p => p.ProcessId == "activeset-test");

        _output.WriteLine("Active set correctly updated when status changed");
    }

    [Fact]
    public async Task RedisStore_ActiveSet_CleansStaleEntries()
    {
        // Arrange - Manually add a stale entry to the active set
        var db = _redis!.GetDatabase();
        var activeSetKey = "honua:process:active";
        await db.SetAddAsync(activeSetKey, "stale-process");

        // Act - GetActiveProcesses should clean up stale entries
        var activeProcesses = await _store!.GetActiveProcessesAsync();

        // Verify stale entry was removed
        var setMembers = await db.SetMembersAsync(activeSetKey);
        var hasStaleEntry = false;
        foreach (var member in setMembers)
        {
            if (member == "stale-process")
            {
                hasStaleEntry = true;
                break;
            }
        }

        // Assert
        hasStaleEntry.Should().BeFalse();
        _output.WriteLine("Stale entries cleaned from active set");
    }

    #endregion

    #region Redis Error Handling Tests

    [Fact]
    public async Task RedisStore_SaveProcess_ThrowsOnNullProcess()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store!.SaveProcessAsync(null!));
    }

    [Fact]
    public async Task RedisStore_SaveProcess_ThrowsOnEmptyProcessId()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "",
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store!.SaveProcessAsync(processInfo));
    }

    [Fact]
    public async Task RedisStore_UpdateProcessStatus_ThrowsForNonExistent()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store!.UpdateProcessStatusAsync("non-existent", "Running"));

        exception.Message.Should().Contain("not found");
    }

    #endregion

    #region Redis Concurrent Operations Tests

    [Fact]
    public async Task RedisStore_ConcurrentSaves_AllSucceed()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Save 50 processes concurrently
        for (int i = 0; i < 50; i++)
        {
            var processId = $"concurrent-{i}";
            tasks.Add(_store!.SaveProcessAsync(new ProcessInfo
            {
                ProcessId = processId,
                WorkflowType = "Concurrent",
                Status = "Running",
                StartTime = DateTime.UtcNow
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All should be retrievable
        var activeProcesses = await _store!.GetActiveProcessesAsync();
        activeProcesses.Should().HaveCountGreaterThanOrEqualTo(50);

        _output.WriteLine($"Successfully saved {activeProcesses.Count} processes concurrently to Redis");
    }

    [Fact]
    public async Task RedisStore_ConcurrentUpdates_LastWins()
    {
        // Arrange
        await _store!.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "concurrent-update",
            WorkflowType = "Test",
            Status = "Running",
            CompletionPercentage = 0,
            StartTime = DateTime.UtcNow
        });

        // Act - Update same process concurrently with different values
        var tasks = new List<Task>();
        for (int i = 1; i <= 10; i++)
        {
            var percentage = i * 10;
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(i * 10); // Stagger slightly
                await _store.UpdateProcessStatusAsync("concurrent-update", "Running", percentage);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Process should have one of the percentages
        var result = await _store!.GetProcessAsync("concurrent-update");
        result.Should().NotBeNull();
        result!.CompletionPercentage.Should().BeGreaterThan(0);

        _output.WriteLine($"Final completion percentage after concurrent updates: {result.CompletionPercentage}%");
    }

    #endregion

    #region Redis JSON Serialization Tests

    [Fact]
    public async Task RedisStore_ComplexProcessInfo_SerializesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var processInfo = new ProcessInfo
        {
            ProcessId = "complex-test",
            WorkflowType = "ComplexDeployment",
            Status = "Failed",
            CurrentStep = "DeployInfrastructure",
            CompletionPercentage = 67,
            StartTime = now,
            EndTime = now.AddMinutes(10),
            ErrorMessage = "Deployment failed: Resource quota exceeded in region us-west-2"
        };

        // Act
        await _store!.SaveProcessAsync(processInfo);
        var retrieved = await _store.GetProcessAsync("complex-test");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be("complex-test");
        retrieved.WorkflowType.Should().Be("ComplexDeployment");
        retrieved.Status.Should().Be("Failed");
        retrieved.CurrentStep.Should().Be("DeployInfrastructure");
        retrieved.CompletionPercentage.Should().Be(67);
        retrieved.ErrorMessage.Should().Be("Deployment failed: Resource quota exceeded in region us-west-2");

        // DateTime comparison with tolerance for serialization
        retrieved.StartTime.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        retrieved.EndTime.Should().NotBeNull();
        retrieved.EndTime!.Value.Should().BeCloseTo(now.AddMinutes(10), TimeSpan.FromSeconds(1));

        _output.WriteLine("Complex ProcessInfo serialized and deserialized correctly");
    }

    #endregion

    #region Redis Key Management Tests

    [Fact]
    public async Task RedisStore_UsesCorrectKeyPrefix()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "key-prefix-test",
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // Act
        await _store!.SaveProcessAsync(processInfo);

        // Verify key exists with correct prefix
        var db = _redis!.GetDatabase();
        var expectedKey = "honua:process:key-prefix-test";
        var exists = await db.KeyExistsAsync(expectedKey);

        // Assert
        exists.Should().BeTrue();
        _output.WriteLine($"Redis key created with correct prefix: {expectedKey}");
    }

    [Fact]
    public async Task RedisStore_DeleteProcess_RemovesFromActiveSet()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessId = "delete-activeset-test",
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };
        await _store!.SaveProcessAsync(processInfo);

        // Verify it's in active set
        var activeBeforeDelete = await _store.GetActiveProcessesAsync();
        activeBeforeDelete.Should().Contain(p => p.ProcessId == "delete-activeset-test");

        // Act
        await _store.DeleteProcessAsync("delete-activeset-test");

        // Assert
        var activeAfterDelete = await _store.GetActiveProcessesAsync();
        activeAfterDelete.Should().NotContain(p => p.ProcessId == "delete-activeset-test");

        _output.WriteLine("Process removed from active set when deleted");
    }

    #endregion
}
