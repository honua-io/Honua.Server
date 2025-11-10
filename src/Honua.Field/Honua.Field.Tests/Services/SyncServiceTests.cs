using FluentAssertions;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using Moq;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for SyncService
/// Tests synchronization workflows, conflict detection, and error handling
/// </summary>
public class SyncServiceTests
{
	private readonly Mock<IApiClient> _mockApiClient;
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly Mock<IFeatureRepository> _mockFeatureRepo;
	private readonly Mock<ICollectionRepository> _mockCollectionRepo;
	private readonly Mock<IChangeRepository> _mockChangeRepo;
	private readonly Mock<IConflictResolutionService> _mockConflictService;
	private readonly SyncService _syncService;

	public SyncServiceTests()
	{
		_mockApiClient = new Mock<IApiClient>();
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockSettingsService = new Mock<ISettingsService>();
		_mockFeatureRepo = new Mock<IFeatureRepository>();
		_mockCollectionRepo = new Mock<ICollectionRepository>();
		_mockChangeRepo = new Mock<IChangeRepository>();
		_mockConflictService = new Mock<IConflictResolutionService>();

		_syncService = new SyncService(
			_mockApiClient.Object,
			_mockAuthService.Object,
			_mockSettingsService.Object,
			_mockFeatureRepo.Object,
			_mockCollectionRepo.Object,
			_mockChangeRepo.Object,
			_mockConflictService.Object);
	}

	#region SynchronizeAsync Tests

	[Fact]
	public async Task SynchronizeAsync_ShouldReturnFailure_WhenNotAuthenticated()
	{
		// Arrange
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync())
			.ReturnsAsync(false);

		// Act
		var result = await _syncService.SynchronizeAsync();

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Contain("authenticated");
		_mockApiClient.Verify(x => x.GetAsync<It.IsAnyType>(
			It.IsAny<string>(), It.IsAny<string>()), Times.Never);
	}

	[Fact]
	public async Task SynchronizeAsync_ShouldPullThenPush_WhenAuthenticated()
	{
		// Arrange
		SetupAuthenticatedUser();
		SetupEmptyPullResponse();
		SetupEmptyPushResponse();

		// Act
		var result = await _syncService.SynchronizeAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.PullResult.Success.Should().BeTrue();
		result.PushResult.Success.Should().BeTrue();
		_mockSettingsService.Verify(x => x.SetAsync(
			"last_sync_time", It.IsAny<long>()), Times.Once);
	}

	[Fact]
	public async Task SynchronizeAsync_ShouldReportProgress_WhenProgressReporterProvided()
	{
		// Arrange
		SetupAuthenticatedUser();
		SetupEmptyPullResponse();
		SetupEmptyPushResponse();

		var progressReports = new List<SyncProgress>();
		var progress = new Progress<SyncProgress>(p => progressReports.Add(p));

		// Act
		await _syncService.SynchronizeAsync(progress);

		// Assert
		progressReports.Should().NotBeEmpty();
		progressReports.Should().Contain(p => p.Stage == SyncStage.Starting);
		progressReports.Should().Contain(p => p.Stage == SyncStage.Completed);
	}

	[Fact]
	public async Task SynchronizeAsync_ShouldHandleCancellation()
	{
		// Arrange
		SetupAuthenticatedUser();
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAsync<OperationCanceledException>(
			async () => await _syncService.SynchronizeAsync(cancellationToken: cts.Token));
	}

	#endregion

	#region PullAsync Tests

	[Fact]
	public async Task PullAsync_ShouldDownloadCollections_WhenCollectionsExist()
	{
		// Arrange
		SetupAuthenticatedUser();

		var collections = new List<object>
		{
			new { id = "col1", title = "Collection 1", description = "Test", schema = "{}", symbology = "{}", itemsCount = 10 },
			new { id = "col2", title = "Collection 2", description = "Test 2", schema = "{}", symbology = "{}", itemsCount = 5 }
		};

		_mockApiClient.Setup(x => x.GetAsync<List<object>>(
			"/api/collections", It.IsAny<string>()))
			.ReturnsAsync(collections);

		_mockApiClient.Setup(x => x.GetAsync<object>(
			It.Is<string>(s => s.Contains("/api/features")), It.IsAny<string>()))
			.ReturnsAsync(new { features = new List<object>() });

		_mockCollectionRepo.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
			.ReturnsAsync((Collection?)null);

		// Act
		var result = await _syncService.PullAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.CollectionsDownloaded.Should().Be(2);
		_mockCollectionRepo.Verify(x => x.InsertAsync(It.IsAny<Collection>()), Times.Exactly(2));
	}

	[Fact]
	public async Task PullAsync_ShouldDownloadFeatures_WhenFeaturesExist()
	{
		// Arrange
		SetupAuthenticatedUser();

		_mockApiClient.Setup(x => x.GetAsync<List<object>>(
			"/api/collections", It.IsAny<string>()))
			.ReturnsAsync(new List<object>());

		var features = new List<object>
		{
			new {
				id = "feat1",
				collectionId = "col1",
				geometry = "{\"type\":\"Point\",\"coordinates\":[0,0]}",
				properties = "{\"name\":\"Feature 1\"}",
				createdAt = 1000L,
				updatedAt = 1000L,
				createdBy = "user1",
				version = 1,
				deleted = false
			}
		};

		_mockApiClient.Setup(x => x.GetAsync<object>(
			It.Is<string>(s => s.Contains("/api/features")), It.IsAny<string>()))
			.ReturnsAsync(new { features = features, totalCount = 1 });

		_mockFeatureRepo.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
			.ReturnsAsync((Feature?)null);

		_mockChangeRepo.Setup(x => x.GetByFeatureIdAsync(It.IsAny<string>()))
			.ReturnsAsync(new List<Change>());

		// Act
		var result = await _syncService.PullAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesDownloaded.Should().Be(1);
		_mockFeatureRepo.Verify(x => x.InsertAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task PullAsync_ShouldUpdateExistingFeatures_WhenServerVersionIsNewer()
	{
		// Arrange
		SetupAuthenticatedUser();

		_mockApiClient.Setup(x => x.GetAsync<List<object>>(
			"/api/collections", It.IsAny<string>()))
			.ReturnsAsync(new List<object>());

		var features = new List<object>
		{
			new {
				id = "feat1",
				collectionId = "col1",
				geometry = "{\"type\":\"Point\",\"coordinates\":[0,0]}",
				properties = "{\"name\":\"Updated Feature\"}",
				createdAt = 1000L,
				updatedAt = 2000L,
				createdBy = "user1",
				version = 2,
				deleted = false
			}
		};

		_mockApiClient.Setup(x => x.GetAsync<object>(
			It.Is<string>(s => s.Contains("/api/features")), It.IsAny<string>()))
			.ReturnsAsync(new { features = features, totalCount = 1 });

		var existingFeature = new Feature
		{
			Id = "feat1",
			CollectionId = "col1",
			Version = 1,
			UpdatedAt = 1000
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(existingFeature);

		_mockChangeRepo.Setup(x => x.GetByFeatureIdAsync("feat1"))
			.ReturnsAsync(new List<Change>());

		// Act
		var result = await _syncService.PullAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesUpdated.Should().Be(1);
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task PullAsync_ShouldDetectConflict_WhenBothLocalAndServerModified()
	{
		// Arrange
		SetupAuthenticatedUser();

		_mockApiClient.Setup(x => x.GetAsync<List<object>>(
			"/api/collections", It.IsAny<string>()))
			.ReturnsAsync(new List<object>());

		var features = new List<object>
		{
			new {
				id = "feat1",
				collectionId = "col1",
				geometry = "{\"type\":\"Point\",\"coordinates\":[0,0]}",
				properties = "{\"name\":\"Server Version\"}",
				createdAt = 1000L,
				updatedAt = 2000L,
				createdBy = "user1",
				version = 2,
				deleted = false
			}
		};

		_mockApiClient.Setup(x => x.GetAsync<object>(
			It.Is<string>(s => s.Contains("/api/features")), It.IsAny<string>()))
			.ReturnsAsync(new { features = features, totalCount = 1 });

		var existingFeature = new Feature
		{
			Id = "feat1",
			CollectionId = "col1",
			Version = 1,
			UpdatedAt = 1500,
			Properties = "{\"name\":\"Local Version\"}"
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(existingFeature);

		// Has pending local changes
		_mockChangeRepo.Setup(x => x.GetByFeatureIdAsync("feat1"))
			.ReturnsAsync(new List<Change> { new Change { FeatureId = "feat1", Synced = 0 } });

		var conflict = new SyncConflict
		{
			FeatureId = "feat1",
			CollectionId = "col1",
			Type = ConflictType.ModifyModify,
			LocalVersion = 1,
			ServerVersion = 2,
			LocalModifiedAt = DateTimeOffset.FromUnixTimeSeconds(1500),
			ServerModifiedAt = DateTimeOffset.FromUnixTimeSeconds(2000)
		};

		_mockConflictService.Setup(x => x.DetectConflictAsync(
			It.IsAny<Feature>(), It.IsAny<Feature>(), null))
			.ReturnsAsync(conflict);

		// Act
		var result = await _syncService.PullAsync();

		// Assert
		result.Success.Should().BeTrue();
		_mockConflictService.Verify(x => x.SaveConflictAsync(It.IsAny<SyncConflict>()), Times.Once);
		_mockFeatureRepo.Verify(x => x.UpdateSyncStatusAsync("feat1", SyncStatus.Conflict), Times.Once);
	}

	[Fact]
	public async Task PullAsync_ShouldDeleteLocalFeatures_WhenDeletedOnServer()
	{
		// Arrange
		SetupAuthenticatedUser();

		_mockApiClient.Setup(x => x.GetAsync<List<object>>(
			"/api/collections", It.IsAny<string>()))
			.ReturnsAsync(new List<object>());

		var features = new List<object>
		{
			new {
				id = "feat1",
				collectionId = "col1",
				geometry = (string?)null,
				properties = (string?)null,
				createdAt = 1000L,
				updatedAt = 2000L,
				createdBy = "user1",
				version = 2,
				deleted = true
			}
		};

		_mockApiClient.Setup(x => x.GetAsync<object>(
			It.Is<string>(s => s.Contains("/api/features")), It.IsAny<string>()))
			.ReturnsAsync(new { features = features, totalCount = 1 });

		var existingFeature = new Feature { Id = "feat1", CollectionId = "col1" };
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(existingFeature);

		// Act
		var result = await _syncService.PullAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesDeleted.Should().Be(1);
		_mockFeatureRepo.Verify(x => x.DeleteAsync("feat1"), Times.Once);
		_mockChangeRepo.Verify(x => x.DeleteByFeatureIdAsync("feat1"), Times.Once);
	}

	#endregion

	#region PushAsync Tests

	[Fact]
	public async Task PushAsync_ShouldReturnSuccess_WhenNoPendingChanges()
	{
		// Arrange
		SetupAuthenticatedUser();
		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(new List<Change>());

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.ChangesSynced.Should().Be(0);
	}

	[Fact]
	public async Task PushAsync_ShouldCreateFeatures_WhenInsertChangesExist()
	{
		// Arrange
		SetupAuthenticatedUser();

		var changes = new List<Change>
		{
			new Change { Id = 1, FeatureId = "feat1", Operation = "Insert", Synced = 0 }
		};

		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(changes);

		var feature = new Feature
		{
			Id = "feat1",
			CollectionId = "col1",
			Properties = "{\"name\":\"New Feature\"}",
			Version = 1
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(feature);

		var responseDto = new
		{
			id = "server_feat1",
			collectionId = "col1",
			properties = "{\"name\":\"New Feature\"}",
			version = 1
		};

		_mockApiClient.Setup(x => x.PostAsync<object>(
			"/api/features", It.IsAny<object>(), It.IsAny<string>()))
			.ReturnsAsync(responseDto);

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesCreated.Should().Be(1);
		result.ChangesSynced.Should().Be(1);
		_mockChangeRepo.Verify(x => x.MarkAsSyncedAsync(1), Times.Once);
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.Is<Feature>(f =>
			f.ServerId == "server_feat1" && f.SyncStatus == "Synced")), Times.Once);
	}

	[Fact]
	public async Task PushAsync_ShouldUpdateFeatures_WhenUpdateChangesExist()
	{
		// Arrange
		SetupAuthenticatedUser();

		var changes = new List<Change>
		{
			new Change { Id = 1, FeatureId = "feat1", Operation = "Update", Synced = 0 }
		};

		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(changes);

		var feature = new Feature
		{
			Id = "feat1",
			ServerId = "server_feat1",
			CollectionId = "col1",
			Properties = "{\"name\":\"Updated Feature\"}",
			Version = 2
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(feature);

		var responseDto = new
		{
			id = "server_feat1",
			collectionId = "col1",
			properties = "{\"name\":\"Updated Feature\"}",
			version = 3
		};

		_mockApiClient.Setup(x => x.PutAsync<object>(
			"/api/features/server_feat1", It.IsAny<object>(), It.IsAny<string>()))
			.ReturnsAsync(responseDto);

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesUpdated.Should().Be(1);
		result.ChangesSynced.Should().Be(1);
		_mockChangeRepo.Verify(x => x.MarkAsSyncedAsync(1), Times.Once);
	}

	[Fact]
	public async Task PushAsync_ShouldDeleteFeatures_WhenDeleteChangesExist()
	{
		// Arrange
		SetupAuthenticatedUser();

		var changes = new List<Change>
		{
			new Change { Id = 1, FeatureId = "feat1", Operation = "Delete", Synced = 0 }
		};

		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(changes);

		var feature = new Feature
		{
			Id = "feat1",
			ServerId = "server_feat1",
			CollectionId = "col1"
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(feature);

		_mockApiClient.Setup(x => x.DeleteAsync(
			"/api/features/server_feat1", It.IsAny<string>()))
			.ReturnsAsync(true);

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesDeleted.Should().Be(1);
		result.ChangesSynced.Should().Be(1);
		_mockChangeRepo.Verify(x => x.MarkAsSyncedAsync(1), Times.Once);
	}

	[Fact]
	public async Task PushAsync_ShouldHandleConflicts_WhenServerReturns409()
	{
		// Arrange
		SetupAuthenticatedUser();

		var changes = new List<Change>
		{
			new Change { Id = 1, FeatureId = "feat1", Operation = "Update", Synced = 0 }
		};

		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(changes);

		var feature = new Feature
		{
			Id = "feat1",
			ServerId = "server_feat1",
			CollectionId = "col1",
			Properties = "{\"name\":\"Local Version\"}",
			Version = 2
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(feature);

		_mockApiClient.Setup(x => x.PutAsync<object>(
			It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()))
			.ThrowsAsync(new ApiException("Conflict detected")
			{
				StatusCode = System.Net.HttpStatusCode.Conflict
			});

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.Conflicts.Should().HaveCount(1);
		result.ChangesSynced.Should().Be(0);
		_mockFeatureRepo.Verify(x => x.UpdateSyncStatusAsync("feat1", SyncStatus.Conflict), Times.Once);
	}

	[Fact]
	public async Task PushAsync_ShouldContinueOnError_WhenSingleFeatureFails()
	{
		// Arrange
		SetupAuthenticatedUser();

		var changes = new List<Change>
		{
			new Change { Id = 1, FeatureId = "feat1", Operation = "Update", Synced = 0 },
			new Change { Id = 2, FeatureId = "feat2", Operation = "Update", Synced = 0 }
		};

		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(changes);

		var feature1 = new Feature { Id = "feat1", ServerId = "server_feat1", CollectionId = "col1", Version = 2 };
		var feature2 = new Feature { Id = "feat2", ServerId = "server_feat2", CollectionId = "col1", Version = 2 };

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1")).ReturnsAsync(feature1);
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat2")).ReturnsAsync(feature2);

		// First update fails
		_mockApiClient.Setup(x => x.PutAsync<object>(
			"/api/features/server_feat1", It.IsAny<object>(), It.IsAny<string>()))
			.ThrowsAsync(new Exception("Network error"));

		// Second update succeeds
		_mockApiClient.Setup(x => x.PutAsync<object>(
			"/api/features/server_feat2", It.IsAny<object>(), It.IsAny<string>()))
			.ReturnsAsync(new { id = "server_feat2", version = 3 });

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Success.Should().BeTrue();
		result.FeaturesUpdated.Should().Be(1);
		result.ChangesSynced.Should().Be(1);
		_mockFeatureRepo.Verify(x => x.UpdateSyncStatusAsync("feat1", SyncStatus.Error), Times.Once);
	}

	#endregion

	#region Helper Tests

	[Fact]
	public async Task GetPendingChangesCountAsync_ShouldReturnCount()
	{
		// Arrange
		_mockChangeRepo.Setup(x => x.GetPendingCountAsync())
			.ReturnsAsync(5);

		// Act
		var count = await _syncService.GetPendingChangesCountAsync();

		// Assert
		count.Should().Be(5);
	}

	[Fact]
	public async Task GetLastSyncTimeAsync_ShouldReturnNull_WhenNeverSynced()
	{
		// Arrange
		_mockSettingsService.Setup(x => x.GetAsync<long>("last_sync_time", 0))
			.ReturnsAsync(0);

		// Act
		var lastSync = await _syncService.GetLastSyncTimeAsync();

		// Assert
		lastSync.Should().BeNull();
	}

	[Fact]
	public async Task GetLastSyncTimeAsync_ShouldReturnTimestamp_WhenPreviouslySynced()
	{
		// Arrange
		var expectedTime = DateTimeOffset.UtcNow.AddHours(-1);
		_mockSettingsService.Setup(x => x.GetAsync<long>("last_sync_time", 0))
			.ReturnsAsync(expectedTime.ToUnixTimeSeconds());

		// Act
		var lastSync = await _syncService.GetLastSyncTimeAsync();

		// Assert
		lastSync.Should().NotBeNull();
		lastSync.Value.ToUnixTimeSeconds().Should().Be(expectedTime.ToUnixTimeSeconds());
	}

	[Fact]
	public async Task ScheduleBackgroundSyncAsync_ShouldSaveSettings()
	{
		// Act
		await _syncService.ScheduleBackgroundSyncAsync(30);

		// Assert
		_mockSettingsService.Verify(x => x.SetAsync("background_sync_interval", 30), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync("background_sync_enabled", true), Times.Once);
	}

	[Fact]
	public async Task CancelBackgroundSyncAsync_ShouldDisableSync()
	{
		// Act
		await _syncService.CancelBackgroundSyncAsync();

		// Assert
		_mockSettingsService.Verify(x => x.SetAsync("background_sync_enabled", false), Times.Once);
	}

	#endregion

	#region Private Helper Methods

	private void SetupAuthenticatedUser()
	{
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync())
			.ReturnsAsync(true);
		_mockAuthService.Setup(x => x.GetAccessTokenAsync())
			.ReturnsAsync("test_token");
	}

	private void SetupEmptyPullResponse()
	{
		_mockApiClient.Setup(x => x.GetAsync<List<object>>(
			"/api/collections", It.IsAny<string>()))
			.ReturnsAsync(new List<object>());

		_mockApiClient.Setup(x => x.GetAsync<object>(
			It.Is<string>(s => s.Contains("/api/features")), It.IsAny<string>()))
			.ReturnsAsync(new { features = new List<object>(), totalCount = 0 });
	}

	private void SetupEmptyPushResponse()
	{
		_mockChangeRepo.Setup(x => x.GetPendingAsync())
			.ReturnsAsync(new List<Change>());
	}

	#endregion
}
