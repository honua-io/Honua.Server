// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Enterprise.ETL.Resilience;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL.Resilience;

public class RetryPolicyTests
{
    [Fact]
    public void DefaultPolicy_HasCorrectDefaults()
    {
        var policy = RetryPolicy.Default;

        Assert.Equal(3, policy.MaxAttempts);
        Assert.Equal(BackoffStrategy.Exponential, policy.BackoffStrategy);
        Assert.Equal(5, policy.InitialDelaySeconds);
        Assert.Equal(300, policy.MaxDelaySeconds);
        Assert.True(policy.UseJitter);
    }

    [Fact]
    public void GetDelay_ExponentialBackoff_CalculatesCorrectly()
    {
        var policy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelaySeconds = 2,
            UseJitter = false // Disable jitter for predictable testing
        };

        var delay1 = policy.GetDelay(1);
        var delay2 = policy.GetDelay(2);
        var delay3 = policy.GetDelay(3);

        Assert.Equal(TimeSpan.FromSeconds(2), delay1);   // 2 * 2^0 = 2
        Assert.Equal(TimeSpan.FromSeconds(4), delay2);   // 2 * 2^1 = 4
        Assert.Equal(TimeSpan.FromSeconds(8), delay3);   // 2 * 2^2 = 8
    }

    [Fact]
    public void GetDelay_LinearBackoff_CalculatesCorrectly()
    {
        var policy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Linear,
            InitialDelaySeconds = 5,
            UseJitter = false
        };

        var delay1 = policy.GetDelay(1);
        var delay2 = policy.GetDelay(2);
        var delay3 = policy.GetDelay(3);

        Assert.Equal(TimeSpan.FromSeconds(5), delay1);   // 5 * 1
        Assert.Equal(TimeSpan.FromSeconds(10), delay2);  // 5 * 2
        Assert.Equal(TimeSpan.FromSeconds(15), delay3);  // 5 * 3
    }

    [Fact]
    public void GetDelay_RespectsMaxDelay()
    {
        var policy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelaySeconds = 100,
            MaxDelaySeconds = 200,
            UseJitter = false
        };

        var delay3 = policy.GetDelay(3); // Would be 400 without cap

        Assert.Equal(TimeSpan.FromSeconds(200), delay3);
    }

    [Fact]
    public void GetDelay_WithJitter_AddsRandomness()
    {
        var policy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelaySeconds = 10,
            UseJitter = true,
            JitterFactor = 0.5
        };

        var delay1a = policy.GetDelay(1);
        var delay1b = policy.GetDelay(1);

        // Delays should be within jitter range (5-15 seconds)
        Assert.InRange(delay1a.TotalSeconds, 5, 15);
        Assert.InRange(delay1b.TotalSeconds, 5, 15);
    }

    [Fact]
    public void ShouldRetry_TransientError_ReturnsTrue()
    {
        var policy = RetryPolicy.Default;

        var shouldRetry = policy.ShouldRetry(ErrorCategory.Transient, 1);

        Assert.True(shouldRetry);
    }

    [Fact]
    public void ShouldRetry_DataError_ReturnsFalse()
    {
        var policy = RetryPolicy.Default;

        var shouldRetry = policy.ShouldRetry(ErrorCategory.Data, 1);

        Assert.False(shouldRetry); // Data errors are not in default retryable list
    }

    [Fact]
    public void ShouldRetry_ExceedsMaxAttempts_ReturnsFalse()
    {
        var policy = new RetryPolicy { MaxAttempts = 3 };

        var shouldRetry = policy.ShouldRetry(ErrorCategory.Transient, 3);

        Assert.False(shouldRetry);
    }

    [Fact]
    public void ForTransientErrors_HasCorrectConfiguration()
    {
        var policy = RetryPolicy.ForTransientErrors;

        Assert.Equal(5, policy.MaxAttempts);
        Assert.Equal(2, policy.InitialDelaySeconds);
        Assert.Single(policy.RetryableErrors);
        Assert.Contains(ErrorCategory.Transient, policy.RetryableErrors);
    }

    [Fact]
    public void NoRetry_HasZeroAttempts()
    {
        var policy = RetryPolicy.NoRetry;

        Assert.Equal(0, policy.MaxAttempts);
        Assert.False(policy.ShouldRetry(ErrorCategory.Transient, 0));
    }
}
