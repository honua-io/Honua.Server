// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using Moq;
using SQLite;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for ConflictResolutionService
/// Tests conflict detection, resolution strategies, and three-way merge
/// </summary>
public class ConflictResolutionServiceTests : IAsyncLifetime
{
	private readonly Mock<IFeatureRepository> _mockFeatureRepo;
	private readonly Mock<IDatabaseService> _mockDatabaseService;
	private readonly HonuaFieldDatabase _testDatabase;
	private readonly ConflictResolutionService _conflictService;
	private readonly string _testDbPath;

	public ConflictResolutionServiceTests()
	{
		_mockFeatureRepo = new Mock<IFeatureRepository>();
		_mockDatabaseService = new Mock<IDatabaseService>();

		// Create test database
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_conflicts_{Guid.NewGuid()}.db");
		_testDatabase = new HonuaFieldDatabase(_testDbPath);

		_mockDatabaseService.Setup(x => x.GetDatabase())
			.Returns(_testDatabase);

		_conflictService = new ConflictResolutionService(
			_mockFeatureRepo.Object,
			_mockDatabaseService.Object);
	}

	public async Task InitializeAsync()
	{
		// Initialize database tables
		await _testDatabase.InitializeAsync();
	}

	public async Task DisposeAsync()
	{
		// Clean up test database
		if (File.Exists(_testDbPath))
		{
			File.Delete(_testDbPath);
		}
	}

	#region DetectConflictAsync Tests

	[Fact]
	public async Task DetectConflictAsync_ShouldReturnNull_WhenVersionsMatch()
	{
		// Arrange
		var localFeature = CreateTestFeature("feat1", version: 1);
		var serverFeature = CreateTestFeature("feat1", version: 1);

		// Act
		var conflict = await _conflictService.DetectConflictAsync(localFeature, serverFeature);

		// Assert
		conflict.Should().BeNull();
	}

	[Fact]
	public async Task DetectConflictAsync_ShouldReturnNull_WhenLocalIsOlderAndUnmodified()
	{
		// Arrange
		var localFeature = CreateTestFeature("feat1", version: 1, updatedAt: 1000);
		var serverFeature = CreateTestFeature("feat1", version: 2, updatedAt: 2000);

		// Act
		var conflict = await _conflictService.DetectConflictAsync(localFeature, serverFeature);

		// Assert
		conflict.Should().BeNull();
	}

	[Fact]
	public async Task DetectConflictAsync_ShouldReturnConflict_WhenBothModified()
	{
		// Arrange
		var localFeature = CreateTestFeature("feat1", version: 1, updatedAt: 1500,
			properties: "{\"name\":\"Local\"}", syncStatus: SyncStatus.Pending);
		var serverFeature = CreateTestFeature("feat1", version: 2, updatedAt: 2000,
			properties: "{\"name\":\"Server\"}");

		// Act
		var conflict = await _conflictService.DetectConflictAsync(localFeature, serverFeature);

		// Assert
		conflict.Should().NotBeNull();
		conflict!.Type.Should().Be(ConflictType.ModifyModify);
		conflict.LocalVersion.Should().Be(1);
		conflict.ServerVersion.Should().Be(2);
		conflict.LocalProperties.Should().Be("{\"name\":\"Local\"}");
		conflict.ServerProperties.Should().Be("{\"name\":\"Server\"}");
	}

	#endregion

	#region ResolveConflictAsync Tests

	[Fact]
	public async Task ResolveConflictAsync_ShouldUseServerData_WhenServerWinsStrategy()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1",
			localProps: "{\"name\":\"Local\"}",
			serverProps: "{\"name\":\"Server\"}",
			localVersion: 1,
			serverVersion: 2);

		var localFeature = CreateTestFeature("feat1", version: 1);
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(localFeature);

		// Act
		var resolved = await _conflictService.ResolveConflictAsync(conflict, ResolutionStrategy.ServerWins);

		// Assert
		resolved.Properties.Should().Be("{\"name\":\"Server\"}");
		resolved.Version.Should().Be(2);
		resolved.SyncStatus.Should().Be(SyncStatus.Synced.ToString());
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task ResolveConflictAsync_ShouldUseLocalData_WhenClientWinsStrategy()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1",
			localProps: "{\"name\":\"Local\"}",
			serverProps: "{\"name\":\"Server\"}",
			localVersion: 1,
			serverVersion: 2);

		var localFeature = CreateTestFeature("feat1", version: 1,
			properties: "{\"name\":\"Local\"}");
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(localFeature);

		// Act
		var resolved = await _conflictService.ResolveConflictAsync(conflict, ResolutionStrategy.ClientWins);

		// Assert
		resolved.Properties.Should().Be("{\"name\":\"Local\"}");
		resolved.Version.Should().Be(3); // Max(1, 2) + 1
		resolved.SyncStatus.Should().Be(SyncStatus.Pending.ToString());
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task ResolveConflictAsync_ShouldAutoMerge_WhenAutoMergeStrategy()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1",
			localProps: "{\"name\":\"Local\",\"description\":\"Test\"}",
			serverProps: "{\"name\":\"Local\",\"count\":5}",
			localVersion: 1,
			serverVersion: 2);

		var localFeature = CreateTestFeature("feat1", version: 1,
			properties: "{\"name\":\"Local\",\"description\":\"Test\"}");
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(localFeature);

		// Act
		var resolved = await _conflictService.ResolveConflictAsync(conflict, ResolutionStrategy.AutoMerge);

		// Assert
		resolved.Should().NotBeNull();
		resolved.SyncStatus.Should().BeOneOf(SyncStatus.Pending.ToString(), SyncStatus.Synced.ToString());
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task ResolveConflictAsync_ShouldThrow_WhenManualStrategyWithoutMergedProperties()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");
		var localFeature = CreateTestFeature("feat1");
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(localFeature);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await _conflictService.ResolveConflictAsync(conflict, ResolutionStrategy.Manual));
	}

	[Fact]
	public async Task ResolveConflictAsync_ShouldThrow_WhenFeatureNotFound()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync((Feature?)null);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await _conflictService.ResolveConflictAsync(conflict, ResolutionStrategy.ServerWins));
	}

	#endregion

	#region ResolveConflictWithCustomMergeAsync Tests

	[Fact]
	public async Task ResolveConflictWithCustomMergeAsync_ShouldUseMergedProperties()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");
		var localFeature = CreateTestFeature("feat1", version: 1);
		var mergedProperties = "{\"name\":\"Merged\",\"value\":42}";

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(localFeature);

		// Act
		var resolved = await _conflictService.ResolveConflictWithCustomMergeAsync(conflict, mergedProperties);

		// Assert
		resolved.Properties.Should().Be(mergedProperties);
		resolved.Version.Should().BeGreaterThan(1);
		resolved.SyncStatus.Should().Be(SyncStatus.Pending.ToString());
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Once);
	}

	[Fact]
	public async Task ResolveConflictWithCustomMergeAsync_ShouldThrow_WhenInvalidJson()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");
		var localFeature = CreateTestFeature("feat1");
		var invalidJson = "{invalid json}";

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(localFeature);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			async () => await _conflictService.ResolveConflictWithCustomMergeAsync(conflict, invalidJson));
	}

	#endregion

	#region ThreeWayMergeAsync Tests

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldMergeSuccessfully_WhenNoConflicts()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\",\"count\":10}";
		var localProps = "{\"name\":\"Base\",\"count\":15}"; // Only count changed locally
		var serverProps = "{\"name\":\"Server\",\"count\":10}"; // Only name changed on server

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.Success.Should().BeTrue();
		result.MergedProperties.Should().Contain("\"name\":\"Server\"");
		result.MergedProperties.Should().Contain("\"count\":15");
		result.RemainingConflicts.Should().BeEmpty();
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldDetectConflicts_WhenSamePropertyModifiedDifferently()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\"}";
		var localProps = "{\"name\":\"Local\"}";
		var serverProps = "{\"name\":\"Server\"}";

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.RemainingConflicts.Should().HaveCount(1);
		result.RemainingConflicts[0].PropertyName.Should().Be("name");
		result.RemainingConflicts[0].LocalValue.Should().Be("Local");
		result.RemainingConflicts[0].ServerValue.Should().Be("Server");
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldAcceptLocalChanges_WhenServerUnchanged()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\",\"count\":10}";
		var localProps = "{\"name\":\"Local\",\"count\":10}"; // name changed locally
		var serverProps = "{\"name\":\"Base\",\"count\":10}"; // unchanged

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.Success.Should().BeTrue();
		result.MergedProperties.Should().Contain("\"name\":\"Local\"");
		result.RemainingConflicts.Should().BeEmpty();
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldAcceptServerChanges_WhenLocalUnchanged()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\",\"count\":10}";
		var localProps = "{\"name\":\"Base\",\"count\":10}"; // unchanged
		var serverProps = "{\"name\":\"Base\",\"count\":20}"; // count changed on server

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.Success.Should().BeTrue();
		result.MergedProperties.Should().Contain("\"count\":20");
		result.RemainingConflicts.Should().BeEmpty();
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldHandleAddedProperties()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\"}";
		var localProps = "{\"name\":\"Base\",\"localProp\":\"local\"}"; // added locally
		var serverProps = "{\"name\":\"Base\",\"serverProp\":\"server\"}"; // added on server

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.MergedProperties.Should().Contain("\"localProp\":\"local\"");
		result.MergedProperties.Should().Contain("\"serverProp\":\"server\"");
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldHandleRemovedProperties()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\",\"prop1\":\"value1\",\"prop2\":\"value2\"}";
		var localProps = "{\"name\":\"Base\",\"prop2\":\"value2\"}"; // prop1 removed locally
		var serverProps = "{\"name\":\"Base\",\"prop1\":\"value1\"}"; // prop2 removed on server

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.MergedProperties.Should().NotContain("prop1");
		result.MergedProperties.Should().NotContain("prop2");
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldDetectModifyDeleteConflict()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\",\"status\":\"active\"}";
		var localProps = "{\"name\":\"Base\",\"status\":\"modified\"}"; // modified locally
		var serverProps = "{\"name\":\"Base\"}"; // deleted on server

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.RemainingConflicts.Should().Contain(c =>
			c.PropertyName == "status" && c.ServerValue == null);
	}

	[Fact]
	public async Task ThreeWayMergeAsync_ShouldAcceptSameChanges()
	{
		// Arrange
		var baseProps = "{\"name\":\"Base\"}";
		var localProps = "{\"name\":\"Updated\"}"; // same change
		var serverProps = "{\"name\":\"Updated\"}"; // same change

		// Act
		var result = await _conflictService.ThreeWayMergeAsync(baseProps, localProps, serverProps);

		// Assert
		result.Success.Should().BeTrue();
		result.MergedProperties.Should().Contain("\"name\":\"Updated\"");
		result.RemainingConflicts.Should().BeEmpty();
	}

	#endregion

	#region ResolveBatchAsync Tests

	[Fact]
	public async Task ResolveBatchAsync_ShouldResolveMultipleConflicts()
	{
		// Arrange
		var conflicts = new List<SyncConflict>
		{
			CreateTestConflict("feat1"),
			CreateTestConflict("feat2")
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ReturnsAsync(CreateTestFeature("feat1"));
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat2"))
			.ReturnsAsync(CreateTestFeature("feat2"));

		// Act
		var resolved = await _conflictService.ResolveBatchAsync(conflicts, ResolutionStrategy.ServerWins);

		// Assert
		resolved.Should().HaveCount(2);
		_mockFeatureRepo.Verify(x => x.UpdateAsync(It.IsAny<Feature>()), Times.Exactly(2));
	}

	[Fact]
	public async Task ResolveBatchAsync_ShouldContinueOnError()
	{
		// Arrange
		var conflicts = new List<SyncConflict>
		{
			CreateTestConflict("feat1"),
			CreateTestConflict("feat2")
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat1"))
			.ThrowsAsync(new Exception("Database error"));
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feat2"))
			.ReturnsAsync(CreateTestFeature("feat2"));

		// Act
		var resolved = await _conflictService.ResolveBatchAsync(conflicts, ResolutionStrategy.ServerWins);

		// Assert
		resolved.Should().HaveCount(1);
		resolved[0].Id.Should().Be("feat2");
	}

	#endregion

	#region Conflict Persistence Tests

	[Fact]
	public async Task SaveConflictAsync_ShouldSaveNewConflict()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");

		// Act
		var conflictId = await _conflictService.SaveConflictAsync(conflict);

		// Assert
		conflictId.Should().BeGreaterThan(0);

		var unresolved = await _conflictService.GetUnresolvedConflictsAsync();
		unresolved.Should().HaveCount(1);
		unresolved[0].FeatureId.Should().Be("feat1");
	}

	[Fact]
	public async Task SaveConflictAsync_ShouldUpdateExistingConflict()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1", localVersion: 1, serverVersion: 2);
		var firstId = await _conflictService.SaveConflictAsync(conflict);

		var updatedConflict = CreateTestConflict("feat1", localVersion: 2, serverVersion: 3);

		// Act
		var secondId = await _conflictService.SaveConflictAsync(updatedConflict);

		// Assert
		firstId.Should().Be(secondId);

		var unresolved = await _conflictService.GetUnresolvedConflictsAsync();
		unresolved.Should().HaveCount(1);
		unresolved[0].ServerVersion.Should().Be(3);
	}

	[Fact]
	public async Task GetUnresolvedConflictsAsync_ShouldReturnOnlyUnresolved()
	{
		// Arrange
		var conflict1 = CreateTestConflict("feat1");
		var conflict2 = CreateTestConflict("feat2");

		var id1 = await _conflictService.SaveConflictAsync(conflict1);
		await _conflictService.SaveConflictAsync(conflict2);

		// Resolve first conflict
		await _conflictService.MarkConflictResolvedAsync(id1, ResolutionStrategy.ServerWins);

		// Act
		var unresolved = await _conflictService.GetUnresolvedConflictsAsync();

		// Assert
		unresolved.Should().HaveCount(1);
		unresolved[0].FeatureId.Should().Be("feat2");
	}

	[Fact]
	public async Task MarkConflictResolvedAsync_ShouldUpdateResolvedStatus()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");
		var conflictId = await _conflictService.SaveConflictAsync(conflict);

		// Act
		await _conflictService.MarkConflictResolvedAsync(conflictId, ResolutionStrategy.ClientWins);

		// Assert
		var unresolved = await _conflictService.GetUnresolvedConflictsAsync();
		unresolved.Should().BeEmpty();
	}

	[Fact]
	public async Task CleanupResolvedConflictsAsync_ShouldDeleteOldResolvedConflicts()
	{
		// Arrange
		var conflict = CreateTestConflict("feat1");
		var conflictId = await _conflictService.SaveConflictAsync(conflict);
		await _conflictService.MarkConflictResolvedAsync(conflictId, ResolutionStrategy.ServerWins);

		// Manually set resolved time to 60 days ago
		var conn = _testDatabase.GetConnection();
		var record = await conn.Table<ConflictRecord>()
			.Where(c => c.Id == conflictId)
			.FirstOrDefaultAsync();
		record!.ResolvedAt = DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeSeconds();
		await conn.UpdateAsync(record);

		// Act
		var deleted = await _conflictService.CleanupResolvedConflictsAsync(30);

		// Assert
		deleted.Should().Be(1);
	}

	#endregion

	#region Helper Methods

	private Feature CreateTestFeature(
		string id,
		int version = 1,
		long updatedAt = 1000,
		string properties = "{}",
		SyncStatus syncStatus = SyncStatus.Synced)
	{
		return new Feature
		{
			Id = id,
			CollectionId = "col1",
			Version = version,
			UpdatedAt = updatedAt,
			Properties = properties,
			SyncStatus = syncStatus.ToString(),
			CreatedAt = 1000,
			CreatedBy = "user1"
		};
	}

	private SyncConflict CreateTestConflict(
		string featureId,
		string localProps = "{}",
		string serverProps = "{}",
		int localVersion = 1,
		int serverVersion = 2)
	{
		return new SyncConflict
		{
			FeatureId = featureId,
			CollectionId = "col1",
			Type = ConflictType.ModifyModify,
			LocalVersion = localVersion,
			ServerVersion = serverVersion,
			LocalModifiedAt = DateTimeOffset.FromUnixTimeSeconds(1500),
			ServerModifiedAt = DateTimeOffset.FromUnixTimeSeconds(2000),
			LocalProperties = localProps,
			ServerProperties = serverProps,
			Message = "Test conflict"
		};
	}

	#endregion
}
