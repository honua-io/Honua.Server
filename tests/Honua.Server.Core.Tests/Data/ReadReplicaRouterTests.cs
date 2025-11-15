// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Data;

public sealed class ReadReplicaRouterTests
{
    private readonly ReadReplicaOptions _defaultOptions;
    private readonly ReadReplicaMetrics _metrics;

    public ReadReplicaRouterTests()
    {
        _defaultOptions = new ReadReplicaOptions
        {
            EnableReadReplicaRouting = true,
            FallbackToPrimary = true,
            MaxConsecutiveFailures = 3,
            UnhealthyRetryIntervalSeconds = 60
        };

        _metrics = new ReadReplicaMetrics(NullLogger<ReadReplicaMetrics>.Instance);
    }

    [Fact]
    public async Task RouteAsync_WriteOperation_AlwaysReturnsPrimary()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1 });

        // Act
        var result = await router.RouteAsync(primary, isReadOnly: false, CancellationToken.None);

        // Assert
        Assert.Same(primary, result);
    }

    [Fact]
    public async Task RouteAsync_ReadOperation_RoutingDisabled_ReturnsPrimary()
    {
        // Arrange
        var options = new ReadReplicaOptions
        {
            EnableReadReplicaRouting = false
        };
        var router = CreateRouter(options);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1 });

        // Act
        var result = await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None);

        // Assert
        Assert.Same(primary, result);
    }

    [Fact]
    public async Task RouteAsync_ReadOperation_NoReplicas_ReturnsPrimary()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);

        // Act
        var result = await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None);

        // Assert
        Assert.Same(primary, result);
    }

    [Fact]
    public async Task RouteAsync_ReadOperation_SingleReplica_ReturnsReplica()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1 });

        // Act
        var result = await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None);

        // Assert
        Assert.Same(replica1, result);
    }

    [Fact]
    public async Task RouteAsync_ReadOperation_MultipleReplicas_RoundRobin()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);
        var replica2 = CreateDataSource("replica2", readOnly: true);
        var replica3 = CreateDataSource("replica3", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1, replica2, replica3 });

        // Act - make 6 calls to see round-robin behavior
        var results = new List<DataSourceDefinition>();
        for (int i = 0; i < 6; i++)
        {
            results.Add(await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None));
        }

        // Assert - should cycle through replicas
        Assert.Contains(replica1, results);
        Assert.Contains(replica2, results);
        Assert.Contains(replica3, results);

        // Should see each replica at least once in 6 calls
        var replica1Count = results.Count(r => r.Id == replica1.Id);
        var replica2Count = results.Count(r => r.Id == replica2.Id);
        var replica3Count = results.Count(r => r.Id == replica3.Id);

        Assert.Equal(2, replica1Count); // 2 times each in 6 calls (6/3=2)
        Assert.Equal(2, replica2Count);
        Assert.Equal(2, replica3Count);
    }

    [Fact]
    public async Task RouteAsync_UnhealthyReplica_SkipsToNext()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);
        var replica2 = CreateDataSource("replica2", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1, replica2 });

        // Mark replica1 as unhealthy (3 consecutive failures)
        router.ReportHealth(replica1.Id, false);
        router.ReportHealth(replica1.Id, false);
        router.ReportHealth(replica1.Id, false);

        // Act
        var result = await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None);

        // Assert - should skip unhealthy replica1 and use replica2
        Assert.Same(replica2, result);
    }

    [Fact]
    public async Task RouteAsync_AllReplicasUnhealthy_FallbackEnabled_ReturnsPrimary()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);
        var replica2 = CreateDataSource("replica2", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1, replica2 });

        // Mark all replicas as unhealthy
        for (int i = 0; i < 3; i++)
        {
            router.ReportHealth(replica1.Id, false);
            router.ReportHealth(replica2.Id, false);
        }

        // Act
        var result = await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None);

        // Assert - should fall back to primary
        Assert.Same(primary, result);
    }

    [Fact]
    public async Task RouteAsync_AllReplicasUnhealthy_FallbackDisabled_ThrowsException()
    {
        // Arrange
        var options = new ReadReplicaOptions
        {
            EnableReadReplicaRouting = true,
            FallbackToPrimary = false,
            MaxConsecutiveFailures = 3
        };
        var router = CreateRouter(options);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1 });

        // Mark replica as unhealthy
        for (int i = 0; i < 3; i++)
        {
            router.ReportHealth(replica1.Id, false);
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RouteAsync(primary, isReadOnly: true, CancellationToken.None));
    }

    [Fact]
    public void ReportHealth_SuccessAfterFailures_ResetsHealth()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);
        var replica1 = CreateDataSource("replica1", readOnly: true);

        router.RegisterReplicas(primary.Id, new[] { replica1 });

        // Mark as unhealthy
        router.ReportHealth(replica1.Id, false);
        router.ReportHealth(replica1.Id, false);

        // Act - report success
        router.ReportHealth(replica1.Id, true);

        // Make a call - should use the now-healthy replica
        var result = router.RouteAsync(primary, isReadOnly: true, CancellationToken.None).Result;

        // Assert
        Assert.Same(replica1, result);
    }

    [Fact]
    public async Task RegisterReplicas_EmptyList_DoesNotThrow()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var primary = CreateDataSource("primary", readOnly: false);

        // Act
        router.RegisterReplicas(primary.Id, Array.Empty<DataSourceDefinition>());

        // Should not throw and should return primary
        var result = await router.RouteAsync(primary, isReadOnly: true, CancellationToken.None);

        // Assert
        Assert.Same(primary, result);
    }

    [Fact]
    public void RegisterReplicas_NullPrimaryId_ThrowsArgumentException()
    {
        // Arrange
        var router = CreateRouter(_defaultOptions);
        var replica = CreateDataSource("replica", readOnly: true);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            router.RegisterReplicas(null!, new[] { replica }));
    }

    private ReadReplicaRouter CreateRouter(ReadReplicaOptions options)
    {
        return new ReadReplicaRouter(
            Options.Create(options),
            NullLogger<ReadReplicaRouter>.Instance,
            _metrics);
    }

    private DataSourceDefinition CreateDataSource(string id, bool readOnly)
    {
        return new DataSourceDefinition
        {
            Id = id,
            Provider = "postgresql",
            ConnectionString = $"Host=localhost;Database={id}",
            ReadOnly = readOnly
        };
    }
}
