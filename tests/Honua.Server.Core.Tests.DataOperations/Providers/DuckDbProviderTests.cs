// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.DuckDB;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Providers;

/// <summary>
/// Integration tests for DuckDB data provider.
/// Tests analytical queries and spatial operations with DuckDB.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "DuckDB")]
public class DuckDbProviderTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;
    private string _connectionString = string.Empty;
    private DuckDBDataStoreProvider? _provider;

    public Task InitializeAsync()
    {
        // Create temporary database file
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.duckdb");
        _connectionString = $"Data Source={_dbPath}";

        // Initialize provider
        _provider = new DuckDBDataStoreProvider();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Cleanup
        _provider?.Dispose();

        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task TestConnectivity_WithValidConnection_Succeeds()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "duckdb",
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
            Provider = "duckdb",
            ConnectionString = "Data Source=/invalid/path/to/database.duckdb"
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
        _provider!.Provider.Should().Be("duckdb");
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
            Provider = "duckdb",
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
            Provider = "duckdb",
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
            Provider = "duckdb",
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
            Provider = "duckdb",
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
            Provider = "duckdb",
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
    public void Dispose_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new DuckDBDataStoreProvider();

        // Act
        provider.Dispose();

        // Assert - no exception means success
    }

    [Fact]
    public async Task DisposeAsync_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new DuckDBDataStoreProvider();

        // Act
        await provider.DisposeAsync();

        // Assert - no exception means success
    }

    [Fact]
    public async Task InMemoryDatabase_CanBeCreatedAndUsed()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "duckdb",
            ConnectionString = "Data Source=:memory:"
        };

        // Act & Assert - should not throw
        await _provider!.TestConnectivityAsync(dataSource, CancellationToken.None);
    }
}
