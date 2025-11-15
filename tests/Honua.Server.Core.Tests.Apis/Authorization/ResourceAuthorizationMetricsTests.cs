// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Honua.Server.Core.Authorization;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Authorization;

public class ResourceAuthorizationMetricsTests
{
    [Fact]
    public void Constructor_WithNullMeterFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ResourceAuthorizationMetrics(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("meterFactory");
    }

    [Fact]
    public void RecordAuthorizationCheck_WithSuccessAndNotCached_RecordsMetrics()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);
        using var collector = new MetricCollector<long>(meterFactory, "Honua.Server.Authorization", "honua.authorization.checks");

        // Act
        metrics.RecordAuthorizationCheck("collection", "read", true, 10.5, false);

        // Assert
        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().NotBeEmpty();
    }

    [Fact]
    public void RecordAuthorizationCheck_WithDenial_RecordsDenialMetric()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);
        using var denialCollector = new MetricCollector<long>(meterFactory, "Honua.Server.Authorization", "honua.authorization.denials");

        // Act
        metrics.RecordAuthorizationCheck("layer", "write", false, 5.0, false);

        // Assert
        var measurements = denialCollector.GetMeasurementSnapshot();
        measurements.Should().NotBeEmpty();
    }

    [Fact]
    public void RecordAuthorizationCheck_WithCacheHit_RecordsCacheHitMetric()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);
        using var cacheHitCollector = new MetricCollector<long>(meterFactory, "Honua.Server.Authorization", "honua.authorization.cache.hits");

        // Act
        metrics.RecordAuthorizationCheck("collection", "read", true, 1.0, true);

        // Assert
        var measurements = cacheHitCollector.GetMeasurementSnapshot();
        measurements.Should().NotBeEmpty();
    }

    [Fact]
    public void RecordAuthorizationCheck_WithCacheMiss_RecordsCacheMissMetric()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);
        using var cacheMissCollector = new MetricCollector<long>(meterFactory, "Honua.Server.Authorization", "honua.authorization.cache.misses");

        // Act
        metrics.RecordAuthorizationCheck("layer", "read", true, 8.0, false);

        // Assert
        var measurements = cacheMissCollector.GetMeasurementSnapshot();
        measurements.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateCacheSize_WithPositiveDelta_UpdatesMetric()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);
        using var cacheSizeCollector = new MetricCollector<int>(meterFactory, "Honua.Server.Authorization", "honua.authorization.cache.size");

        // Act
        metrics.UpdateCacheSize(10, 0);

        // Assert
        var measurements = cacheSizeCollector.GetMeasurementSnapshot();
        measurements.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateCacheSize_WithZeroDelta_DoesNotUpdateMetric()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);
        using var cacheSizeCollector = new MetricCollector<int>(meterFactory, "Honua.Server.Authorization", "honua.authorization.cache.size");

        // Act
        metrics.UpdateCacheSize(10, 10);

        // Assert
        var measurements = cacheSizeCollector.GetMeasurementSnapshot();
        measurements.Should().BeEmpty();
    }

    [Fact]
    public void RecordCacheInvalidation_DoesNotThrow()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new ResourceAuthorizationMetrics(meterFactory);

        // Act
        var act = () => metrics.RecordCacheInvalidation("collection", 5);

        // Assert
        act.Should().NotThrow();
    }
}
