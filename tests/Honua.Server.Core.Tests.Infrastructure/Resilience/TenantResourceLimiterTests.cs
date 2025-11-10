// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Resilience;

/// <summary>
/// Tests for TenantResourceLimiter to verify per-tenant resource isolation.
/// </summary>
public class TenantResourceLimiterTests : IDisposable
{
    private readonly TenantResourceLimiter _limiter;
    private readonly BulkheadOptions _options;

    public TenantResourceLimiterTests()
    {
        _options = new BulkheadOptions
        {
            PerTenantEnabled = true,
            PerTenantMaxParallelization = 3,
            PerTenantMaxQueuedActions = 0 // No queueing for fast failure
        };

        _limiter = new TenantResourceLimiter(
            Options.Create(_options),
            NullLogger<TenantResourceLimiter>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WithinLimit_Succeeds()
    {
        // Arrange
        const string tenantId = "tenant1";
        var executionCount = 0;

        // Act
        var result = await _limiter.ExecuteAsync(tenantId, async () =>
        {
            executionCount++;
            await Task.Delay(10);
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsLimit_ThrowsException()
    {
        // Arrange
        const string tenantId = "tenant1";
        var tasks = new List<Task<int>>();
        var tcs = new TaskCompletionSource<bool>();

        // Start max parallelization tasks that will block
        for (int i = 0; i < _options.PerTenantMaxParallelization; i++)
        {
            tasks.Add(_limiter.ExecuteAsync(tenantId, async () =>
            {
                await tcs.Task; // Block until we signal completion
                return i;
            }));
        }

        // Wait for tasks to start
        await Task.Delay(100);

        // Act - try to exceed the limit
        var exception = await Assert.ThrowsAsync<TenantResourceLimitExceededException>(async () =>
        {
            await _limiter.ExecuteAsync(tenantId, async () =>
            {
                return 999;
            });
        });

        // Assert
        Assert.Equal(tenantId, exception.TenantId);
        Assert.Equal(_options.PerTenantMaxParallelization, exception.MaxParallelization);

        // Cleanup - release blocked tasks
        tcs.SetResult(true);
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentTenants_IndependentLimits()
    {
        // Arrange
        const string tenant1 = "tenant1";
        const string tenant2 = "tenant2";
        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();

        // Fill up tenant1's slots
        var tenant1Tasks = new List<Task<int>>();
        for (int i = 0; i < _options.PerTenantMaxParallelization; i++)
        {
            tenant1Tasks.Add(_limiter.ExecuteAsync(tenant1, async () =>
            {
                await tcs1.Task;
                return i;
            }));
        }

        // Wait for tenant1 tasks to start
        await Task.Delay(100);

        // Act - tenant2 should still be able to execute
        var tenant2Result = await _limiter.ExecuteAsync(tenant2, async () =>
        {
            return 42;
        });

        // Assert
        Assert.Equal(42, tenant2Result);

        // Cleanup
        tcs1.SetResult(true);
        await Task.WhenAll(tenant1Tasks);
    }

    [Fact]
    public async Task ExecuteAsync_ReleasesSlotOnException()
    {
        // Arrange
        const string tenantId = "tenant1";

        // Act - first operation throws an exception
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _limiter.ExecuteAsync(tenantId, async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Test exception");
            });
        });

        // Assert - slot should be released, second operation should succeed
        var result = await _limiter.ExecuteAsync(tenantId, async () =>
        {
            return 123;
        });

        Assert.Equal(123, result);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentRequestsSameTenant_RespectLimit()
    {
        // Arrange
        const string tenantId = "tenant1";
        const int totalRequests = 10;
        var successCount = 0;
        var failureCount = 0;
        var tasks = new List<Task>();

        // Act - fire off many concurrent requests
        for (int i = 0; i < totalRequests; i++)
        {
            var taskNumber = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _limiter.ExecuteAsync(tenantId, async () =>
                    {
                        await Task.Delay(50); // Simulate work
                        Interlocked.Increment(ref successCount);
                    });
                }
                catch (TenantResourceLimitExceededException)
                {
                    Interlocked.Increment(ref failureCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - some should succeed, some should fail
        Assert.True(successCount > 0, "At least some requests should succeed");
        Assert.True(failureCount > 0, "Some requests should be rejected");
        Assert.Equal(totalRequests, successCount + failureCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_AllowsUnlimitedExecution()
    {
        // Arrange
        var disabledOptions = new BulkheadOptions
        {
            PerTenantEnabled = false,
            PerTenantMaxParallelization = 1 // Very low limit, but disabled
        };

        var disabledLimiter = new TenantResourceLimiter(
            Options.Create(disabledOptions),
            NullLogger<TenantResourceLimiter>.Instance);

        const string tenantId = "tenant1";
        const int concurrentRequests = 10;

        // Act - execute many concurrent requests with limiter disabled
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => disabledLimiter.ExecuteAsync(tenantId, async () =>
            {
                await Task.Delay(10);
                return i;
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - all should succeed
        Assert.Equal(concurrentRequests, results.Length);
        Assert.Equal(Enumerable.Range(0, concurrentRequests).OrderBy(x => x), results.OrderBy(x => x));
    }

    [Fact]
    public void GetAvailableSlots_NewTenant_ReturnsNull()
    {
        // Arrange
        const string tenantId = "new-tenant";

        // Act
        var availableSlots = _limiter.GetAvailableSlots(tenantId);

        // Assert
        Assert.Null(availableSlots);
    }

    [Fact]
    public async Task GetAvailableSlots_AfterExecution_ReturnsCorrectCount()
    {
        // Arrange
        const string tenantId = "tenant1";
        var tcs = new TaskCompletionSource<bool>();

        // Start one operation that blocks
        var task = _limiter.ExecuteAsync(tenantId, async () =>
        {
            await tcs.Task;
            return 1;
        });

        await Task.Delay(50); // Wait for task to start

        // Act
        var availableSlots = _limiter.GetAvailableSlots(tenantId);

        // Assert
        Assert.Equal(_options.PerTenantMaxParallelization - 1, availableSlots);

        // Cleanup
        tcs.SetResult(true);
        await task;
    }

    [Fact]
    public void CleanupInactiveTenants_RemovesInactiveSemaphores()
    {
        // Arrange
        var tenant1 = "tenant1";
        var tenant2 = "tenant2";
        var tenant3 = "tenant3";

        // Execute operations to create semaphores
        _limiter.ExecuteAsync(tenant1, async () => { await Task.CompletedTask; }).Wait();
        _limiter.ExecuteAsync(tenant2, async () => { await Task.CompletedTask; }).Wait();
        _limiter.ExecuteAsync(tenant3, async () => { await Task.CompletedTask; }).Wait();

        // Verify all semaphores exist
        Assert.NotNull(_limiter.GetAvailableSlots(tenant1));
        Assert.NotNull(_limiter.GetAvailableSlots(tenant2));
        Assert.NotNull(_limiter.GetAvailableSlots(tenant3));

        // Act - cleanup with only tenant1 and tenant3 active
        var activeTenants = new HashSet<string> { tenant1, tenant3 };
        var removed = _limiter.CleanupInactiveTenants(activeTenants);

        // Assert
        Assert.Equal(1, removed); // tenant2 should be removed
        Assert.NotNull(_limiter.GetAvailableSlots(tenant1));
        Assert.Null(_limiter.GetAvailableSlots(tenant2)); // Removed
        Assert.NotNull(_limiter.GetAvailableSlots(tenant3));
    }

    [Fact]
    public async Task ExecuteAsync_VoidOperation_WorksCorrectly()
    {
        // Arrange
        const string tenantId = "tenant1";
        var executed = false;

        // Act
        await _limiter.ExecuteAsync(tenantId, async () =>
        {
            await Task.Delay(10);
            executed = true;
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        const string tenantId = "tenant1";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _limiter.ExecuteAsync(tenantId, async () =>
            {
                await Task.Delay(1000);
                return 1;
            }, cts.Token);
        });
    }

    public void Dispose()
    {
        _limiter?.Dispose();
    }
}
