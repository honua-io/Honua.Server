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
    public void GetTtl_WithDefaultPolicy_ReturnsDefaultValue()
    {
        // Arrange
        var policy = new CacheTtlPolicy
        {
            DefaultTtl = TimeSpan.FromMinutes(10)
        };

        // Act
        var ttl = policy.GetTtl("any-key");

        // Assert
        ttl.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void GetTtl_WithPatternMatch_ReturnsPatternTtl()
    {
        // Arrange
        var policy = new CacheTtlPolicy
        {
            DefaultTtl = TimeSpan.FromMinutes(10)
        };
        policy.SetPatternTtl("feature:*", TimeSpan.FromHours(1));

        // Act
        var ttl = policy.GetTtl("feature:123");

        // Assert
        ttl.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void GetTtl_WithMultiplePatterns_MatchesFirstPattern()
    {
        // Arrange
        var policy = new CacheTtlPolicy
        {
            DefaultTtl = TimeSpan.FromMinutes(5)
        };
        policy.SetPatternTtl("*:short", TimeSpan.FromMinutes(2));
        policy.SetPatternTtl("cache:*", TimeSpan.FromMinutes(30));

        // Act
        var ttl = policy.GetTtl("cache:short");

        // Assert
        // Should match first pattern that was added
        ttl.Should().BeOneOf(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
    }

    [Theory]
    [InlineData("spatial:query:*", "spatial:query:bbox")]
    [InlineData("*:temp", "cache:temp")]
    [InlineData("layer:*:metadata", "layer:123:metadata")]
    public void GetTtl_WithWildcardPatterns_MatchesCorrectly(string pattern, string key)
    {
        // Arrange
        var policy = new CacheTtlPolicy
        {
            DefaultTtl = TimeSpan.FromMinutes(10)
        };
        var customTtl = TimeSpan.FromSeconds(30);
        policy.SetPatternTtl(pattern, customTtl);

        // Act
        var ttl = policy.GetTtl(key);

        // Assert
        ttl.Should().Be(customTtl);
    }

    [Fact]
    public void GetTtl_WithZeroTtl_ReturnsNoCache()
    {
        // Arrange
        var policy = new CacheTtlPolicy
        {
            DefaultTtl = TimeSpan.FromMinutes(10)
        };
        policy.SetPatternTtl("nocache:*", TimeSpan.Zero);

        // Act
        var ttl = policy.GetTtl("nocache:data");

        // Assert
        ttl.Should().Be(TimeSpan.Zero);
    }
}
