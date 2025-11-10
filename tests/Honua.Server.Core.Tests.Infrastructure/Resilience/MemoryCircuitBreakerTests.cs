// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Resilience;

/// <summary>
/// Tests for MemoryCircuitBreaker to verify memory threshold protection.
/// </summary>
public class MemoryCircuitBreakerTests
{
    [Fact]
    public async Task ExecuteAsync_BelowThreshold_Succeeds()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            // Set threshold well above current memory
            MemoryThresholdBytes = currentMemory + 1_073_741_824 // Current + 1GB
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        var executionCount = 0;

        // Act
        var result = await breaker.ExecuteAsync(async () =>
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
    public async Task ExecuteAsync_ExceedsThreshold_ThrowsException()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            // Set threshold below current memory to trigger the exception
            MemoryThresholdBytes = currentMemory / 2 // Half of current memory
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<MemoryThresholdExceededException>(async () =>
        {
            await breaker.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                return 42;
            });
        });

        Assert.True(exception.CurrentMemoryBytes > exception.ThresholdBytes);
        Assert.Equal(exception.CurrentMemoryBytes / 1_048_576, exception.CurrentMemoryMB);
        Assert.Equal(exception.ThresholdBytes / 1_048_576, exception.ThresholdMB);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_AllowsExecution()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = false,
            // Set threshold below current memory, but breaker is disabled
            MemoryThresholdBytes = currentMemory / 2
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act - should succeed even though memory exceeds threshold
        var result = await breaker.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            return 123;
        });

        // Assert
        Assert.Equal(123, result);
    }

    [Fact]
    public async Task ExecuteAsync_VoidOperation_WorksCorrectly()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = currentMemory + 1_073_741_824
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        var executed = false;

        // Act
        await breaker.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            executed = true;
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesInnerException()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = currentMemory + 1_073_741_824
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await breaker.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Test exception");
            });
        });
    }

    [Fact]
    public void GetCurrentMemoryUsage_ReturnsPositiveValue()
    {
        // Arrange
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = 1_073_741_824
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act
        var memoryUsage = breaker.GetCurrentMemoryUsage();

        // Assert
        Assert.True(memoryUsage > 0, "Memory usage should be positive");
    }

    [Fact]
    public void GetMemoryUsagePercentage_ReturnsValidPercentage()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = currentMemory * 2 // Double current memory
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act
        var percentage = breaker.GetMemoryUsagePercentage();

        // Assert
        Assert.True(percentage > 0, "Percentage should be positive");
        Assert.True(percentage < 100, "Percentage should be less than 100 since threshold is double current memory");
    }

    [Fact]
    public void IsApproachingThreshold_BelowWarning_ReturnsFalse()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = currentMemory * 10 // 10x current memory
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act
        var isApproaching = breaker.IsApproachingThreshold(warningPercentage: 90.0);

        // Assert
        Assert.False(isApproaching, "Should not be approaching threshold when at 10% usage");
    }

    [Fact]
    public void IsApproachingThreshold_AboveWarning_ReturnsTrue()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            // Set threshold just slightly above current memory
            MemoryThresholdBytes = (long)(currentMemory * 1.05) // 5% above current
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act - use low warning percentage to ensure we're above it
        var isApproaching = breaker.IsApproachingThreshold(warningPercentage: 90.0);

        // Assert
        Assert.True(isApproaching, "Should be approaching threshold when at 95% usage");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleOperations_ConsistentBehavior()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = currentMemory + 1_073_741_824
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act - execute multiple operations
        var results = new int[5];
        for (int i = 0; i < 5; i++)
        {
            var capturedI = i;
            results[i] = await breaker.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                return capturedI;
            });
        }

        // Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i, results[i]);
        }
    }

    [Theory]
    [InlineData(50.0)]
    [InlineData(75.0)]
    [InlineData(90.0)]
    [InlineData(95.0)]
    public void IsApproachingThreshold_VariousWarningLevels_WorksCorrectly(double warningPercentage)
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new BulkheadOptions
        {
            MemoryCircuitBreakerEnabled = true,
            MemoryThresholdBytes = currentMemory * 2
        };

        var breaker = new MemoryCircuitBreaker(
            Options.Create(options),
            NullLogger<MemoryCircuitBreaker>.Instance);

        // Act
        var isApproaching = breaker.IsApproachingThreshold(warningPercentage);

        // Assert - at 50% usage, should only exceed thresholds below 50%
        var actualPercentage = breaker.GetMemoryUsagePercentage();
        Assert.Equal(actualPercentage >= warningPercentage, isApproaching);
    }
}
