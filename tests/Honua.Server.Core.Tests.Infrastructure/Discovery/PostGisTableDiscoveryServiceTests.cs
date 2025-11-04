// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Discovery;

/// <summary>
/// Tests for PostGIS table discovery service.
/// Note: These are unit tests that verify the service logic.
/// Integration tests against a real PostGIS database are in separate files.
/// </summary>
public sealed class PostGisTableDiscoveryServiceTests
{
    [Fact]
    public void Constructor_WithNullMetadataRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PostGisTableDiscoveryService(
                null!,
                schemaDiscovery,
                options,
                NullLogger<PostGisTableDiscoveryService>.Instance));
    }

    [Fact]
    public void Constructor_WithNullSchemaDiscovery_ThrowsArgumentNullException()
    {
        // Arrange
        var metadataRegistry = new TestMetadataRegistry();
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PostGisTableDiscoveryService(
                metadataRegistry,
                null!,
                options,
                NullLogger<PostGisTableDiscoveryService>.Instance));
    }

    [Fact]
    public async Task DiscoverTablesAsync_WhenDisabled_ReturnsEmptyList()
    {
        // Arrange
        var metadataRegistry = new TestMetadataRegistry();
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = false });

        var service = new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            options,
            NullLogger<PostGisTableDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverTablesAsync("test-datasource");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverTablesAsync_WithInvalidDataSource_ReturnsEmptyList()
    {
        // Arrange
        var metadataRegistry = new TestMetadataRegistry();
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        var service = new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            options,
            NullLogger<PostGisTableDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverTablesAsync("non-existent-datasource");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void DiscoveredTable_QualifiedName_CombinesSchemaAndTable()
    {
        // Arrange
        var table = new DiscoveredTable
        {
            Schema = "public",
            TableName = "roads",
            GeometryColumn = "geom",
            SRID = 4326,
            GeometryType = "LineString",
            PrimaryKeyColumn = "gid",
            Columns = new System.Collections.Generic.Dictionary<string, ColumnInfo>()
        };

        // Act
        var qualifiedName = table.QualifiedName;

        // Assert
        Assert.Equal("public.roads", qualifiedName);
    }

    [Fact]
    public void Envelope_ToArray_ReturnsCorrectOrder()
    {
        // Arrange
        var envelope = new Envelope
        {
            MinX = -122.5,
            MinY = 37.7,
            MaxX = -122.4,
            MaxY = 37.8
        };

        // Act
        var array = envelope.ToArray();

        // Assert
        Assert.Equal(4, array.Length);
        Assert.Equal(-122.5, array[0]);
        Assert.Equal(37.7, array[1]);
        Assert.Equal(-122.4, array[2]);
        Assert.Equal(37.8, array[3]);
    }

    [Theory]
    [InlineData("temp_data", "temp_*", true)]
    [InlineData("temp_", "temp_*", true)]
    [InlineData("staging_foo", "staging_*", true)]
    [InlineData("_private", "_*", true)]
    [InlineData("roads", "temp_*", false)]
    [InlineData("public_data", "_*", false)]
    [InlineData("users_table", "user_*", false)] // users_table doesn't match user_*
    [InlineData("user_data", "user_*", true)]
    public void ExclusionPattern_MatchesCorrectly(string tableName, string pattern, bool shouldMatch)
    {
        // Test the pattern matching logic using a similar approach to the implementation
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var matches = System.Text.RegularExpressions.Regex.IsMatch(tableName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        Assert.Equal(shouldMatch, matches);
    }

    [Fact]
    public void AutoDiscoveryOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new AutoDiscoveryOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.True(options.DiscoverPostGISTablesAsODataCollections);
        Assert.True(options.DiscoverPostGISTablesAsOgcCollections);
        Assert.Equal(4326, options.DefaultSRID);
        Assert.Equal(100, options.MaxTables);
        Assert.False(options.RequireSpatialIndex);
        Assert.Equal(TimeSpan.FromMinutes(5), options.CacheDuration);
        Assert.True(options.UseFriendlyNames);
        Assert.True(options.GenerateOpenApiDocs);
        Assert.False(options.ComputeExtentOnDiscovery);
        Assert.False(options.IncludeNonSpatialTables);
        Assert.True(options.BackgroundRefresh);
    }

    [Fact]
    public void ColumnInfo_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var column = new ColumnInfo
        {
            Name = "population",
            DataType = "int32",
            StorageType = "integer",
            IsNullable = true,
            IsPrimaryKey = false,
            Alias = "Population"
        };

        // Assert
        Assert.Equal("population", column.Name);
        Assert.Equal("int32", column.DataType);
        Assert.Equal("integer", column.StorageType);
        Assert.True(column.IsNullable);
        Assert.False(column.IsPrimaryKey);
        Assert.Equal("Population", column.Alias);
    }

    [Fact]
    public async Task DiscoverTableAsync_WhenDisabled_ReturnsNull()
    {
        // Arrange
        var metadataRegistry = new TestMetadataRegistry();
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = false });

        var service = new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            options,
            NullLogger<PostGisTableDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverTableAsync("test-datasource", "public.cities");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverTableAsync_WithInvalidDataSource_ReturnsNull()
    {
        // Arrange
        var metadataRegistry = new TestMetadataRegistry();
        var schemaDiscovery = new PostgresSchemaDiscoveryService(NullLogger<PostgresSchemaDiscoveryService>.Instance);
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        var service = new PostGisTableDiscoveryService(
            metadataRegistry,
            schemaDiscovery,
            options,
            NullLogger<PostGisTableDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverTableAsync("non-existent", "public.cities");

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Test implementation of IMetadataRegistry for unit testing.
    /// </summary>
    private sealed class TestMetadataRegistry : IMetadataRegistry
    {
        private readonly MetadataSnapshot _snapshot;

        public TestMetadataRegistry()
        {
            _snapshot = new MetadataSnapshot(
                new CatalogDefinition { Id = "test" },
                Array.Empty<FolderDefinition>(),
                Array.Empty<DataSourceDefinition>(),
                Array.Empty<ServiceDefinition>(),
                Array.Empty<LayerDefinition>());
        }

        public MetadataSnapshot Snapshot => _snapshot;
        public bool IsInitialized => true;

        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }

        public ValueTask<MetadataSnapshot> GetSnapshotAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_snapshot);
        }

        public Task EnsureInitializedAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReloadAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Update(MetadataSnapshot snapshot)
        {
        }

        public Task UpdateAsync(MetadataSnapshot snapshot, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Microsoft.Extensions.Primitives.IChangeToken GetChangeToken()
        {
            return new Microsoft.Extensions.Primitives.CancellationChangeToken(System.Threading.CancellationToken.None);
        }
    }
}
