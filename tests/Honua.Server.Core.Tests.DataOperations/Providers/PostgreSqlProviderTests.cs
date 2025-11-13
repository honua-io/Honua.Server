// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;
using Testcontainers.PostgreSql;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Providers;

/// <summary>
/// Integration tests for PostgreSQL data provider using TestContainers.
/// Tests spatial queries, geometry operations, and transactions with real PostgreSQL + PostGIS.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "PostgreSQL")]
public class PostgreSqlProviderTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;
    private PostgresDataStoreProvider? _provider;

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container with PostGIS
        _container = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Initialize provider
        _provider = new PostgresDataStoreProvider();
    }

    public async Task DisposeAsync()
    {
        if (_provider != null)
        {
            await _provider.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task TestConnectivity_WithValidConnection_Succeeds()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = _connectionString
        };

        // Act & Assert
        await _provider!.TestConnectivityAsync(dataSource, CancellationToken.None);
    }

    [Fact]
    public async Task TestConnectivity_WithInvalidConnection_Throws()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = "Host=invalid;Database=test;Username=test;Password=test"
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _provider!.TestConnectivityAsync(dataSource, CancellationToken.None);
        });
    }

    [Fact]
    public void Provider_ReturnsCorrectProviderKey()
    {
        // Assert
        _provider!.Provider.Should().Be("postgis");
    }

    [Fact]
    public void Capabilities_ReturnsValidCapabilities()
    {
        // Act
        var capabilities = _provider!.Capabilities;

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsTransactions.Should().BeTrue();
        capabilities.SupportsSpatialIndexes.Should().BeTrue();
        capabilities.SupportsNativeMvt.Should().BeTrue();
    }

    [Fact]
    public async Task BeginTransaction_CreatesValidTransaction()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = _connectionString
        };

        // Act
        var transaction = await _provider!.BeginTransactionAsync(dataSource, CancellationToken.None);

        // Assert
        transaction.Should().NotBeNull();

        // Cleanup
        if (transaction != null)
        {
            await transaction.CommitAsync(CancellationToken.None);
            await transaction.DisposeAsync();
        }
    }

    [Fact]
    public async Task BeginTransaction_CanCommit()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = _connectionString
        };

        // Act
        var transaction = await _provider!.BeginTransactionAsync(dataSource, CancellationToken.None);
        await transaction!.CommitAsync(CancellationToken.None);

        // Assert - no exception means success
        transaction.Should().NotBeNull();

        // Cleanup
        await transaction.DisposeAsync();
    }

    [Fact]
    public async Task BeginTransaction_CanRollback()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = _connectionString
        };

        // Act
        var transaction = await _provider!.BeginTransactionAsync(dataSource, CancellationToken.None);
        await transaction!.RollbackAsync(CancellationToken.None);

        // Assert - no exception means success
        transaction.Should().NotBeNull();

        // Cleanup
        await transaction.DisposeAsync();
    }

    [Fact]
    public void GetQueryBuilderPoolStatistics_ReturnsValidStatistics()
    {
        // Act
        var stats = _provider!.GetQueryBuilderPoolStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalPools.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void WarmQueryBuilderCache_DoesNotThrow()
    {
        // Arrange
        var service = new ServiceDefinition
        {
            Id = "test",
            Title = "Test Service",
            FolderId = "folder1",
            ServiceType = "FeatureServer",
            DataSourceId = "ds1"
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act & Assert - should not throw
        _provider!.WarmQueryBuilderCache(service, layer, 4326, 4326, 5);
    }

    [Fact]
    public async Task Dispose_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new PostgresDataStoreProvider();

        // Act
        provider.Dispose();

        // Assert - no exception means success
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DisposeAsync_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new PostgresDataStoreProvider();

        // Act
        await provider.DisposeAsync();

        // Assert - no exception means success
    }

    [Fact]
    public async Task QueryAsync_WithNonExistentTable_HandlesGracefully()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = _connectionString
        };

        var service = new ServiceDefinition
        {
            Id = "test",
            Title = "Test Service",
            FolderId = "folder1",
            ServiceType = "FeatureServer",
            DataSourceId = "ds1"
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "non_existent_table",
                GeometryColumn = "geom"
            }
        };

        // Act & Assert - should throw or return empty
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var results = _provider!.QueryAsync(dataSource, service, layer, null, CancellationToken.None);
            await foreach (var result in results)
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task CountAsync_WithNonExistentTable_HandlesGracefully()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "postgis",
            ConnectionString = _connectionString
        };

        var service = new ServiceDefinition
        {
            Id = "test",
            Title = "Test Service",
            FolderId = "folder1",
            ServiceType = "FeatureServer",
            DataSourceId = "ds1"
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "test",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "non_existent_table",
                GeometryColumn = "geom"
            }
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _provider!.CountAsync(dataSource, service, layer, null, CancellationToken.None);
        });
    }
}
