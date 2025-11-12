// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for FeaturesService
/// Tests CRUD operations, spatial queries, attachments, and change tracking
/// </summary>
public class FeaturesServiceTests
{
	private readonly Mock<IFeatureRepository> _mockFeatureRepository;
	private readonly Mock<IAttachmentRepository> _mockAttachmentRepository;
	private readonly Mock<IChangeRepository> _mockChangeRepository;
	private readonly Mock<ICollectionRepository> _mockCollectionRepository;
	private readonly FeaturesService _service;

	public FeaturesServiceTests()
	{
		_mockFeatureRepository = new Mock<IFeatureRepository>();
		_mockAttachmentRepository = new Mock<IAttachmentRepository>();
		_mockChangeRepository = new Mock<IChangeRepository>();
		_mockCollectionRepository = new Mock<ICollectionRepository>();

		_service = new FeaturesService(
			_mockFeatureRepository.Object,
			_mockAttachmentRepository.Object,
			_mockChangeRepository.Object,
			_mockCollectionRepository.Object);
	}

	#region CRUD Tests

	[Fact]
	public async Task GetFeatureByIdAsync_ShouldReturnFeature_WhenFeatureExists()
	{
		// Arrange
		var featureId = "feature-123";
		var feature = new Feature { Id = featureId, CollectionId = "coll-1" };

		_mockFeatureRepository
			.Setup(x => x.GetByIdAsync(featureId))
			.ReturnsAsync(feature);

		// Act
		var result = await _service.GetFeatureByIdAsync(featureId);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(featureId);
		_mockFeatureRepository.Verify(x => x.GetByIdAsync(featureId), Times.Once);
	}

	[Fact]
	public async Task GetFeatureByIdAsync_ShouldReturnNull_WhenFeatureDoesNotExist()
	{
		// Arrange
		var featureId = "nonexistent";

		_mockFeatureRepository
			.Setup(x => x.GetByIdAsync(featureId))
			.ReturnsAsync((Feature?)null);

		// Act
		var result = await _service.GetFeatureByIdAsync(featureId);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task GetFeaturesByCollectionIdAsync_ShouldReturnPaginatedFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId },
			new() { Id = "f3", CollectionId = collectionId }
		};

		_mockFeatureRepository
			.Setup(x => x.GetByCollectionIdAsync(collectionId))
			.ReturnsAsync(features);

		// Act
		var result = await _service.GetFeaturesByCollectionIdAsync(collectionId, skip: 0, take: 2);

		// Assert
		result.Should().HaveCount(2);
		result[0].Id.Should().Be("f1");
		result[1].Id.Should().Be("f2");
	}

	[Fact]
	public async Task CreateFeatureAsync_ShouldCreateFeature_WhenCollectionExists()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = new Collection { Id = collectionId, Title = "Test Collection" };
		var feature = new Feature { CollectionId = collectionId, Properties = "{}" };

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		_mockFeatureRepository
			.Setup(x => x.InsertAsync(It.IsAny<Feature>()))
			.ReturnsAsync("new-feature-id");

		_mockCollectionRepository
			.Setup(x => x.IncrementItemsCountAsync(collectionId, 1))
			.ReturnsAsync(1);

		// Act
		var result = await _service.CreateFeatureAsync(feature);

		// Assert
		result.Should().Be("new-feature-id");
		_mockFeatureRepository.Verify(x => x.InsertAsync(It.IsAny<Feature>()), Times.Once);
		_mockCollectionRepository.Verify(x => x.IncrementItemsCountAsync(collectionId, 1), Times.Once);
	}

	[Fact]
	public async Task CreateFeatureAsync_ShouldThrowException_WhenCollectionDoesNotExist()
	{
		// Arrange
		var feature = new Feature { CollectionId = "nonexistent", Properties = "{}" };

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync("nonexistent"))
			.ReturnsAsync((Collection?)null);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await _service.CreateFeatureAsync(feature));
	}

	[Fact]
	public async Task UpdateFeatureAsync_ShouldUpdateFeature_WhenFeatureExists()
	{
		// Arrange
		var feature = new Feature { Id = "f1", CollectionId = "coll-1", Properties = "{}" };

		_mockFeatureRepository
			.Setup(x => x.UpdateAsync(It.IsAny<Feature>()))
			.ReturnsAsync(1);

		// Act
		var result = await _service.UpdateFeatureAsync(feature);

		// Assert
		result.Should().BeTrue();
		_mockFeatureRepository.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task DeleteFeatureAsync_ShouldDeleteFeatureAndAttachments()
	{
		// Arrange
		var featureId = "f1";
		var feature = new Feature { Id = featureId, CollectionId = "coll-1" };

		_mockFeatureRepository
			.Setup(x => x.GetByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockAttachmentRepository
			.Setup(x => x.DeleteByFeatureIdAsync(featureId))
			.ReturnsAsync(2);

		_mockFeatureRepository
			.Setup(x => x.DeleteAsync(featureId))
			.ReturnsAsync(1);

		_mockCollectionRepository
			.Setup(x => x.IncrementItemsCountAsync("coll-1", -1))
			.ReturnsAsync(1);

		// Act
		var result = await _service.DeleteFeatureAsync(featureId);

		// Assert
		result.Should().BeTrue();
		_mockAttachmentRepository.Verify(x => x.DeleteByFeatureIdAsync(featureId), Times.Once);
		_mockFeatureRepository.Verify(x => x.DeleteAsync(featureId), Times.Once);
		_mockCollectionRepository.Verify(x => x.IncrementItemsCountAsync("coll-1", -1), Times.Once);
	}

	#endregion

	#region Search and Filtering Tests

	[Fact]
	public async Task SearchFeaturesAsync_ShouldReturnAllFeatures_WhenSearchTextIsEmpty()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId, Properties = "{\"name\":\"Feature 1\"}" },
			new() { Id = "f2", CollectionId = collectionId, Properties = "{\"name\":\"Feature 2\"}" }
		};

		_mockFeatureRepository
			.Setup(x => x.GetByCollectionIdAsync(collectionId))
			.ReturnsAsync(features);

		// Act
		var result = await _service.SearchFeaturesAsync(collectionId, "");

		// Assert
		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task SearchFeaturesAsync_ShouldFilterFeatures_WhenSearchTextMatches()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId, Properties = "{\"name\":\"Park\"}" },
			new() { Id = "f2", CollectionId = collectionId, Properties = "{\"name\":\"School\"}" }
		};

		_mockFeatureRepository
			.Setup(x => x.GetByCollectionIdAsync(collectionId))
			.ReturnsAsync(features);

		// Act
		var result = await _service.SearchFeaturesAsync(collectionId, "park");

		// Assert
		result.Should().HaveCount(1);
		result[0].Id.Should().Be("f1");
	}

	[Fact]
	public async Task GetFeaturesByPropertyAsync_ShouldReturnMatchingFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId, Properties = "{\"type\":\"park\"}" },
			new() { Id = "f2", CollectionId = collectionId, Properties = "{\"type\":\"school\"}" },
			new() { Id = "f3", CollectionId = collectionId, Properties = "{\"type\":\"park\"}" }
		};

		_mockFeatureRepository
			.Setup(x => x.GetByCollectionIdAsync(collectionId))
			.ReturnsAsync(features);

		// Act
		var result = await _service.GetFeaturesByPropertyAsync(collectionId, "type", "park");

		// Assert
		result.Should().HaveCount(2);
		result.Should().Contain(f => f.Id == "f1");
		result.Should().Contain(f => f.Id == "f3");
	}

	#endregion

	#region Spatial Query Tests

	[Fact]
	public async Task GetFeaturesInBoundsAsync_ShouldReturnFeaturesInBounds()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId },
			new() { Id = "f3", CollectionId = "other-coll" }
		};

		_mockFeatureRepository
			.Setup(x => x.GetByBoundsAsync(-180, -90, 180, 90))
			.ReturnsAsync(features);

		// Act
		var result = await _service.GetFeaturesInBoundsAsync(collectionId, -180, -90, 180, 90);

		// Assert
		result.Should().HaveCount(2);
		result.Should().AllSatisfy(f => f.CollectionId.Should().Be(collectionId));
	}

	[Fact]
	public async Task GetFeaturesNearbyAsync_ShouldReturnNearbyFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var point = new Point(0, 0);
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = "other-coll" }
		};

		_mockFeatureRepository
			.Setup(x => x.GetWithinDistanceAsync(It.IsAny<Point>(), 1000))
			.ReturnsAsync(features);

		// Act
		var result = await _service.GetFeaturesNearbyAsync(0, 0, 1000, collectionId);

		// Assert
		result.Should().HaveCount(1);
		result[0].CollectionId.Should().Be(collectionId);
	}

	[Fact]
	public async Task GetNearestFeatureAsync_ShouldReturnNearestFeature()
	{
		// Arrange
		var nearestFeature = new Feature { Id = "f1", CollectionId = "coll-1" };

		_mockFeatureRepository
			.Setup(x => x.GetNearestAsync(It.IsAny<Point>()))
			.ReturnsAsync(nearestFeature);

		// Act
		var result = await _service.GetNearestFeatureAsync(0, 0);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be("f1");
	}

	#endregion

	#region Attachment Tests

	[Fact]
	public async Task GetFeatureAttachmentsAsync_ShouldReturnAttachments()
	{
		// Arrange
		var featureId = "f1";
		var attachments = new List<Attachment>
		{
			new() { Id = "a1", FeatureId = featureId },
			new() { Id = "a2", FeatureId = featureId }
		};

		_mockAttachmentRepository
			.Setup(x => x.GetByFeatureIdAsync(featureId))
			.ReturnsAsync(attachments);

		// Act
		var result = await _service.GetFeatureAttachmentsAsync(featureId);

		// Assert
		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task AddAttachmentAsync_ShouldAddAttachment_WhenFeatureExists()
	{
		// Arrange
		var featureId = "f1";
		var feature = new Feature { Id = featureId, CollectionId = "coll-1" };
		var attachment = new Attachment { FeatureId = featureId, Filename = "photo.jpg" };

		_mockFeatureRepository
			.Setup(x => x.GetByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockAttachmentRepository
			.Setup(x => x.InsertAsync(It.IsAny<Attachment>()))
			.ReturnsAsync("new-attachment-id");

		// Act
		var result = await _service.AddAttachmentAsync(attachment);

		// Assert
		result.Should().Be("new-attachment-id");
		_mockAttachmentRepository.Verify(x => x.InsertAsync(It.IsAny<Attachment>()), Times.Once);
	}

	[Fact]
	public async Task AddAttachmentAsync_ShouldThrowException_WhenFeatureDoesNotExist()
	{
		// Arrange
		var attachment = new Attachment { FeatureId = "nonexistent", Filename = "photo.jpg" };

		_mockFeatureRepository
			.Setup(x => x.GetByIdAsync("nonexistent"))
			.ReturnsAsync((Feature?)null);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await _service.AddAttachmentAsync(attachment));
	}

	[Fact]
	public async Task DeleteAttachmentAsync_ShouldDeleteAttachment()
	{
		// Arrange
		var attachmentId = "a1";

		_mockAttachmentRepository
			.Setup(x => x.DeleteAsync(attachmentId))
			.ReturnsAsync(1);

		// Act
		var result = await _service.DeleteAttachmentAsync(attachmentId);

		// Assert
		result.Should().BeTrue();
		_mockAttachmentRepository.Verify(x => x.DeleteAsync(attachmentId), Times.Once);
	}

	[Fact]
	public async Task GetAttachmentsByTypeAsync_ShouldReturnAttachmentsByType()
	{
		// Arrange
		var featureId = "f1";
		var attachments = new List<Attachment>
		{
			new() { Id = "a1", FeatureId = featureId, Type = AttachmentType.Photo.ToString() }
		};

		_mockAttachmentRepository
			.Setup(x => x.GetByFeatureAndTypeAsync(featureId, AttachmentType.Photo))
			.ReturnsAsync(attachments);

		// Act
		var result = await _service.GetAttachmentsByTypeAsync(featureId, AttachmentType.Photo);

		// Assert
		result.Should().HaveCount(1);
		result[0].Type.Should().Be(AttachmentType.Photo.ToString());
	}

	#endregion

	#region Sync and Change Tracking Tests

	[Fact]
	public async Task GetPendingSyncFeaturesAsync_ShouldReturnPendingFeatures()
	{
		// Arrange
		var features = new List<Feature>
		{
			new() { Id = "f1", SyncStatus = SyncStatus.Pending.ToString() },
			new() { Id = "f2", SyncStatus = SyncStatus.Pending.ToString() }
		};

		_mockFeatureRepository
			.Setup(x => x.GetPendingSyncAsync())
			.ReturnsAsync(features);

		// Act
		var result = await _service.GetPendingSyncFeaturesAsync();

		// Assert
		result.Should().HaveCount(2);
		result.Should().AllSatisfy(f => f.SyncStatus.Should().Be(SyncStatus.Pending.ToString()));
	}

	[Fact]
	public async Task GetPendingChangesCountAsync_ShouldReturnCount()
	{
		// Arrange
		_mockChangeRepository
			.Setup(x => x.GetPendingCountAsync())
			.ReturnsAsync(5);

		// Act
		var result = await _service.GetPendingChangesCountAsync();

		// Assert
		result.Should().Be(5);
	}

	[Fact]
	public async Task MarkFeatureAsSyncedAsync_ShouldUpdateSyncStatus()
	{
		// Arrange
		var featureId = "f1";

		_mockFeatureRepository
			.Setup(x => x.UpdateSyncStatusAsync(featureId, SyncStatus.Synced))
			.ReturnsAsync(1);

		// Act
		var result = await _service.MarkFeatureAsSyncedAsync(featureId);

		// Assert
		result.Should().BeTrue();
		_mockFeatureRepository.Verify(
			x => x.UpdateSyncStatusAsync(featureId, SyncStatus.Synced),
			Times.Once);
	}

	#endregion

	#region Statistics Tests

	[Fact]
	public async Task GetFeatureCountAsync_ShouldReturnCount()
	{
		// Arrange
		var collectionId = "coll-1";

		_mockFeatureRepository
			.Setup(x => x.GetCountByCollectionAsync(collectionId))
			.ReturnsAsync(10);

		// Act
		var result = await _service.GetFeatureCountAsync(collectionId);

		// Assert
		result.Should().Be(10);
	}

	[Fact]
	public async Task GetCollectionExtentAsync_ShouldReturnExtent()
	{
		// Arrange
		var collectionId = "coll-1";
		var extent = (-180.0, -90.0, 180.0, 90.0);

		_mockFeatureRepository
			.Setup(x => x.GetExtentAsync(collectionId))
			.ReturnsAsync(extent);

		// Act
		var result = await _service.GetCollectionExtentAsync(collectionId);

		// Assert
		result.Should().NotBeNull();
		result!.Value.minX.Should().Be(-180);
		result.Value.maxX.Should().Be(180);
	}

	[Fact]
	public async Task GetAttachmentsSizeAsync_ShouldReturnTotalSize()
	{
		// Arrange
		var featureId = "f1";

		_mockAttachmentRepository
			.Setup(x => x.GetTotalSizeByFeatureAsync(featureId))
			.ReturnsAsync(1024000);

		// Act
		var result = await _service.GetAttachmentsSizeAsync(featureId);

		// Assert
		result.Should().Be(1024000);
	}

	#endregion
}
