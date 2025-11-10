// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
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
/// Tests for BulkheadPolicyProvider to verify bulkhead policies work correctly.
/// </summary>
public class BulkheadPolicyProviderTests
{
    [Fact]
    public async Task ExecuteDatabaseOperationAsync_WithinLimit_Succeeds()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = true,
            DatabaseMaxParallelization = 3,
            DatabaseMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        // Act
        var result = await provider.ExecuteDatabaseOperationAsync(async () =>
        {
            await Task.Delay(10);
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteDatabaseOperationAsync_ExceedsLimit_ThrowsBulkheadRejectedException()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = true,
            DatabaseMaxParallelization = 2,
            DatabaseMaxQueuedActions = 0 // No queueing
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        var tcs = new TaskCompletionSource<bool>();
        var tasks = new List<Task<int>>();

        // Fill up all slots
        for (int i = 0; i < options.DatabaseMaxParallelization; i++)
        {
            tasks.Add(provider.ExecuteDatabaseOperationAsync(async () =>
            {
                await tcs.Task;
                return i;
            }));
        }

        await Task.Delay(100); // Wait for tasks to start

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BulkheadRejectedException>(async () =>
        {
            await provider.ExecuteDatabaseOperationAsync(async () =>
            {
                return 999;
            });
        });

        Assert.Equal("Database", exception.BulkheadName);

        // Cleanup
        tcs.SetResult(true);
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ExecuteExternalApiOperationAsync_WithinLimit_Succeeds()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            ExternalApiEnabled = true,
            ExternalApiMaxParallelization = 3,
            ExternalApiMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        // Act
        var result = await provider.ExecuteExternalApiOperationAsync(async () =>
        {
            await Task.Delay(10);
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task ExecuteExternalApiOperationAsync_ExceedsLimit_ThrowsBulkheadRejectedException()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            ExternalApiEnabled = true,
            ExternalApiMaxParallelization = 2,
            ExternalApiMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        var tcs = new TaskCompletionSource<bool>();
        var tasks = new List<Task<string>>();

        // Fill up all slots
        for (int i = 0; i < options.ExternalApiMaxParallelization; i++)
        {
            tasks.Add(provider.ExecuteExternalApiOperationAsync(async () =>
            {
                await tcs.Task;
                return $"result-{i}";
            }));
        }

        await Task.Delay(100);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BulkheadRejectedException>(async () =>
        {
            await provider.ExecuteExternalApiOperationAsync(async () =>
            {
                return "overflow";
            });
        });

        Assert.Equal("ExternalApi", exception.BulkheadName);

        // Cleanup
        tcs.SetResult(true);
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ExecuteDatabaseOperationAsync_VoidOperation_Succeeds()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = true,
            DatabaseMaxParallelization = 3,
            DatabaseMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        var executed = false;

        // Act
        await provider.ExecuteDatabaseOperationAsync(async () =>
        {
            await Task.Delay(10);
            executed = true;
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteExternalApiOperationAsync_VoidOperation_Succeeds()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            ExternalApiEnabled = true,
            ExternalApiMaxParallelization = 3,
            ExternalApiMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        var executed = false;

        // Act
        await provider.ExecuteExternalApiOperationAsync(async () =>
        {
            await Task.Delay(10);
            executed = true;
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteDatabaseOperationAsync_WhenDisabled_AllowsUnlimitedExecution()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = false,
            DatabaseMaxParallelization = 1, // Very low limit, but disabled
            DatabaseMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        const int concurrentRequests = 10;

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => provider.ExecuteDatabaseOperationAsync(async () =>
            {
                await Task.Delay(10);
                return i;
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentRequests, results.Length);
        Assert.Equal(Enumerable.Range(0, concurrentRequests).OrderBy(x => x), results.OrderBy(x => x));
    }

    [Fact]
    public async Task ExecuteExternalApiOperationAsync_WhenDisabled_AllowsUnlimitedExecution()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            ExternalApiEnabled = false,
            ExternalApiMaxParallelization = 1,
            ExternalApiMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        const int concurrentRequests = 10;

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => provider.ExecuteExternalApiOperationAsync(async () =>
            {
                await Task.Delay(10);
                return i;
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentRequests, results.Length);
    }

    [Fact]
    public async Task BulkheadPolicies_AreIndependent()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = true,
            DatabaseMaxParallelization = 2,
            DatabaseMaxQueuedActions = 0,
            ExternalApiEnabled = true,
            ExternalApiMaxParallelization = 2,
            ExternalApiMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        var dbTcs = new TaskCompletionSource<bool>();
        var apiTcs = new TaskCompletionSource<bool>();

        // Fill up database bulkhead
        var dbTasks = new List<Task<int>>();
        for (int i = 0; i < options.DatabaseMaxParallelization; i++)
        {
            dbTasks.Add(provider.ExecuteDatabaseOperationAsync(async () =>
            {
                await dbTcs.Task;
                return i;
            }));
        }

        await Task.Delay(100);

        // Act - External API should still work even though DB bulkhead is full
        var apiResult = await provider.ExecuteExternalApiOperationAsync(async () =>
        {
            return "api-success";
        });

        // Assert
        Assert.Equal("api-success", apiResult);

        // Cleanup
        dbTcs.SetResult(true);
        await Task.WhenAll(dbTasks);
    }

    [Fact]
    public async Task ExecuteDatabaseOperationAsync_PropagatesInnerException()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = true,
            DatabaseMaxParallelization = 3,
            DatabaseMaxQueuedActions = 0
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await provider.ExecuteDatabaseOperationAsync(async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Test error");
            });
        });
    }

    [Fact]
    public async Task ExecuteDatabaseOperationAsync_WithQueueing_HandlesOverflow()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            DatabaseEnabled = true,
            DatabaseMaxParallelization = 2,
            DatabaseMaxQueuedActions = 3 // Allow some queueing
        };

        var provider = new BulkheadPolicyProvider(
            Options.Create(options),
            NullLogger<BulkheadPolicyProvider>.Instance);

        var tcs = new TaskCompletionSource<bool>();
        var tasks = new List<Task<int>>();

        // Start operations that exceed parallelization but fit in queue
        for (int i = 0; i < options.DatabaseMaxParallelization + options.DatabaseMaxQueuedActions; i++)
        {
            var capturedI = i;
            tasks.Add(Task.Run(async () =>
            {
                return await provider.ExecuteDatabaseOperationAsync(async () =>
                {
                    await tcs.Task;
                    return capturedI;
                });
            }));
        }

        await Task.Delay(100);

        // Act - this should exceed both parallelization and queue
        await Assert.ThrowsAsync<BulkheadRejectedException>(async () =>
        {
            await provider.ExecuteDatabaseOperationAsync(async () =>
            {
                return 999;
            });
        });

        // Cleanup
        tcs.SetResult(true);
        await Task.WhenAll(tasks);

        // Assert - all queued tasks should complete successfully
        Assert.All(tasks, task => Assert.True(task.IsCompletedSuccessfully));
    }
}
