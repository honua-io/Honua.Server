// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using Xunit;
using FluentAssertions;

namespace HonuaField.Tests.Data.Repositories;

/// <summary>
/// Unit tests for CollectionRepository
/// </summary>
public class CollectionRepositoryTests : IAsyncLifetime
{
	private HonuaFieldDatabase? _database;
	private CollectionRepository? _repository;
	private readonly string _testDbPath;

	public CollectionRepositoryTests()
	{
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
	}

	public async Task InitializeAsync()
	{
		_database = new HonuaFieldDatabase(_testDbPath);
		await _database.InitializeAsync();
		_repository = new CollectionRepository(_database);
	}

	public async Task DisposeAsync()
	{
		if (_database != null)
			await _database.CloseAsync();

		if (File.Exists(_testDbPath))
			File.Delete(_testDbPath);
	}

	[Fact]
	public async Task InsertAsync_ShouldInsertCollection_AndReturnId()
	{
		// Arrange
		var collection = new Collection
		{
			Title = "Test Collection",
			Schema = "{}",
			Symbology = "{}"
		};

		// Act
		var id = await _repository!.InsertAsync(collection);

		// Assert
		id.Should().NotBeNullOrEmpty();
		collection.Id.Should().Be(id);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnCollection_WhenExists()
	{
		// Arrange
		var collection = new Collection { Title = "Test", Schema = "{}", Symbology = "{}" };
		var id = await _repository!.InsertAsync(collection);

		// Act
		var retrieved = await _repository.GetByIdAsync(id);

		// Assert
		retrieved.Should().NotBeNull();
		retrieved!.Title.Should().Be("Test");
	}

	[Fact]
	public async Task UpdateAsync_ShouldUpdateCollection()
	{
		// Arrange
		var collection = new Collection { Title = "Original", Schema = "{}", Symbology = "{}" };
		await _repository!.InsertAsync(collection);

		// Act
		collection.Title = "Updated";
		await _repository.UpdateAsync(collection);

		// Assert
		var updated = await _repository.GetByIdAsync(collection.Id);
		updated!.Title.Should().Be("Updated");
	}

	[Fact]
	public async Task DeleteAsync_ShouldDeleteCollection()
	{
		// Arrange
		var collection = new Collection { Title = "Test", Schema = "{}", Symbology = "{}" };
		var id = await _repository!.InsertAsync(collection);

		// Act
		await _repository.DeleteAsync(id);

		// Assert
		var deleted = await _repository.GetByIdAsync(id);
		deleted.Should().BeNull();
	}

	[Fact]
	public async Task GetAllAsync_ShouldReturnAllCollections()
	{
		// Arrange
		await _repository!.InsertAsync(new Collection { Title = "Col1", Schema = "{}", Symbology = "{}" });
		await _repository.InsertAsync(new Collection { Title = "Col2", Schema = "{}", Symbology = "{}" });

		// Act
		var collections = await _repository.GetAllAsync();

		// Assert
		collections.Should().HaveCount(2);
	}

	[Fact]
	public async Task UpdateItemsCountAsync_ShouldUpdateCount()
	{
		// Arrange
		var collection = new Collection { Title = "Test", Schema = "{}", Symbology = "{}" };
		var id = await _repository!.InsertAsync(collection);

		// Act
		await _repository.UpdateItemsCountAsync(id, 42);

		// Assert
		var updated = await _repository.GetByIdAsync(id);
		updated!.ItemsCount.Should().Be(42);
	}

	[Fact]
	public async Task IncrementItemsCountAsync_ShouldIncrementCount()
	{
		// Arrange
		var collection = new Collection { Title = "Test", Schema = "{}", Symbology = "{}", ItemsCount = 10 };
		var id = await _repository!.InsertAsync(collection);

		// Act
		await _repository.IncrementItemsCountAsync(id, 5);

		// Assert
		var updated = await _repository.GetByIdAsync(id);
		updated!.ItemsCount.Should().Be(15);
	}

	[Fact]
	public async Task UpdateExtentAsync_ShouldUpdateExtent()
	{
		// Arrange
		var collection = new Collection { Title = "Test", Schema = "{}", Symbology = "{}" };
		var id = await _repository!.InsertAsync(collection);
		var extentJson = "{\"min_x\": -180, \"max_x\": 180}";

		// Act
		await _repository.UpdateExtentAsync(id, extentJson);

		// Assert
		var updated = await _repository.GetByIdAsync(id);
		updated!.Extent.Should().Be(extentJson);
	}

	[Fact]
	public async Task InsertBatchAsync_ShouldInsertMultipleCollections()
	{
		// Arrange
		var collections = new List<Collection>
		{
			new() { Title = "Col1", Schema = "{}", Symbology = "{}" },
			new() { Title = "Col2", Schema = "{}", Symbology = "{}" },
			new() { Title = "Col3", Schema = "{}", Symbology = "{}" }
		};

		// Act
		var result = await _repository!.InsertBatchAsync(collections);

		// Assert
		result.Should().Be(3);
		var all = await _repository.GetAllAsync();
		all.Should().HaveCount(3);
	}

	[Fact]
	public async Task GetCountAsync_ShouldReturnTotalCount()
	{
		// Arrange
		await _repository!.InsertAsync(new Collection { Title = "Col1", Schema = "{}", Symbology = "{}" });
		await _repository.InsertAsync(new Collection { Title = "Col2", Schema = "{}", Symbology = "{}" });

		// Act
		var count = await _repository.GetCountAsync();

		// Assert
		count.Should().Be(2);
	}
}
