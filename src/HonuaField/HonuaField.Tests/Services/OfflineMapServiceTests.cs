using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for OfflineMapService
/// Tests tile downloading, storage, deletion, and size estimation
/// </summary>
public class OfflineMapServiceTests : IAsyncLifetime
{
	private HonuaFieldDatabase? _database;
	private MapRepository? _mapRepository;
	private OfflineMapService? _offlineMapService;
	private readonly string _testDbPath;
	private readonly string _testTilesPath;

	public OfflineMapServiceTests()
	{
		// Create unique paths for each test run
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
		_testTilesPath = Path.Combine(Path.GetTempPath(), $"test_tiles_{Guid.NewGuid()}");
	}

	public async Task InitializeAsync()
	{
		// Initialize database and repositories
		_database = new HonuaFieldDatabase(_testDbPath);
		await _database.InitializeAsync();

		_mapRepository = new MapRepository(_database);
		_offlineMapService = new OfflineMapService(_mapRepository);

		// Create test tiles directory
		Directory.CreateDirectory(_testTilesPath);
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

		// Clean up test tiles directory
		if (Directory.Exists(_testTilesPath))
		{
			Directory.Delete(_testTilesPath, recursive: true);
		}

		// Clean up service tiles directory
		var tilesDir = _offlineMapService?.GetTilesDirectory();
		if (!string.IsNullOrEmpty(tilesDir) && Directory.Exists(tilesDir))
		{
			try
			{
				Directory.Delete(tilesDir, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}

	#region Tile Source Tests

	[Fact]
	public async Task GetTileSourcesAsync_ShouldReturnAvailableSources()
	{
		// Act
		var sources = await _offlineMapService!.GetTileSourcesAsync();

		// Assert
		sources.Should().NotBeEmpty();
		sources.Should().Contain(s => s.Id == "osm");
		sources.Should().Contain(s => s.Name == "OpenStreetMap");
	}

	[Fact]
	public async Task GetTileSourcesAsync_ShouldIncludeUrlTemplates()
	{
		// Act
		var sources = await _offlineMapService!.GetTileSourcesAsync();

		// Assert
		var osmSource = sources.First(s => s.Id == "osm");
		osmSource.UrlTemplate.Should().Contain("{z}");
		osmSource.UrlTemplate.Should().Contain("{x}");
		osmSource.UrlTemplate.Should().Contain("{y}");
	}

	#endregion

	#region Size Estimation Tests

	[Fact]
	public async Task GetMapSizeAsync_ShouldEstimateTileCount()
	{
		// Arrange - small area, single zoom level
		var bounds = (-122.42, 37.77, -122.41, 37.78);

		// Act
		var (estimatedBytes, tileCount) = await _offlineMapService!.GetMapSizeAsync(bounds, 10, 10);

		// Assert
		tileCount.Should().BeGreaterThan(0);
		estimatedBytes.Should().BeGreaterThan(0);
		estimatedBytes.Should().Be(tileCount * 15000); // Average tile size
	}

	[Fact]
	public async Task GetMapSizeAsync_ShouldIncreaseWithZoomLevels()
	{
		// Arrange
		var bounds = (-122.42, 37.77, -122.41, 37.78);

		// Act
		var (bytes1, tiles1) = await _offlineMapService!.GetMapSizeAsync(bounds, 10, 10);
		var (bytes2, tiles2) = await _offlineMapService.GetMapSizeAsync(bounds, 10, 12);

		// Assert
		tiles2.Should().BeGreaterThan(tiles1);
		bytes2.Should().BeGreaterThan(bytes1);
	}

	[Fact]
	public async Task GetMapSizeAsync_ShouldIncreaseWithArea()
	{
		// Arrange
		var smallBounds = (-122.42, 37.77, -122.41, 37.78);
		var largeBounds = (-122.50, 37.70, -122.30, 37.85);

		// Act
		var (bytes1, tiles1) = await _offlineMapService!.GetMapSizeAsync(smallBounds, 10, 10);
		var (bytes2, tiles2) = await _offlineMapService.GetMapSizeAsync(largeBounds, 10, 10);

		// Assert
		tiles2.Should().BeGreaterThan(tiles1);
		bytes2.Should().BeGreaterThan(bytes1);
	}

	#endregion

	#region Tile Storage Tests

	[Fact]
	public async Task GetTilesDirectory_ShouldReturnValidPath()
	{
		// Act
		var tilesDir = _offlineMapService!.GetTilesDirectory();

		// Assert
		tilesDir.Should().NotBeNullOrEmpty();
		Path.IsPathRooted(tilesDir).Should().BeTrue();
	}

	[Fact]
	public async Task TileExistsAsync_ShouldReturnFalse_WhenTileNotExists()
	{
		// Arrange
		var mapId = "test-map";

		// Act
		var exists = await _offlineMapService!.TileExistsAsync(mapId, 10, 100, 200);

		// Assert
		exists.Should().BeFalse();
	}

	[Fact]
	public async Task GetTileAsync_ShouldReturnNull_WhenTileNotExists()
	{
		// Arrange
		var mapId = "test-map";

		// Act
		var tile = await _offlineMapService!.GetTileAsync(mapId, 10, 100, 200);

		// Assert
		tile.Should().BeNull();
	}

	#endregion

	#region Available Maps Tests

	[Fact]
	public async Task GetAvailableMapsAsync_ShouldReturnEmptyList_WhenNoDownloadedMaps()
	{
		// Act
		var maps = await _offlineMapService!.GetAvailableMapsAsync();

		// Assert
		maps.Should().BeEmpty();
	}

	[Fact]
	public async Task GetAvailableMapsAsync_ShouldReturnDownloadedMaps()
	{
		// Arrange
		var map = CreateTestMap("Downloaded Map");
		map.DownloadSize = 1000000;
		map.DownloadedExtent = JsonSerializer.Serialize(new
		{
			min_x = -122.5,
			min_y = 37.7,
			max_x = -122.3,
			max_y = 37.8,
			min_zoom = 10,
			max_zoom = 15
		});
		await _mapRepository!.InsertAsync(map);

		// Act
		var maps = await _offlineMapService!.GetAvailableMapsAsync();

		// Assert
		maps.Should().HaveCount(1);
		maps[0].MapId.Should().Be(map.Id);
		maps[0].Title.Should().Be("Downloaded Map");
		maps[0].DownloadSize.Should().Be(1000000);
		maps[0].MinZoom.Should().Be(10);
		maps[0].MaxZoom.Should().Be(15);
	}

	[Fact]
	public async Task GetAvailableMapsAsync_ShouldHandleInvalidJson()
	{
		// Arrange
		var map = CreateTestMap("Invalid Map");
		map.DownloadSize = 1000000;
		map.Basemap = "invalid json";
		await _mapRepository!.InsertAsync(map);

		// Act
		var maps = await _offlineMapService!.GetAvailableMapsAsync();

		// Assert
		// Should not throw, but may not include the invalid map
		maps.Should().NotBeNull();
	}

	#endregion

	#region Delete Map Tests

	[Fact]
	public async Task DeleteMapAsync_ShouldReturnTrue_WhenMapDeleted()
	{
		// Arrange
		var map = CreateTestMap("Test Map");
		map.DownloadSize = 1000000;
		await _mapRepository!.InsertAsync(map);

		// Act
		var result = await _offlineMapService!.DeleteMapAsync(map.Id);

		// Assert
		result.Should().BeTrue();
		var retrieved = await _mapRepository.GetByIdAsync(map.Id);
		retrieved!.DownloadSize.Should().Be(0);
		retrieved.DownloadedExtent.Should().BeNull();
	}

	[Fact]
	public async Task DeleteMapAsync_ShouldDeleteTileDirectory()
	{
		// Arrange
		var map = CreateTestMap("Test Map");
		await _mapRepository!.InsertAsync(map);

		// Create a fake tile directory
		var tilesDir = _offlineMapService!.GetTilesDirectory();
		var mapDir = Path.Combine(tilesDir, map.Id);
		Directory.CreateDirectory(mapDir);
		File.WriteAllText(Path.Combine(mapDir, "test.txt"), "test");

		// Act
		var result = await _offlineMapService.DeleteMapAsync(map.Id);

		// Assert
		result.Should().BeTrue();
		Directory.Exists(mapDir).Should().BeFalse();
	}

	#endregion

	#region Storage Management Tests

	[Fact]
	public async Task GetTotalStorageUsedAsync_ShouldReturnZero_WhenNoTiles()
	{
		// Act
		var storage = await _offlineMapService!.GetTotalStorageUsedAsync();

		// Assert
		storage.Should().Be(0);
	}

	[Fact]
	public async Task GetTotalStorageUsedAsync_ShouldCalculateTotalSize()
	{
		// Arrange
		var tilesDir = _offlineMapService!.GetTilesDirectory();
		Directory.CreateDirectory(tilesDir);

		// Create some test files
		var testData1 = new byte[1000];
		var testData2 = new byte[2000];
		await File.WriteAllBytesAsync(Path.Combine(tilesDir, "tile1.png"), testData1);
		await File.WriteAllBytesAsync(Path.Combine(tilesDir, "tile2.png"), testData2);

		// Act
		var storage = await _offlineMapService.GetTotalStorageUsedAsync();

		// Assert
		storage.Should().BeGreaterOrEqualTo(3000);
	}

	[Fact]
	public async Task CleanupStorageAsync_ShouldReturnZero_WhenUnderQuota()
	{
		// Arrange
		var maxStorage = 10000000L; // 10MB

		// Act
		var bytesFreed = await _offlineMapService!.CleanupStorageAsync(maxStorage);

		// Assert
		bytesFreed.Should().Be(0);
	}

	[Fact]
	public async Task CleanupStorageAsync_ShouldDeleteMaps_WhenOverQuota()
	{
		// Arrange
		var map1 = CreateTestMap("Large Map 1");
		map1.DownloadSize = 5000000;
		var map2 = CreateTestMap("Large Map 2");
		map2.DownloadSize = 6000000;

		await _mapRepository!.InsertAsync(map1);
		await _mapRepository.InsertAsync(map2);

		var maxStorage = 1000000L; // 1MB - much less than 11MB total

		// Act
		var bytesFreed = await _offlineMapService!.CleanupStorageAsync(maxStorage);

		// Assert
		bytesFreed.Should().BeGreaterThan(0);
		var remainingMaps = await _mapRepository.GetDownloadedMapsAsync();
		remainingMaps.Count.Should().BeLessThan(2);
	}

	#endregion

	#region Download Tests

	[Fact]
	public async Task DownloadTilesAsync_ShouldReturnError_WhenMapNotExists()
	{
		// Arrange
		var bounds = (-122.42, 37.77, -122.41, 37.78);
		var tileSource = new TileSource
		{
			Id = "test",
			Name = "Test",
			UrlTemplate = "https://example.com/{z}/{x}/{y}.png"
		};

		// Act
		var result = await _offlineMapService!.DownloadTilesAsync(
			"nonexistent",
			bounds,
			10,
			10,
			tileSource);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Contain("Map not found");
	}

	[Fact]
	public async Task DownloadTilesAsync_ShouldReturnError_WhenNoTilesToDownload()
	{
		// Arrange
		var map = CreateTestMap("Test Map");
		await _mapRepository!.InsertAsync(map);

		// Invalid bounds (same min and max)
		var bounds = (-122.42, 37.77, -122.42, 37.77);
		var tileSource = new TileSource
		{
			Id = "test",
			Name = "Test",
			UrlTemplate = "https://example.com/{z}/{x}/{y}.png"
		};

		// Act
		var result = await _offlineMapService!.DownloadTilesAsync(
			map.Id,
			bounds,
			10,
			10,
			tileSource);

		// Assert
		result.Success.Should().BeFalse();
	}

	[Fact]
	public async Task DownloadTilesAsync_ShouldReportProgress()
	{
		// Arrange
		var map = CreateTestMap("Test Map");
		await _mapRepository!.InsertAsync(map);

		var bounds = (-122.42, 37.77, -122.41, 37.78);
		var tileSource = new TileSource
		{
			Id = "test",
			Name = "Test",
			UrlTemplate = "https://httpbin.org/status/404" // Will fail but that's ok for progress test
		};

		var progressReports = new List<TileDownloadProgress>();
		var progress = new Progress<TileDownloadProgress>(p => progressReports.Add(p));

		// Act
		var result = await _offlineMapService!.DownloadTilesAsync(
			map.Id,
			bounds,
			10,
			10,
			tileSource,
			progress,
			CancellationToken.None);

		// Assert
		progressReports.Should().NotBeEmpty();
		progressReports.Should().Contain(p => p.Stage == TileDownloadStage.Initializing);
		progressReports.Should().Contain(p => p.Stage == TileDownloadStage.Calculating);
	}

	[Fact]
	public async Task DownloadTilesAsync_ShouldSupportCancellation()
	{
		// Arrange
		var map = CreateTestMap("Test Map");
		await _mapRepository!.InsertAsync(map);

		var bounds = (-122.5, 37.7, -122.3, 37.8); // Larger area
		var tileSource = new TileSource
		{
			Id = "test",
			Name = "Test",
			UrlTemplate = "https://httpbin.org/delay/10" // Slow endpoint
		};

		var cts = new CancellationTokenSource();
		cts.CancelAfter(TimeSpan.FromMilliseconds(100));

		// Act & Assert
		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
		{
			await _offlineMapService!.DownloadTilesAsync(
				map.Id,
				bounds,
				10,
				12,
				tileSource,
				null,
				cts.Token);
		});
	}

	#endregion

	#region Helper Methods

	private Map CreateTestMap(string title)
	{
		var extent = JsonSerializer.Serialize(new
		{
			min_x = -122.5,
			min_y = 37.7,
			max_x = -122.3,
			max_y = 37.8
		});

		var basemap = JsonSerializer.Serialize(new
		{
			source = "osm",
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
