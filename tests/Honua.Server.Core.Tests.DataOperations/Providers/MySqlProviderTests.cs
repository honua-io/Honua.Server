// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Metadata;
using Testcontainers.MySql;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Providers;

/// <summary>
/// Integration tests for MySQL data provider using TestContainers.
/// Tests spatial queries with MySQL spatial extensions.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "MySQL")]
public class MySqlProviderTests : IAsyncLifetime
{
    private MySqlContainer? _container;
    private string _connectionString = string.Empty;
    private MySqlDataStoreProvider? _provider;

    public async Task InitializeAsync()
    {
        // Start MySQL container
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("test_db")
            .WithUsername("root")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Initialize provider
        _provider = new MySqlDataStoreProvider();
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
            Provider = "mysql",
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
            Provider = "mysql",
            ConnectionString = "Server=invalid;Database=test;User=test;Password=test"
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
        _provider!.Provider.Should().Be("mysql");
    }

    [Fact]
    public void Capabilities_ReturnsValidCapabilities()
    {
        // Act
        var capabilities = _provider!.Capabilities;

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsTransactions.Should().BeTrue();
        capabilities.SupportsSpatialFilters.Should().BeTrue();
    }

    [Fact]
    public async Task BeginTransaction_CreatesValidTransaction()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "mysql",
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
            Provider = "mysql",
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
            Provider = "mysql",
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
    public async Task QueryAsync_WithNonExistentTable_HandlesGracefully()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "mysql",
            ConnectionString = _connectionString
        };

        var service = new ServiceDefinition
        {
            Id = "test",
            Name = "Test Service"
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            Name = "Test Layer",
            GeometryType = "Point",
            GeometryColumn = "geom",
            TableName = "non_existent_table"
        };

        // Act & Assert
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
            Provider = "mysql",
            ConnectionString = _connectionString
        };

        var service = new ServiceDefinition
        {
            Id = "test",
            Name = "Test Service"
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            Name = "Test Layer",
            GeometryType = "Point",
            GeometryColumn = "geom",
            TableName = "non_existent_table"
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _provider!.CountAsync(dataSource, service, layer, null, CancellationToken.None);
        });
    }

    [Fact]
    public async Task Dispose_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new MySqlDataStoreProvider();

        // Act
        provider.Dispose();

        // Assert - no exception means success
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DisposeAsync_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new MySqlDataStoreProvider();

        // Act
        await provider.DisposeAsync();

        // Assert - no exception means success
    }
}
