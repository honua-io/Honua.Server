// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using NetTopologySuite.Geometries;
using Xunit;
using FluentAssertions;

namespace HonuaField.Tests.Data.Repositories;

/// <summary>
/// Unit tests for FeatureRepository
/// Tests CRUD operations and spatial queries
/// </summary>
public class FeatureRepositoryTests : IAsyncLifetime
{
	private HonuaFieldDatabase? _database;
	private FeatureRepository? _featureRepository;
	private ChangeRepository? _changeRepository;
	private readonly string _testDbPath;

	public FeatureRepositoryTests()
	{
		// Create unique database path for each test run
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
	}

	public async Task InitializeAsync()
	{
		// Initialize database and repositories
		_database = new HonuaFieldDatabase(_testDbPath);
		await _database.InitializeAsync();

		_changeRepository = new ChangeRepository(_database);
		_featureRepository = new FeatureRepository(_database, _changeRepository);
	}

	public async Task DisposeAsync()
	{
		// Clean up database
		if (_database != null)
		{
			await _database.CloseAsync();
		}

		if (File.Exists(_testDbPath))
		{
			File.Delete(_testDbPath);
		}
	}

	#region CRUD Tests

	[Fact]
	public async Task InsertAsync_ShouldInsertFeature_AndReturnId()
	{
		// Arrange
		var feature = CreateTestFeature("collection1", -122.4194, 37.7749);

		// Act
		var id = await _featureRepository!.InsertAsync(feature);

		// Assert
		id.Should().NotBeNullOrEmpty();
		feature.Id.Should().Be(id);
		feature.SyncStatus.Should().Be(SyncStatus.Pending.ToString());
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnFeature_WhenExists()
	{
		// Arrange
		var feature = CreateTestFeature("collection1", -122.4194, 37.7749);
		var id = await _featureRepository!.InsertAsync(feature);

		// Act
		var retrieved = await _featureRepository.GetByIdAsync(id);

		// Assert
		retrieved.Should().NotBeNull();
		retrieved!.Id.Should().Be(id);
		retrieved.CollectionId.Should().Be("collection1");
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
	{
		// Act
		var retrieved = await _featureRepository!.GetByIdAsync("nonexistent");

		// Assert
		retrieved.Should().BeNull();
	}

	[Fact]
	public async Task UpdateAsync_ShouldUpdateFeature_AndIncrementVersion()
	{
		// Arrange
		var feature = CreateTestFeature("collection1", -122.4194, 37.7749);
		await _featureRepository!.InsertAsync(feature);
		var originalVersion = feature.Version;

		// Act
		feature.Properties = "{\"name\": \"Updated\"}";
		await _featureRepository.UpdateAsync(feature);

		// Assert
		var updated = await _featureRepository.GetByIdAsync(feature.Id);
		updated.Should().NotBeNull();
		updated!.Properties.Should().Contain("Updated");
		updated.Version.Should().Be(originalVersion + 1);
		updated.SyncStatus.Should().Be(SyncStatus.Pending.ToString());
	}

	[Fact]
	public async Task DeleteAsync_ShouldDeleteFeature()
	{
		// Arrange
		var feature = CreateTestFeature("collection1", -122.4194, 37.7749);
		var id = await _featureRepository!.InsertAsync(feature);

		// Act
		var result = await _featureRepository.DeleteAsync(id);

		// Assert
		result.Should().BeGreaterThan(0);
		var deleted = await _featureRepository.GetByIdAsync(id);
		deleted.Should().BeNull();
	}

	[Fact]
	public async Task GetAllAsync_ShouldReturnAllFeatures()
	{
		// Arrange
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4084, 37.7849));
		await _featureRepository.InsertAsync(CreateTestFeature("col2", -122.4284, 37.7649));

		// Act
		var features = await _featureRepository.GetAllAsync();

		// Assert
		features.Should().HaveCount(3);
	}

	[Fact]
	public async Task GetByCollectionIdAsync_ShouldReturnFeaturesForCollection()
	{
		// Arrange
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4084, 37.7849));
		await _featureRepository.InsertAsync(CreateTestFeature("col2", -122.4284, 37.7649));

		// Act
		var features = await _featureRepository.GetByCollectionIdAsync("col1");

		// Assert
		features.Should().HaveCount(2);
		features.Should().AllSatisfy(f => f.CollectionId.Should().Be("col1"));
	}

	#endregion

	#region Spatial Query Tests

	[Fact]
	public async Task GetByBoundsAsync_ShouldReturnFeaturesWithinBounds()
	{
		// Arrange - San Francisco area features
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749)); // SF downtown
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4084, 37.7849)); // North Beach
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.5, 37.8)); // Outside bounds

		// Act - Query for downtown SF area
		var features = await _featureRepository.GetByBoundsAsync(-122.43, 37.77, -122.40, 37.79);

		// Assert
		features.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetWithinDistanceAsync_ShouldReturnNearbyFeatures()
	{
		// Arrange
		var centerPoint = new Point(new Coordinate(-122.4194, 37.7749)); // SF downtown
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749)); // Exact location
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4184, 37.7759)); // ~100m away
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.5, 37.8)); // Far away

		// Act - Query for features within 500m
		var features = await _featureRepository.GetWithinDistanceAsync(centerPoint, 500);

		// Assert - Should find the two nearby features
		features.Should().HaveCountGreaterOrEqualTo(2);
	}

	[Fact]
	public async Task GetNearestAsync_ShouldReturnClosestFeature()
	{
		// Arrange
		var queryPoint = new Point(new Coordinate(-122.4194, 37.7749));
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4184, 37.7759)); // Close
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.5, 37.8)); // Far
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4195, 37.7750)); // Closest

		// Act
		var nearest = await _featureRepository.GetNearestAsync(queryPoint);

		// Assert
		nearest.Should().NotBeNull();
		var geometry = nearest!.GetGeometry() as Point;
		geometry.Should().NotBeNull();
		// The closest point should be very near the query point
		geometry!.X.Should().BeApproximately(-122.4195, 0.001);
		geometry.Y.Should().BeApproximately(37.7750, 0.001);
	}

	[Fact]
	public async Task GetIntersectingAsync_ShouldReturnIntersectingFeatures()
	{
		// Arrange
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4084, 37.7849));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.5, 37.8));

		// Act - Query with polygon covering first two points
		var factory = new GeometryFactory();
		var polygon = factory.CreatePolygon(new[]
		{
			new Coordinate(-122.43, 37.77),
			new Coordinate(-122.40, 37.77),
			new Coordinate(-122.40, 37.79),
			new Coordinate(-122.43, 37.79),
			new Coordinate(-122.43, 37.77)
		});

		var features = await _featureRepository.GetIntersectingAsync(polygon);

		// Assert
		features.Should().HaveCount(2);
	}

	#endregion

	#region Sync Operation Tests

	[Fact]
	public async Task GetPendingSyncAsync_ShouldReturnPendingFeatures()
	{
		// Arrange
		var feature1 = CreateTestFeature("col1", -122.4194, 37.7749);
		await _featureRepository!.InsertAsync(feature1);

		var feature2 = CreateTestFeature("col1", -122.4084, 37.7849);
		await _featureRepository.InsertAsync(feature2);
		await _featureRepository.UpdateSyncStatusAsync(feature2.Id, SyncStatus.Synced);

		// Act
		var pending = await _featureRepository.GetPendingSyncAsync();

		// Assert
		pending.Should().HaveCount(1);
		pending[0].Id.Should().Be(feature1.Id);
	}

	[Fact]
	public async Task UpdateSyncStatusAsync_ShouldUpdateStatus()
	{
		// Arrange
		var feature = CreateTestFeature("col1", -122.4194, 37.7749);
		var id = await _featureRepository!.InsertAsync(feature);

		// Act
		await _featureRepository.UpdateSyncStatusAsync(id, SyncStatus.Synced);

		// Assert
		var updated = await _featureRepository.GetByIdAsync(id);
		updated!.SyncStatus.Should().Be(SyncStatus.Synced.ToString());
	}

	[Fact]
	public async Task UpdateServerIdAsync_ShouldUpdateServerId()
	{
		// Arrange
		var feature = CreateTestFeature("col1", -122.4194, 37.7749);
		var localId = await _featureRepository!.InsertAsync(feature);
		var serverId = "server_123";

		// Act
		await _featureRepository.UpdateServerIdAsync(localId, serverId);

		// Assert
		var updated = await _featureRepository.GetByIdAsync(localId);
		updated!.ServerId.Should().Be(serverId);
	}

	#endregion

	#region Batch Operation Tests

	[Fact]
	public async Task InsertBatchAsync_ShouldInsertMultipleFeatures()
	{
		// Arrange
		var features = new List<Feature>
		{
			CreateTestFeature("col1", -122.4194, 37.7749),
			CreateTestFeature("col1", -122.4084, 37.7849),
			CreateTestFeature("col2", -122.4284, 37.7649)
		};

		// Act
		var result = await _featureRepository!.InsertBatchAsync(features);

		// Assert
		result.Should().Be(3);
		var all = await _featureRepository.GetAllAsync();
		all.Should().HaveCount(3);
	}

	[Fact]
	public async Task UpdateBatchAsync_ShouldUpdateMultipleFeatures()
	{
		// Arrange
		var features = new List<Feature>
		{
			CreateTestFeature("col1", -122.4194, 37.7749),
			CreateTestFeature("col1", -122.4084, 37.7849)
		};
		await _featureRepository!.InsertBatchAsync(features);

		// Act
		foreach (var f in features)
		{
			f.Properties = "{\"updated\": true}";
		}
		var result = await _featureRepository.UpdateBatchAsync(features);

		// Assert
		result.Should().Be(2);
		var updated = await _featureRepository.GetAllAsync();
		updated.Should().AllSatisfy(f => f.Properties.Should().Contain("updated"));
	}

	[Fact]
	public async Task DeleteBatchAsync_ShouldDeleteMultipleFeatures()
	{
		// Arrange
		var feature1 = CreateTestFeature("col1", -122.4194, 37.7749);
		var id1 = await _featureRepository!.InsertAsync(feature1);
		var feature2 = CreateTestFeature("col1", -122.4084, 37.7849);
		var id2 = await _featureRepository.InsertAsync(feature2);

		// Act
		var result = await _featureRepository.DeleteBatchAsync(new List<string> { id1, id2 });

		// Assert
		result.Should().Be(2);
		var remaining = await _featureRepository.GetAllAsync();
		remaining.Should().BeEmpty();
	}

	#endregion

	#region Statistics Tests

	[Fact]
	public async Task GetCountAsync_ShouldReturnTotalCount()
	{
		// Arrange
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4084, 37.7849));
		await _featureRepository.InsertAsync(CreateTestFeature("col2", -122.4284, 37.7649));

		// Act
		var count = await _featureRepository.GetCountAsync();

		// Assert
		count.Should().Be(3);
	}

	[Fact]
	public async Task GetCountByCollectionAsync_ShouldReturnCollectionCount()
	{
		// Arrange
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.4194, 37.7749));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4084, 37.7849));
		await _featureRepository.InsertAsync(CreateTestFeature("col2", -122.4284, 37.7649));

		// Act
		var count = await _featureRepository.GetCountByCollectionAsync("col1");

		// Assert
		count.Should().Be(2);
	}

	[Fact]
	public async Task GetExtentAsync_ShouldReturnBoundingBox()
	{
		// Arrange
		await _featureRepository!.InsertAsync(CreateTestFeature("col1", -122.5, 37.7));
		await _featureRepository.InsertAsync(CreateTestFeature("col1", -122.4, 37.8));

		// Act
		var extent = await _featureRepository.GetExtentAsync();

		// Assert
		extent.Should().NotBeNull();
		extent!.Value.minX.Should().BeApproximately(-122.5, 0.001);
		extent.Value.maxX.Should().BeApproximately(-122.4, 0.001);
		extent.Value.minY.Should().BeApproximately(37.7, 0.001);
		extent.Value.maxY.Should().BeApproximately(37.8, 0.001);
	}

	#endregion

	#region Change Tracking Tests

	[Fact]
	public async Task InsertAsync_ShouldCreateChangeRecord()
	{
		// Arrange
		var feature = CreateTestFeature("col1", -122.4194, 37.7749);

		// Act
		await _featureRepository!.InsertAsync(feature);

		// Assert
		var changes = await _changeRepository!.GetByFeatureIdAsync(feature.Id);
		changes.Should().HaveCount(1);
		changes[0].Operation.Should().Be(ChangeOperation.Insert.ToString());
	}

	[Fact]
	public async Task UpdateAsync_ShouldCreateChangeRecord()
	{
		// Arrange
		var feature = CreateTestFeature("col1", -122.4194, 37.7749);
		await _featureRepository!.InsertAsync(feature);

		// Act
		feature.Properties = "{\"updated\": true}";
		await _featureRepository.UpdateAsync(feature);

		// Assert
		var changes = await _changeRepository!.GetByFeatureIdAsync(feature.Id);
		changes.Should().HaveCount(2); // INSERT + UPDATE
		changes[1].Operation.Should().Be(ChangeOperation.Update.ToString());
	}

	[Fact]
	public async Task DeleteAsync_ShouldCreateChangeRecord()
	{
		// Arrange
		var feature = CreateTestFeature("col1", -122.4194, 37.7749);
		var id = await _featureRepository!.InsertAsync(feature);

		// Act
		await _featureRepository.DeleteAsync(id);

		// Assert
		var changes = await _changeRepository!.GetByFeatureIdAsync(id);
		changes.Should().HaveCount(2); // INSERT + DELETE
		changes[1].Operation.Should().Be(ChangeOperation.Delete.ToString());
	}

	#endregion

	#region Helper Methods

	private Feature CreateTestFeature(string collectionId, double longitude, double latitude)
	{
		var point = new Point(new Coordinate(longitude, latitude));
		var feature = new Feature
		{
			CollectionId = collectionId,
			Properties = "{\"name\": \"Test Feature\"}",
			CreatedBy = "test_user"
		};
		feature.SetGeometry(point);
		return feature;
	}

	#endregion
}
