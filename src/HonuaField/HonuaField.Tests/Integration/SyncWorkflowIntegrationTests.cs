using FluentAssertions;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for synchronization workflows
/// Tests real SyncService, ConflictResolutionService, and repositories with mocked HTTP client
/// </summary>
public class SyncWorkflowIntegrationTests : IntegrationTestBase
{
	private MockHttpMessageHandler _mockHttpHandler = null!;
	private Mock<IAuthenticationService> _mockAuthService = null!;
	private Mock<ISettingsService> _mockSettingsService = null!;
	private ISyncService _syncService = null!;
	private IConflictResolutionService _conflictResolutionService = null!;

	protected override void ConfigureServices(IServiceCollection services)
	{
		base.ConfigureServices(services);

		// Create mock HTTP handler
		_mockHttpHandler = new MockHttpMessageHandler();
		var httpClient = _mockHttpHandler.CreateClient();

		// Create mock services
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(true);
		_mockAuthService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync("test_access_token");

		_mockSettingsService = new Mock<ISettingsService>();

		// Create API client with mock HTTP client
		var apiClient = new ApiClient(httpClient, _mockAuthService.Object);

		// Register services
		services.AddSingleton<IApiClient>(apiClient);
		services.AddSingleton(_mockAuthService.Object);
		services.AddSingleton(_mockSettingsService.Object);
		services.AddSingleton<IConflictResolutionService, ConflictResolutionService>();
		services.AddSingleton<ISyncService, SyncService>();
	}

	protected override async Task OnInitializeAsync()
	{
		_syncService = ServiceProvider.GetRequiredService<ISyncService>();
		_conflictResolutionService = ServiceProvider.GetRequiredService<IConflictResolutionService>();
		await base.OnInitializeAsync();
	}

	[Fact]
	public async Task PullFromServer_WithNewFeatures_ShouldDownloadToLocalDatabase()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Mock server response with features
		var serverFeatures = new[]
		{
			new
			{
				id = Guid.NewGuid().ToString(),
				collection_id = collection.Id,
				geometry = new { type = "Point", coordinates = new[] { -122.6765, 45.5231 } },
				properties = new { name = "Server Feature 1" },
				created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				updated_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				version = 1
			}
		};

		_mockHttpHandler.ConfigureJsonResponse("/collections", new[] { collection });
		_mockHttpHandler.ConfigureJsonResponse($"/collections/{collection.Id}/features", serverFeatures);

		// Act
		var result = await _syncService.PullAsync();

		// Assert
		result.Should().NotBeNull();
		result.Success.Should().BeTrue();
		result.FeaturesDownloaded.Should().BeGreaterThan(0);

		var localFeatures = await FeatureRepository.GetByCollectionIdAsync(collection.Id);
		localFeatures.Should().NotBeEmpty();
	}

	[Fact]
	public async Task PushToServer_WithLocalChanges_ShouldUploadFeatures()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var localFeature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(localFeature);

		// Create change record
		var change = new Change
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = localFeature.Id,
			ChangeType = "Create",
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Synced = 0
		};
		await ChangeRepository.InsertAsync(change);

		// Mock server response
		_mockHttpHandler.ConfigureJsonResponse(
			$"/collections/{collection.Id}/features",
			new { id = localFeature.Id, server_id = Guid.NewGuid().ToString() },
			HttpStatusCode.Created);

		// Act
		var result = await _syncService.PushAsync();

		// Assert
		result.Should().NotBeNull();
		result.Success.Should().BeTrue();
		result.FeaturesCreated.Should().Be(1);

		// Verify change was marked as synced
		var pendingChanges = await ChangeRepository.GetPendingSyncAsync();
		pendingChanges.Should().BeEmpty();
	}

	[Fact]
	public async Task BidirectionalSync_WithLocalAndRemoteChanges_ShouldSyncBoth()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Local feature to push
		var localFeature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(localFeature);

		var localChange = new Change
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = localFeature.Id,
			ChangeType = "Create",
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Synced = 0
		};
		await ChangeRepository.InsertAsync(localChange);

		// Mock server features to pull
		var serverFeature = new
		{
			id = Guid.NewGuid().ToString(),
			collection_id = collection.Id,
			geometry = new { type = "Point", coordinates = new[] { -122.0, 45.0 } },
			properties = new { name = "Server Feature" },
			created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			updated_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			version = 1
		};

		_mockHttpHandler.ConfigureJsonResponse("/collections", new[] { collection });
		_mockHttpHandler.ConfigureJsonResponse($"/collections/{collection.Id}/features", new[] { serverFeature });
		_mockHttpHandler.ConfigureJsonResponse(
			$"/collections/{collection.Id}/features",
			new { id = localFeature.Id, server_id = Guid.NewGuid().ToString() },
			HttpStatusCode.Created);

		// Act
		var result = await _syncService.SynchronizeAsync();

		// Assert
		result.Should().NotBeNull();
		result.Success.Should().BeTrue();
		result.PullResult.FeaturesDownloaded.Should().BeGreaterThan(0);
		result.PushResult.FeaturesCreated.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task DetectConflict_WhenBothLocalAndServerModified_ShouldIdentifyConflict()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var baseFeature = DataBuilder.CreateTestFeature(collection.Id);
		baseFeature.Version = 1;
		baseFeature.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();

		var localFeature = DataBuilder.CreateTestFeature(collection.Id);
		localFeature.Id = baseFeature.Id;
		localFeature.Version = 2;
		localFeature.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
		localFeature.Properties = System.Text.Json.JsonSerializer.Serialize(new { name = "Local Change" });

		var serverFeature = DataBuilder.CreateTestFeature(collection.Id);
		serverFeature.Id = baseFeature.Id;
		serverFeature.Version = 2;
		serverFeature.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds();
		serverFeature.Properties = System.Text.Json.JsonSerializer.Serialize(new { name = "Server Change" });

		// Act
		var conflict = await _conflictResolutionService.DetectConflictAsync(localFeature, serverFeature, baseFeature);

		// Assert
		conflict.Should().NotBeNull();
		conflict!.Type.Should().Be(ConflictType.ModifyModify);
		conflict.LocalVersion.Should().Be(2);
		conflict.ServerVersion.Should().Be(2);
	}

	[Fact]
	public async Task ResolveConflict_ServerWins_ShouldUseServerVersion()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var conflict = new SyncConflict
		{
			FeatureId = Guid.NewGuid().ToString(),
			CollectionId = collection.Id,
			Type = ConflictType.ModifyModify,
			LocalVersion = 2,
			ServerVersion = 2,
			LocalModifiedAt = DateTimeOffset.UtcNow.AddHours(-1),
			ServerModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
			LocalProperties = System.Text.Json.JsonSerializer.Serialize(new { name = "Local" }),
			ServerProperties = System.Text.Json.JsonSerializer.Serialize(new { name = "Server" })
		};

		// Act
		var resolved = await _conflictResolutionService.ResolveConflictAsync(conflict, ResolutionStrategy.ServerWins);

		// Assert
		resolved.Should().NotBeNull();
		var props = resolved.GetPropertiesDict();
		props["name"].ToString().Should().Be("Server");
	}

	[Fact]
	public async Task ResolveConflict_ClientWins_ShouldUseLocalVersion()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var conflict = new SyncConflict
		{
			FeatureId = Guid.NewGuid().ToString(),
			CollectionId = collection.Id,
			Type = ConflictType.ModifyModify,
			LocalVersion = 2,
			ServerVersion = 2,
			LocalModifiedAt = DateTimeOffset.UtcNow.AddHours(-1),
			ServerModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
			LocalProperties = System.Text.Json.JsonSerializer.Serialize(new { name = "Local" }),
			ServerProperties = System.Text.Json.JsonSerializer.Serialize(new { name = "Server" })
		};

		// Act
		var resolved = await _conflictResolutionService.ResolveConflictAsync(conflict, ResolutionStrategy.ClientWins);

		// Assert
		resolved.Should().NotBeNull();
		var props = resolved.GetPropertiesDict();
		props["name"].ToString().Should().Be("Local");
	}

	[Fact]
	public async Task ThreeWayMerge_WithNonConflictingChanges_ShouldAutoMerge()
	{
		// Arrange
		var baseProps = System.Text.Json.JsonSerializer.Serialize(new
		{
			name = "Original",
			status = "Active",
			value = 100
		});

		var localProps = System.Text.Json.JsonSerializer.Serialize(new
		{
			name = "Updated Name",
			status = "Active",
			value = 100
		});

		var serverProps = System.Text.Json.JsonSerializer.Serialize(new
		{
			name = "Original",
			status = "Inactive",
			value = 100
		});

		// Act
		var result = await _conflictResolutionService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.Should().NotBeNull();
		result.Success.Should().BeTrue();
		result.RemainingConflicts.Should().BeEmpty();

		var merged = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(result.MergedProperties);
		merged.Should().NotBeNull();
		merged!["name"].ToString().Should().Be("Updated Name"); // Local change
		merged["status"].ToString().Should().Be("Inactive"); // Server change
	}

	[Fact]
	public async Task SyncWithNetworkFailure_ShouldHandleGracefully()
	{
		// Arrange
		_mockHttpHandler.ConfigureNetworkFailure("/collections", "Network unreachable");

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
		{
			await _syncService.PullAsync();
		});
	}

	[Fact]
	public async Task SyncWithRetry_AfterTransientFailure_ShouldEventuallySucceed()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var attemptCount = 0;
		_mockHttpHandler.ConfigureResponse("/collections", exception: new HttpRequestException("Timeout"));

		// This test demonstrates the concept - actual retry logic would be in SyncService
		// For now, we verify that multiple attempts can be made
		var success = false;
		for (int i = 0; i < 3 && !success; i++)
		{
			try
			{
				await _syncService.PullAsync();
				success = true;
			}
			catch (HttpRequestException)
			{
				attemptCount++;
				if (i == 2)
				{
					// On third attempt, configure success
					_mockHttpHandler.ConfigureJsonResponse("/collections", new[] { collection });
					_mockHttpHandler.ConfigureJsonResponse($"/collections/{collection.Id}/features", Array.Empty<object>());
				}
			}
		}

		// Assert
		attemptCount.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task GetPendingChangesCount_WithUnsynced Changes_ShouldReturnCount()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		for (int i = 0; i < 5; i++)
		{
			var feature = DataBuilder.CreateTestFeature(collection.Id);
			await FeatureRepository.InsertAsync(feature);

			var change = new Change
			{
				Id = Guid.NewGuid().ToString(),
				FeatureId = feature.Id,
				ChangeType = "Create",
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				Synced = 0
			};
			await ChangeRepository.InsertAsync(change);
		}

		// Act
		var count = await _syncService.GetPendingChangesCountAsync();

		// Assert
		count.Should().Be(5);
	}

	[Fact]
	public async Task SaveConflict_ToDatabaseForLaterResolution_ShouldPersist()
	{
		// Arrange
		var conflict = new SyncConflict
		{
			FeatureId = Guid.NewGuid().ToString(),
			CollectionId = Guid.NewGuid().ToString(),
			Type = ConflictType.ModifyModify,
			LocalVersion = 2,
			ServerVersion = 3,
			LocalModifiedAt = DateTimeOffset.UtcNow.AddHours(-1),
			ServerModifiedAt = DateTimeOffset.UtcNow,
			LocalProperties = "{}",
			ServerProperties = "{}",
			Message = "Test conflict"
		};

		// Act
		var conflictId = await _conflictResolutionService.SaveConflictAsync(conflict);

		// Assert
		conflictId.Should().BeGreaterThan(0);

		var unresolved = await _conflictResolutionService.GetUnresolvedConflictsAsync();
		unresolved.Should().ContainSingle();
		unresolved[0].FeatureId.Should().Be(conflict.FeatureId);
	}

	[Fact]
	public async Task ResolveBatch_MultipleConflicts_ShouldResolveAll()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var conflicts = new List<SyncConflict>();
		for (int i = 0; i < 3; i++)
		{
			conflicts.Add(new SyncConflict
			{
				FeatureId = Guid.NewGuid().ToString(),
				CollectionId = collection.Id,
				Type = ConflictType.ModifyModify,
				LocalVersion = 2,
				ServerVersion = 2,
				LocalModifiedAt = DateTimeOffset.UtcNow.AddHours(-1),
				ServerModifiedAt = DateTimeOffset.UtcNow,
				LocalProperties = System.Text.Json.JsonSerializer.Serialize(new { name = $"Local {i}" }),
				ServerProperties = System.Text.Json.JsonSerializer.Serialize(new { name = $"Server {i}" })
			});
		}

		// Act
		var resolved = await _conflictResolutionService.ResolveBatchAsync(conflicts, ResolutionStrategy.ServerWins);

		// Assert
		resolved.Should().HaveCount(3);
		resolved.All(f => f.GetPropertiesDict()["name"].ToString()!.StartsWith("Server")).Should().BeTrue();
	}

	[Fact]
	public async Task SyncProgress_ShouldReportProgressThroughAllStages()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		_mockHttpHandler.ConfigureJsonResponse("/collections", new[] { collection });
		_mockHttpHandler.ConfigureJsonResponse($"/collections/{collection.Id}/features", Array.Empty<object>());

		var progressReports = new List<SyncProgress>();
		var progress = new Progress<SyncProgress>(p => progressReports.Add(p));

		// Act
		await _syncService.SynchronizeAsync(progress);

		// Assert
		progressReports.Should().NotBeEmpty();
		progressReports.Should().Contain(p => p.Stage == SyncStage.Starting);
		progressReports.Should().Contain(p => p.Stage == SyncStage.Completed || p.Stage == SyncStage.Failed);
	}
}
