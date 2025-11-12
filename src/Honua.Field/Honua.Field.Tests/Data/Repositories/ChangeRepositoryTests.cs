// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using Xunit;
using FluentAssertions;

namespace HonuaField.Tests.Data.Repositories;

/// <summary>
/// Unit tests for ChangeRepository
/// </summary>
public class ChangeRepositoryTests : IAsyncLifetime
{
	private HonuaFieldDatabase? _database;
	private ChangeRepository? _repository;
	private readonly string _testDbPath;

	public ChangeRepositoryTests()
	{
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
	}

	public async Task InitializeAsync()
	{
		_database = new HonuaFieldDatabase(_testDbPath);
		await _database.InitializeAsync();
		_repository = new ChangeRepository(_database);
	}

	public async Task DisposeAsync()
	{
		if (_database != null)
			await _database.CloseAsync();

		if (File.Exists(_testDbPath))
			File.Delete(_testDbPath);
	}

	[Fact]
	public async Task InsertAsync_ShouldInsertChange_AndAutoIncrementId()
	{
		// Arrange
		var change = new Change
		{
			FeatureId = "feature1",
			Operation = ChangeOperation.Insert.ToString()
		};

		// Act
		var result = await _repository!.InsertAsync(change);

		// Assert
		result.Should().BeGreaterThan(0);
		change.Id.Should().BeGreaterThan(0);
		change.Timestamp.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task GetByFeatureIdAsync_ShouldReturnChangesForFeature()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "feature1",
			Operation = ChangeOperation.Insert.ToString()
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "feature1",
			Operation = ChangeOperation.Update.ToString()
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "feature2",
			Operation = ChangeOperation.Insert.ToString()
		});

		// Act
		var changes = await _repository.GetByFeatureIdAsync("feature1");

		// Assert
		changes.Should().HaveCount(2);
		changes.Should().AllSatisfy(c => c.FeatureId.Should().Be("feature1"));
	}

	[Fact]
	public async Task GetPendingAsync_ShouldReturnUnsyncedChanges()
	{
		// Arrange
		var change1 = new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 0
		};
		await _repository!.InsertAsync(change1);

		var change2 = new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Update.ToString(),
			Synced = 1
		};
		await _repository.InsertAsync(change2);

		var change3 = new Change
		{
			FeatureId = "f3",
			Operation = ChangeOperation.Delete.ToString(),
			Synced = 0
		};
		await _repository.InsertAsync(change3);

		// Act
		var pending = await _repository.GetPendingAsync();

		// Assert
		pending.Should().HaveCount(2);
		pending.Should().AllSatisfy(c => c.Synced.Should().Be(0));
	}

	[Fact]
	public async Task GetSyncedAsync_ShouldReturnSyncedChanges()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 0
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Update.ToString(),
			Synced = 1
		});

		// Act
		var synced = await _repository.GetSyncedAsync();

		// Assert
		synced.Should().HaveCount(1);
		synced[0].Synced.Should().Be(1);
	}

	[Fact]
	public async Task MarkAsSyncedAsync_ShouldUpdateSyncedFlag()
	{
		// Arrange
		var change = new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 0
		};
		await _repository!.InsertAsync(change);

		// Act
		await _repository.MarkAsSyncedAsync(change.Id);

		// Assert
		var updated = await _repository.GetByIdAsync(change.Id);
		updated!.Synced.Should().Be(1);
	}

	[Fact]
	public async Task MarkBatchAsSyncedAsync_ShouldUpdateMultipleChanges()
	{
		// Arrange
		var change1 = new Change { FeatureId = "f1", Operation = ChangeOperation.Insert.ToString() };
		await _repository!.InsertAsync(change1);

		var change2 = new Change { FeatureId = "f2", Operation = ChangeOperation.Update.ToString() };
		await _repository.InsertAsync(change2);

		// Act
		await _repository.MarkBatchAsSyncedAsync(new List<int> { change1.Id, change2.Id });

		// Assert
		var updated1 = await _repository.GetByIdAsync(change1.Id);
		var updated2 = await _repository.GetByIdAsync(change2.Id);
		updated1!.Synced.Should().Be(1);
		updated2!.Synced.Should().Be(1);
	}

	[Fact]
	public async Task GetByOperationAsync_ShouldReturnChangesByOperation()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString()
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Update.ToString()
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f3",
			Operation = ChangeOperation.Insert.ToString()
		});

		// Act
		var inserts = await _repository.GetByOperationAsync(ChangeOperation.Insert);

		// Assert
		inserts.Should().HaveCount(2);
		inserts.Should().AllSatisfy(c => c.Operation.Should().Be(ChangeOperation.Insert.ToString()));
	}

	[Fact]
	public async Task GetPendingByOperationAsync_ShouldReturnPendingChangesByOperation()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 0
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 1
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f3",
			Operation = ChangeOperation.Update.ToString(),
			Synced = 0
		});

		// Act
		var pendingInserts = await _repository.GetPendingByOperationAsync(ChangeOperation.Insert);

		// Assert
		pendingInserts.Should().HaveCount(1);
		pendingInserts[0].Operation.Should().Be(ChangeOperation.Insert.ToString());
		pendingInserts[0].Synced.Should().Be(0);
	}

	[Fact]
	public async Task DeleteByFeatureIdAsync_ShouldDeleteAllChangesForFeature()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString()
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Update.ToString()
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Insert.ToString()
		});

		// Act
		var result = await _repository.DeleteByFeatureIdAsync("f1");

		// Assert
		result.Should().Be(2);
		var remaining = await _repository.GetByFeatureIdAsync("f1");
		remaining.Should().BeEmpty();
	}

	[Fact]
	public async Task ClearSyncedAsync_ShouldDeleteAllSyncedChanges()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 1
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Update.ToString(),
			Synced = 1
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f3",
			Operation = ChangeOperation.Delete.ToString(),
			Synced = 0
		});

		// Act
		var result = await _repository.ClearSyncedAsync();

		// Assert
		result.Should().Be(2);
		var remaining = await _repository.GetAllAsync();
		remaining.Should().HaveCount(1);
		remaining[0].Synced.Should().Be(0);
	}

	[Fact]
	public async Task GetPendingCountAsync_ShouldReturnCountOfPendingChanges()
	{
		// Arrange
		await _repository!.InsertAsync(new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Synced = 0
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f2",
			Operation = ChangeOperation.Update.ToString(),
			Synced = 0
		});
		await _repository.InsertAsync(new Change
		{
			FeatureId = "f3",
			Operation = ChangeOperation.Delete.ToString(),
			Synced = 1
		});

		// Act
		var count = await _repository.GetPendingCountAsync();

		// Assert
		count.Should().Be(2);
	}

	[Fact]
	public async Task GetLatestByFeatureIdAsync_ShouldReturnMostRecentChange()
	{
		// Arrange
		await Task.Delay(10); // Ensure timestamps are different
		var change1 = new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Insert.ToString(),
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};
		await _repository!.InsertAsync(change1);

		await Task.Delay(10);
		var change2 = new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Update.ToString(),
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};
		await _repository.InsertAsync(change2);

		await Task.Delay(10);
		var change3 = new Change
		{
			FeatureId = "f1",
			Operation = ChangeOperation.Delete.ToString(),
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};
		await _repository.InsertAsync(change3);

		// Act
		var latest = await _repository.GetLatestByFeatureIdAsync("f1");

		// Assert
		latest.Should().NotBeNull();
		latest!.Operation.Should().Be(ChangeOperation.Delete.ToString());
		latest.Timestamp.Should().BeGreaterOrEqualTo(change3.Timestamp);
	}

	[Fact]
	public async Task InsertBatchAsync_ShouldInsertMultipleChanges()
	{
		// Arrange
		var changes = new List<Change>
		{
			new() { FeatureId = "f1", Operation = ChangeOperation.Insert.ToString() },
			new() { FeatureId = "f2", Operation = ChangeOperation.Update.ToString() },
			new() { FeatureId = "f3", Operation = ChangeOperation.Delete.ToString() }
		};

		// Act
		var result = await _repository!.InsertBatchAsync(changes);

		// Assert
		result.Should().Be(3);
		var all = await _repository.GetAllAsync();
		all.Should().HaveCount(3);
	}
}
