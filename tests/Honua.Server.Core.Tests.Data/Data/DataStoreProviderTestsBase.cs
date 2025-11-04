using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Abstract base class for data store provider integration tests.
/// Eliminates duplicate CRUD test patterns across PostgreSQL, MySQL, SQL Server, and SQLite providers.
/// </summary>
/// <remarks>
/// <para>
/// This base class consolidates common test methods for:
/// <list type="bullet">
/// <item>Querying features with geometry projection</item>
/// <item>Applying spatial filters (bounding boxes)</item>
/// <item>Creating, updating, and deleting features</item>
/// <item>Transaction handling</item>
/// </list>
/// </para>
/// <para>
/// Derived classes must implement database-specific setup via fixtures:
/// <list type="bullet">
/// <item>Container/connection initialization</item>
/// <item>Database seeding using <see cref="TestInfrastructure.TestDatabaseSeeder"/></item>
/// <item>Provider-specific connection strings</item>
/// <item>Cleanup and disposal</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// Implementing for a specific provider:
/// <code>
/// public class PostgresDataStoreProviderTests : DataStoreProviderTestsBase&lt;PostgresDataStoreProviderTests.Fixture&gt;
/// {
///     protected override IDataStoreProvider CreateProvider() => new PostgresDataStoreProvider();
///     protected override string ProviderName => "PostgreSQL";
///
///     public class Fixture : IAsyncLifetime
///     {
///         public (DataSourceDefinition, ServiceDefinition, LayerDefinition) Metadata { get; private set; }
///
///         public async Task InitializeAsync()
///         {
///             // Setup container, seed database
///         }
///     }
/// }
/// </code>
/// </example>
/// <typeparam name="TFixture">The fixture type providing database setup and metadata</typeparam>
public abstract class DataStoreProviderTestsBase<TFixture>
    where TFixture : class
{
    /// <summary>
    /// Creates a new instance of the data store provider for testing.
    /// </summary>
    /// <returns>A new provider instance.</returns>
    protected abstract IDataStoreProvider CreateProvider();

    /// <summary>
    /// Gets the metadata (data source, service, layer) for the current test.
    /// </summary>
    protected abstract (DataSourceDefinition DataSource, ServiceDefinition Service, LayerDefinition Layer) GetMetadata();

    /// <summary>
    /// Gets the provider name for logging and diagnostics.
    /// </summary>
    protected abstract string ProviderName { get; }

    /// <summary>
    /// Determines whether tests should be skipped (e.g., Docker not available).
    /// </summary>
    protected virtual bool ShouldSkip => false;

    /// <summary>
    /// Gets the reason for skipping tests, if applicable.
    /// </summary>
    protected virtual string? SkipReason => null;

    /// <summary>
    /// Tests that QueryAsync returns features with properly projected geometries.
    /// </summary>
    [Fact]
    public virtual async Task QueryAsync_ShouldReturnProjectedGeometry()
    {
        if (ShouldSkip)
        {
            Console.WriteLine($"[{ProviderName}] {SkipReason}");
            return;
        }

        var provider = CreateProvider();
        var (dataSource, service, layer) = GetMetadata();

        try
        {
            var query = new FeatureQuery(Crs: "EPSG:4326", Limit: 10);
            var results = new List<FeatureRecord>();
            await foreach (var record in provider.QueryAsync(dataSource, service, layer, query))
            {
                results.Add(record);
            }

            results.Should().HaveCountGreaterThanOrEqualTo(2, "seeded data should contain at least 2 features");

            var first = results.FirstOrDefault(r =>
                Convert.ToInt32(r.Attributes["road_id"], CultureInfo.InvariantCulture) == 1001);

            if (first != null)
            {
                var geometry = first.Attributes["geom"].Should().BeOfType<JsonObject>().Subject;
                var coordinates = geometry["coordinates"]!.AsArray();
                coordinates[0]!.GetValue<double>().Should().BeApproximately(-122.5, 1e-4);
                coordinates[1]!.GetValue<double>().Should().BeApproximately(45.5, 1e-4);
            }
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Tests that CountAsync applies bounding box filters correctly.
    /// </summary>
    [Fact]
    public virtual async Task CountAsync_ShouldApplyBoundingBox()
    {
        if (ShouldSkip)
        {
            Console.WriteLine($"[{ProviderName}] {SkipReason}");
            return;
        }

        var provider = CreateProvider();
        var (dataSource, service, layer) = GetMetadata();

        try
        {
            var bbox = new BoundingBox(-123, 45.4, -122.3, 45.8, Crs: "EPSG:4326");
            var query = new FeatureQuery(Bbox: bbox, Crs: "EPSG:4326");

            var results = new List<FeatureRecord>();
            await foreach (var record in provider.QueryAsync(dataSource, service, layer, query))
            {
                results.Add(record);
            }

            results.Should().HaveCount(1, "bounding box should filter to one feature");
            Convert.ToInt32(results[0].Attributes["road_id"], CultureInfo.InvariantCulture).Should().Be(1001);

            var count = await provider.CountAsync(dataSource, service, layer, query);
            count.Should().Be(1, "count should match filtered results");
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Tests the complete CRUD lifecycle: Create, Read (Get), Update, Delete.
    /// </summary>
    [Fact]
    public virtual async Task CreateUpdateAndDelete_ShouldRoundTripFeature()
    {
        if (ShouldSkip)
        {
            Console.WriteLine($"[{ProviderName}] {SkipReason}");
            return;
        }

        var provider = CreateProvider();
        var (dataSource, service, layer) = GetMetadata();

        try
        {
            // CREATE
            var attributes = new Dictionary<string, object?>
            {
                ["road_id"] = 2001,
                ["name"] = "Client Insert",
                ["status"] = "planned",
                ["observed_at"] = DateTimeOffset.UtcNow,
                ["geom"] = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[-45.0,10.0]}")
            };

            var record = new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
            var created = await provider.CreateAsync(dataSource, service, layer, record);
            Convert.ToInt32(created.Attributes["road_id"], CultureInfo.InvariantCulture).Should().Be(2001);

            // READ (Get)
            var fetched = await provider.GetAsync(dataSource, service, layer, "2001", new FeatureQuery(Crs: "EPSG:4326"));
            fetched.Should().NotBeNull("feature should exist after creation");
            var point = fetched!.Attributes["geom"].Should().BeOfType<JsonObject>().Subject;
            var coordinates = point["coordinates"]!.AsArray();
            coordinates[0]!.GetValue<double>().Should().BeApproximately(-45.0, 1e-4);
            coordinates[1]!.GetValue<double>().Should().BeApproximately(10.0, 1e-4);

            // UPDATE
            var updatedAttributes = new Dictionary<string, object?>
            {
                ["name"] = "Updated",
                ["status"] = "active"
            };

            var updateRecord = new FeatureRecord(new ReadOnlyDictionary<string, object?>(updatedAttributes));
            var updated = await provider.UpdateAsync(dataSource, service, layer, "2001", updateRecord);
            updated.Should().NotBeNull("update should return the updated feature");
            updated!.Attributes["name"].Should().Be("Updated");
            updated.Attributes["status"].Should().Be("active");

            // DELETE
            var deleted = await provider.DeleteAsync(dataSource, service, layer, "2001");
            deleted.Should().BeTrue("delete should succeed");

            var missing = await provider.GetAsync(dataSource, service, layer, "2001", new FeatureQuery(Crs: "EPSG:4326"));
            missing.Should().BeNull("feature should not exist after deletion");
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }
}
