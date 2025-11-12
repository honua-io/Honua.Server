// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace HonuaField.Tests.Data.Repositories;

/// <summary>
/// Unit tests for MapRepository
/// Tests CRUD operations and query methods
/// </summary>
public class MapRepositoryTests : IAsyncLifetime
{
	private HonuaFieldDatabase? _database;
	private MapRepository? _mapRepository;
	private readonly string _testDbPath;

	public MapRepositoryTests()
	{
		// Create unique database path for each test run
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
	}

	public async Task InitializeAsync()
	{
		// Initialize database and repository
		_database = new HonuaFieldDatabase(_testDbPath);
		await _database.InitializeAsync();

		_mapRepository = new MapRepository(_database);
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
	public async Task InsertAsync_ShouldInsertMap_AndReturnId()
	{
		// Arrange
		var map = CreateTestMap("Test Map", -122.5, 37.7, -122.3, 37.8);

		// Act
		var id = await _mapRepository!.InsertAsync(map);

		// Assert
		id.Should().NotBeNullOrEmpty();
		map.Id.Should().Be(id);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnMap_WhenExists()
	{
		// Arrange
		var map = CreateTestMap("Test Map", -122.5, 37.7, -122.3, 37.8);
		var id = await _mapRepository!.InsertAsync(map);

		// Act
		var retrieved = await _mapRepository.GetByIdAsync(id);

		// Assert
		retrieved.Should().NotBeNull();
		retrieved!.Id.Should().Be(id);
		retrieved.Title.Should().Be("Test Map");
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
	{
		// Act
		var retrieved = await _mapRepository!.GetByIdAsync("nonexistent");

		// Assert
		retrieved.Should().BeNull();
	}

	[Fact]
	public async Task GetAllAsync_ShouldReturnAllMaps()
	{
		// Arrange
		var map1 = CreateTestMap("Map 1", -122.5, 37.7, -122.3, 37.8);
		var map2 = CreateTestMap("Map 2", -121.5, 38.7, -121.3, 38.8);
		await _mapRepository!.InsertAsync(map1);
		await _mapRepository.InsertAsync(map2);

		// Act
		var maps = await _mapRepository.GetAllAsync();

		// Assert
		maps.Should().HaveCount(2);
		maps.Should().Contain(m => m.Title == "Map 1");
		maps.Should().Contain(m => m.Title == "Map 2");
	}

	[Fact]
	public async Task UpdateAsync_ShouldUpdateMap()
	{
		// Arrange
		var map = CreateTestMap("Original Title", -122.5, 37.7, -122.3, 37.8);
		await _mapRepository!.InsertAsync(map);

		// Act
		map.Title = "Updated Title";
		var result = await _mapRepository.UpdateAsync(map);

		// Assert
		result.Should().Be(1);
		var retrieved = await _mapRepository.GetByIdAsync(map.Id);
		retrieved!.Title.Should().Be("Updated Title");
	}

	[Fact]
	public async Task DeleteAsync_ShouldDeleteMap()
	{
		// Arrange
		var map = CreateTestMap("Test Map", -122.5, 37.7, -122.3, 37.8);
		var id = await _mapRepository!.InsertAsync(map);

		// Act
		var result = await _mapRepository.DeleteAsync(id);

		// Assert
		result.Should().Be(1);
		var retrieved = await _mapRepository.GetByIdAsync(id);
		retrieved.Should().BeNull();
	}

	#endregion

	#region Query Tests

	[Fact]
	public async Task GetDownloadedMapsAsync_ShouldReturnOnlyDownloadedMaps()
	{
		// Arrange
		var map1 = CreateTestMap("Downloaded Map", -122.5, 37.7, -122.3, 37.8);
		map1.DownloadSize = 1000000;
		var map2 = CreateTestMap("Online Map", -121.5, 38.7, -121.3, 38.8);
		map2.DownloadSize = 0;

		await _mapRepository!.InsertAsync(map1);
		await _mapRepository.InsertAsync(map2);

		// Act
		var downloadedMaps = await _mapRepository.GetDownloadedMapsAsync();

		// Assert
		downloadedMaps.Should().HaveCount(1);
		downloadedMaps[0].Title.Should().Be("Downloaded Map");
	}

	[Fact]
	public async Task GetByBoundsAsync_ShouldReturnMapsInBounds()
	{
		// Arrange
		// Map fully within query bounds
		var map1 = CreateTestMap("Inside Map", -122.4, 37.75, -122.35, 37.78);
		// Map overlapping query bounds
		var map2 = CreateTestMap("Overlapping Map", -122.6, 37.6, -122.2, 37.9);
		// Map outside query bounds
		var map3 = CreateTestMap("Outside Map", -121.0, 38.0, -120.8, 38.2);

		await _mapRepository!.InsertAsync(map1);
		await _mapRepository.InsertAsync(map2);
		await _mapRepository.InsertAsync(map3);

		// Act
		var mapsInBounds = await _mapRepository.GetByBoundsAsync(-122.5, 37.7, -122.3, 37.8);

		// Assert
		mapsInBounds.Should().HaveCount(2);
		mapsInBounds.Should().Contain(m => m.Title == "Inside Map");
		mapsInBounds.Should().Contain(m => m.Title == "Overlapping Map");
		mapsInBounds.Should().NotContain(m => m.Title == "Outside Map");
	}

	[Fact]
	public async Task GetByBoundsAsync_ShouldHandleInvalidExtent()
	{
		// Arrange
		var map = CreateTestMap("Test Map", -122.5, 37.7, -122.3, 37.8);
		map.Extent = "invalid json";
		await _mapRepository!.InsertAsync(map);

		// Act
		var mapsInBounds = await _mapRepository.GetByBoundsAsync(-122.5, 37.7, -122.3, 37.8);

		// Assert
		mapsInBounds.Should().BeEmpty();
	}

	#endregion

	#region Download Management Tests

	[Fact]
	public async Task UpdateDownloadInfoAsync_ShouldUpdateDownloadInfo()
	{
		// Arrange
		var map = CreateTestMap("Test Map", -122.5, 37.7, -122.3, 37.8);
		var id = await _mapRepository!.InsertAsync(map);

		var downloadedExtent = JsonSerializer.Serialize(new
		{
			min_x = -122.5,
			min_y = 37.7,
			max_x = -122.3,
			max_y = 37.8,
			min_zoom = 10,
			max_zoom = 15
		});

		// Act
		var result = await _mapRepository.UpdateDownloadInfoAsync(id, downloadedExtent, 5000000);

		// Assert
		result.Should().Be(1);
		var retrieved = await _mapRepository.GetByIdAsync(id);
		retrieved!.DownloadedExtent.Should().Be(downloadedExtent);
		retrieved.DownloadSize.Should().Be(5000000);
	}

	[Fact]
	public async Task ClearDownloadInfoAsync_ShouldClearDownloadInfo()
	{
		// Arrange
		var map = CreateTestMap("Test Map", -122.5, 37.7, -122.3, 37.8);
		map.DownloadedExtent = "{\"min_x\": -122.5}";
		map.DownloadSize = 1000000;
		var id = await _mapRepository!.InsertAsync(map);

		// Act
		var result = await _mapRepository.ClearDownloadInfoAsync(id);

		// Assert
		result.Should().Be(1);
		var retrieved = await _mapRepository.GetByIdAsync(id);
		retrieved!.DownloadedExtent.Should().BeNull();
		retrieved.DownloadSize.Should().Be(0);
	}

	[Fact]
	public async Task UpdateDownloadInfoAsync_ShouldReturnZero_WhenMapNotExists()
	{
		// Act
		var result = await _mapRepository!.UpdateDownloadInfoAsync("nonexistent", "{}", 1000);

		// Assert
		result.Should().Be(0);
	}

	#endregion

	#region Statistics Tests

	[Fact]
	public async Task GetCountAsync_ShouldReturnTotalCount()
	{
		// Arrange
		await _mapRepository!.InsertAsync(CreateTestMap("Map 1", -122.5, 37.7, -122.3, 37.8));
		await _mapRepository.InsertAsync(CreateTestMap("Map 2", -121.5, 38.7, -121.3, 38.8));
		await _mapRepository.InsertAsync(CreateTestMap("Map 3", -120.5, 39.7, -120.3, 39.8));

		// Act
		var count = await _mapRepository.GetCountAsync();

		// Assert
		count.Should().Be(3);
	}

	[Fact]
	public async Task GetDownloadedCountAsync_ShouldReturnDownloadedCount()
	{
		// Arrange
		var map1 = CreateTestMap("Downloaded 1", -122.5, 37.7, -122.3, 37.8);
		map1.DownloadSize = 1000000;
		var map2 = CreateTestMap("Downloaded 2", -121.5, 38.7, -121.3, 38.8);
		map2.DownloadSize = 2000000;
		var map3 = CreateTestMap("Online", -120.5, 39.7, -120.3, 39.8);

		await _mapRepository!.InsertAsync(map1);
		await _mapRepository.InsertAsync(map2);
		await _mapRepository.InsertAsync(map3);

		// Act
		var count = await _mapRepository.GetDownloadedCountAsync();

		// Assert
		count.Should().Be(2);
	}

	[Fact]
	public async Task GetTotalDownloadSizeAsync_ShouldReturnTotalSize()
	{
		// Arrange
		var map1 = CreateTestMap("Map 1", -122.5, 37.7, -122.3, 37.8);
		map1.DownloadSize = 1000000;
		var map2 = CreateTestMap("Map 2", -121.5, 38.7, -121.3, 38.8);
		map2.DownloadSize = 2500000;
		var map3 = CreateTestMap("Map 3", -120.5, 39.7, -120.3, 39.8);
		map3.DownloadSize = 0; // Online only

		await _mapRepository!.InsertAsync(map1);
		await _mapRepository.InsertAsync(map2);
		await _mapRepository.InsertAsync(map3);

		// Act
		var totalSize = await _mapRepository.GetTotalDownloadSizeAsync();

		// Assert
		totalSize.Should().Be(3500000);
	}

	[Fact]
	public async Task GetTotalDownloadSizeAsync_ShouldReturnZero_WhenNoDownloadedMaps()
	{
		// Act
		var totalSize = await _mapRepository!.GetTotalDownloadSizeAsync();

		// Assert
		totalSize.Should().Be(0);
	}

	#endregion

	#region Helper Methods

	private Map CreateTestMap(string title, double minX, double minY, double maxX, double maxY)
	{
		var extent = JsonSerializer.Serialize(new
		{
			min_x = minX,
			min_y = minY,
			max_x = maxX,
			max_y = maxY
		});

		var basemap = JsonSerializer.Serialize(new
		{
			source = "OpenStreetMap",
			minZoom = 0,
			maxZoom = 19
		});

		return new Map
		{
			Title = title,
			Description = "Test map description",
			Extent = extent,
			Basemap = basemap,
			Layers = "[]"
		};
	}

	#endregion
}
