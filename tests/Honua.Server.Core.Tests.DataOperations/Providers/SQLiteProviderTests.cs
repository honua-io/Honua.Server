// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Providers;

/// <summary>
/// Integration tests for SQLite data provider with SpatiaLite extension.
/// Tests file-based database operations and spatial queries.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "SQLite")]
public class SQLiteProviderTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;
    private string _connectionString = string.Empty;
    private SqliteDataStoreProvider? _provider;

    public Task InitializeAsync()
    {
        // Create temporary database file
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";

        // Initialize provider
        _provider = new SqliteDataStoreProvider();

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
            Provider = "sqlite",
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
            Provider = "sqlite",
            ConnectionString = "Data Source=/invalid/path/to/database.db"
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
        _provider!.Provider.Should().Be("sqlite");
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
    }

    [Fact]
    public async Task BeginTransaction_CreatesValidTransaction()
    {
        // Arrange
        var dataSource = new DataSourceDefinition
        {
            Id = "test",
            Provider = "sqlite",
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
            Provider = "sqlite",
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
            Provider = "sqlite",
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
            Provider = "sqlite",
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
            Provider = "sqlite",
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

    [Fact]
    public void Dispose_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new SqliteDataStoreProvider();

        // Act
        provider.Dispose();

        // Assert - no exception means success
    }

    [Fact]
    public async Task DisposeAsync_AfterUse_DoesNotThrow()
    {
        // Arrange
        var provider = new SqliteDataStoreProvider();

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
            Provider = "sqlite",
            ConnectionString = "Data Source=:memory:"
        };

        // Act & Assert - should not throw
        await _provider!.TestConnectivityAsync(dataSource, CancellationToken.None);
    }
}
