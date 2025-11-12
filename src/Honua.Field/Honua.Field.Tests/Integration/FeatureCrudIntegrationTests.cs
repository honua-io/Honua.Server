// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Models;
using HonuaField.Tests.Integration.Infrastructure;
using NetTopologySuite.Geometries;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for complete feature CRUD lifecycle
/// Tests real database operations with FeatureRepository, AttachmentRepository, and ChangeRepository
/// </summary>
public class FeatureCrudIntegrationTests : IntegrationTestBase
{
	[Fact]
	public async Task CreateFeature_WithPropertiesAndGeometry_ShouldPersistToDatabase()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var properties = new Dictionary<string, object>
		{
			{ "name", "Test Building" },
			{ "type", "Commercial" },
			{ "floors", 5 }
		};
		var point = DataBuilder.CreateRandomPoint(45.5231, -122.6765);
		var feature = DataBuilder.CreateTestFeature(collection.Id, point, properties);

		// Act
		var featureId = await FeatureRepository.InsertAsync(feature);

		// Assert
		featureId.Should().NotBeNullOrEmpty();

		var retrievedFeature = await FeatureRepository.GetByIdAsync(featureId);
		retrievedFeature.Should().NotBeNull();
		retrievedFeature!.CollectionId.Should().Be(collection.Id);
		retrievedFeature.GetGeometry().Should().BeOfType<Point>();
		retrievedFeature.GetPropertiesDict().Should().ContainKey("name");
		retrievedFeature.GetPropertiesDict()["name"].ToString().Should().Be("Test Building");
	}

	[Fact]
	public async Task CreateFeature_WithAttachments_ShouldStoreBothFeatureAndAttachments()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		var featureId = await FeatureRepository.InsertAsync(feature);

		var attachment1 = DataBuilder.CreateTestAttachment(featureId, AttachmentType.Photo);
		var attachment2 = DataBuilder.CreateTestAttachment(featureId, AttachmentType.Photo);

		// Act
		await AttachmentRepository.InsertAsync(attachment1);
		await AttachmentRepository.InsertAsync(attachment2);

		// Assert
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(featureId);
		attachments.Should().HaveCount(2);
		attachments.All(a => a.FeatureId == featureId).Should().BeTrue();
		attachments.All(a => File.Exists(a.Filepath)).Should().BeTrue();
	}

	[Fact]
	public async Task UpdateFeature_Properties_ShouldUpdateInDatabase()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var originalVersion = feature.Version;

		// Act - Update properties
		var updatedProperties = new Dictionary<string, object>
		{
			{ "name", "Updated Building" },
			{ "status", "Active" }
		};
		feature.Properties = System.Text.Json.JsonSerializer.Serialize(updatedProperties);
		feature.Version++;
		feature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		await FeatureRepository.UpdateAsync(feature);

		// Assert
		var retrievedFeature = await FeatureRepository.GetByIdAsync(feature.Id);
		retrievedFeature.Should().NotBeNull();
		retrievedFeature!.Version.Should().Be(originalVersion + 1);
		retrievedFeature.GetPropertiesDict()["name"].ToString().Should().Be("Updated Building");
	}

	[Fact]
	public async Task UpdateFeature_Geometry_ShouldUpdateInDatabase()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var originalPoint = DataBuilder.CreateRandomPoint(45.0, -122.0);
		var feature = DataBuilder.CreateTestFeature(collection.Id, originalPoint);
		await FeatureRepository.InsertAsync(feature);

		// Act - Update geometry
		var newPoint = DataBuilder.CreateRandomPoint(46.0, -123.0);
		feature.SetGeometry(newPoint);
		feature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		await FeatureRepository.UpdateAsync(feature);

		// Assert
		var retrievedFeature = await FeatureRepository.GetByIdAsync(feature.Id);
		retrievedFeature.Should().NotBeNull();
		var geometry = retrievedFeature!.GetGeometry() as Point;
		geometry.Should().NotBeNull();
		geometry!.X.Should().BeApproximately(newPoint.X, 0.0001);
		geometry.Y.Should().BeApproximately(newPoint.Y, 0.0001);
	}

	[Fact]
	public async Task AddAttachment_ToExistingFeature_ShouldPersistAttachment()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		// Act - Add attachment after feature creation
		var attachment = DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Photo);
		await AttachmentRepository.InsertAsync(attachment);

		// Assert
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);
		attachments.Should().HaveCount(1);
		attachments[0].Id.Should().Be(attachment.Id);
		File.Exists(attachments[0].Filepath).Should().BeTrue();
	}

	[Fact]
	public async Task RemoveAttachment_FromFeature_ShouldDeleteAttachment()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var attachment = DataBuilder.CreateTestAttachment(feature.Id);
		await AttachmentRepository.InsertAsync(attachment);

		// Act - Remove attachment
		await AttachmentRepository.DeleteAsync(attachment.Id);

		// Assert
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);
		attachments.Should().BeEmpty();
	}

	[Fact]
	public async Task DeleteFeature_WithAttachments_ShouldCascadeDelete()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var attachment1 = DataBuilder.CreateTestAttachment(feature.Id);
		var attachment2 = DataBuilder.CreateTestAttachment(feature.Id);
		await AttachmentRepository.InsertAsync(attachment1);
		await AttachmentRepository.InsertAsync(attachment2);

		// Act - Delete feature
		await FeatureRepository.DeleteAsync(feature.Id);

		// Assert - Feature should be deleted
		var deletedFeature = await FeatureRepository.GetByIdAsync(feature.Id);
		deletedFeature.Should().BeNull();

		// Attachments should be deleted (cascade)
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);
		attachments.Should().BeEmpty();
	}

	[Fact]
	public async Task SearchFeatures_ByCollectionId_ShouldReturnMatchingFeatures()
	{
		// Arrange
		var collection1 = DataBuilder.CreateTestCollection("Collection 1");
		var collection2 = DataBuilder.CreateTestCollection("Collection 2");
		await CollectionRepository.InsertAsync(collection1);
		await CollectionRepository.InsertAsync(collection2);

		var feature1 = DataBuilder.CreateTestFeature(collection1.Id);
		var feature2 = DataBuilder.CreateTestFeature(collection1.Id);
		var feature3 = DataBuilder.CreateTestFeature(collection2.Id);

		await FeatureRepository.InsertAsync(feature1);
		await FeatureRepository.InsertAsync(feature2);
		await FeatureRepository.InsertAsync(feature3);

		// Act
		var collection1Features = await FeatureRepository.GetByCollectionIdAsync(collection1.Id);

		// Assert
		collection1Features.Should().HaveCount(2);
		collection1Features.All(f => f.CollectionId == collection1.Id).Should().BeTrue();
	}

	[Fact]
	public async Task QueryFeatures_BySpatialBounds_ShouldReturnFeaturesInBounds()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Create features at specific locations
		var featureInside1 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(45.5, -122.6));
		var featureInside2 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(45.5, -122.7));
		var featureOutside = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(46.0, -123.0));

		await FeatureRepository.InsertAsync(featureInside1);
		await FeatureRepository.InsertAsync(featureInside2);
		await FeatureRepository.InsertAsync(featureOutside);

		// Act - Query for features in Portland area
		var bounds = await FeatureRepository.GetByBoundsAsync(
			minX: -122.8,
			minY: 45.4,
			maxX: -122.5,
			maxY: 45.6);

		// Assert
		bounds.Should().HaveCount(2);
		bounds.Should().Contain(f => f.Id == featureInside1.Id);
		bounds.Should().Contain(f => f.Id == featureInside2.Id);
		bounds.Should().NotContain(f => f.Id == featureOutside.Id);
	}

	[Fact]
	public async Task CreateFeature_ShouldRecordChangeForSync()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);

		// Act
		await FeatureRepository.InsertAsync(feature);

		// Create a change record for sync
		var change = new Change
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = feature.Id,
			ChangeType = "Create",
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Synced = 0
		};
		await ChangeRepository.InsertAsync(change);

		// Assert
		var pendingChanges = await ChangeRepository.GetPendingSyncAsync();
		pendingChanges.Should().HaveCount(1);
		pendingChanges[0].FeatureId.Should().Be(feature.Id);
		pendingChanges[0].ChangeType.Should().Be("Create");
	}

	[Fact]
	public async Task GetFeatureCount_ByCollection_ShouldReturnCorrectCount()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		for (int i = 0; i < 5; i++)
		{
			var feature = DataBuilder.CreateTestFeature(collection.Id);
			await FeatureRepository.InsertAsync(feature);
		}

		// Act
		var count = await FeatureRepository.GetCountByCollectionAsync(collection.Id);

		// Assert
		count.Should().Be(5);
	}

	[Fact]
	public async Task GetFeatureExtent_ForCollection_ShouldReturnBoundingBox()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Create features at known locations
		var feature1 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(45.0, -122.0));
		var feature2 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(46.0, -123.0));

		await FeatureRepository.InsertAsync(feature1);
		await FeatureRepository.InsertAsync(feature2);

		// Act
		var extent = await FeatureRepository.GetExtentAsync(collection.Id);

		// Assert
		extent.Should().NotBeNull();
		extent!.Value.minX.Should().BeLessOrEqualTo(-122.0);
		extent.Value.maxX.Should().BeGreaterOrEqualTo(-122.0);
		extent.Value.minY.Should().BeLessOrEqualTo(45.0);
		extent.Value.maxY.Should().BeGreaterOrEqualTo(45.0);
	}

	[Fact]
	public async Task BatchInsert_MultipleFeatures_ShouldInsertAll()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var features = new List<Feature>();
		for (int i = 0; i < 10; i++)
		{
			features.Add(DataBuilder.CreateTestFeature(collection.Id));
		}

		// Act
		var insertedCount = await FeatureRepository.InsertBatchAsync(features);

		// Assert
		insertedCount.Should().Be(10);
		var allFeatures = await FeatureRepository.GetByCollectionIdAsync(collection.Id);
		allFeatures.Should().HaveCount(10);
	}

	[Fact]
	public async Task UpdateSyncStatus_ForFeature_ShouldUpdateStatus()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		feature.SyncStatus = SyncStatus.Pending.ToString();
		await FeatureRepository.InsertAsync(feature);

		// Act
		await FeatureRepository.UpdateSyncStatusAsync(feature.Id, SyncStatus.Synced);

		// Assert
		var updatedFeature = await FeatureRepository.GetByIdAsync(feature.Id);
		updatedFeature.Should().NotBeNull();
		updatedFeature!.SyncStatus.Should().Be(SyncStatus.Synced.ToString());
	}
}
