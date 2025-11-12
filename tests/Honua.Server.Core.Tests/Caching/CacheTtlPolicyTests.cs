// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.Caching;
using Xunit;

namespace Honua.Server.Core.Tests.Caching;

public class CacheTtlPolicyTests
{
    [Fact]
    public void ToTimeSpan_VeryShort_ReturnsOneMinute()
    {
        // Arrange
        var policy = CacheTtlPolicy.VeryShort;

        // Act
        var ttl = policy.ToTimeSpan();

        // Assert
        ttl.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ToTimeSpan_Short_ReturnsFiveMinutes()
    {
        // Arrange
        var policy = CacheTtlPolicy.Short;

        // Act
        var ttl = policy.ToTimeSpan();

        // Assert
        ttl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ToTimeSpan_Medium_ReturnsOneHour()
    {
        // Arrange
        var policy = CacheTtlPolicy.Medium;

        // Act
        var ttl = policy.ToTimeSpan();

        // Assert
        ttl.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void ToTimeSpan_Long_ReturnsTwentyFourHours()
    {
        // Arrange
        var policy = CacheTtlPolicy.Long;

        // Act
        var ttl = policy.ToTimeSpan();

        // Assert
        ttl.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void ToTimeSpan_VeryLong_ReturnsSevenDays()
    {
        // Arrange
        var policy = CacheTtlPolicy.VeryLong;

        // Act
        var ttl = policy.ToTimeSpan();

        // Assert
        ttl.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void ToTimeSpan_Permanent_ReturnsThirtyDays()
    {
        // Arrange
        var policy = CacheTtlPolicy.Permanent;

        // Act
        var ttl = policy.ToTimeSpan();

        // Assert
        ttl.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void ToDistributedCacheOptions_CreatesOptionsWithCorrectTtl()
    {
        // Arrange
        var policy = CacheTtlPolicy.Medium;

        // Act
        var options = policy.ToDistributedCacheOptions();

        // Assert
        options.Should().NotBeNull();
        options.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void ToMemoryCacheOptions_CreatesOptionsWithCorrectTtl()
    {
        // Arrange
        var policy = CacheTtlPolicy.Short;

        // Act
        var options = policy.ToMemoryCacheOptions();

        // Assert
        options.Should().NotBeNull();
        options.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ToMemoryCacheOptionsWithSliding_CreatesOptionsWithSlidingExpiration()
    {
        // Arrange
        var policy = CacheTtlPolicy.Medium;

        // Act
        var options = policy.ToMemoryCacheOptionsWithSliding();

        // Assert
        options.Should().NotBeNull();
        options.SlidingExpiration.Should().Be(TimeSpan.FromHours(1));
    }
}
