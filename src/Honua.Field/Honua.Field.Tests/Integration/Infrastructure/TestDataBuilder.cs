// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace HonuaField.Tests.Integration.Infrastructure;

/// <summary>
/// Builder for creating test data with realistic values
/// Provides methods to create features, collections, attachments, GPS tracks, and users
/// </summary>
public class TestDataBuilder
{
	private readonly Random _random = new();
	private readonly string _testDataDirectory;
	private static readonly string[] SampleStreets = { "Main St", "Oak Ave", "Maple Dr", "Pine Ln", "Elm Ct", "Cedar Way" };
	private static readonly string[] SampleCities = { "Portland", "Seattle", "San Francisco", "Los Angeles", "San Diego" };

	public TestDataBuilder(string testDataDirectory)
	{
		_testDataDirectory = testDataDirectory;
	}

	/// <summary>
	/// Create a test feature with randomized properties and geometry
	/// </summary>
	public Feature CreateTestFeature(
		string? collectionId = null,
		Geometry? geometry = null,
		Dictionary<string, object>? properties = null,
		string? createdBy = null)
	{
		var feature = new Feature
		{
			Id = Guid.NewGuid().ToString(),
			CollectionId = collectionId ?? Guid.NewGuid().ToString(),
			CreatedBy = createdBy ?? $"test_user_{_random.Next(1, 100)}",
			CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Version = 1,
			SyncStatus = SyncStatus.Pending.ToString()
		};

		// Set geometry (default to random point)
		if (geometry != null)
		{
			feature.SetGeometry(geometry);
		}
		else
		{
			var point = CreateRandomPoint();
			feature.SetGeometry(point);
		}

		// Set properties
		if (properties != null)
		{
			feature.Properties = JsonSerializer.Serialize(properties);
		}
		else
		{
			feature.Properties = JsonSerializer.Serialize(CreateRandomProperties());
		}

		return feature;
	}

	/// <summary>
	/// Create a test collection with schema and symbology
	/// </summary>
	public Collection CreateTestCollection(
		string? title = null,
		string? description = null,
		string? schema = null,
		string? symbology = null)
	{
		var collection = new Collection
		{
			Id = Guid.NewGuid().ToString(),
			Title = title ?? $"Test Collection {_random.Next(1, 1000)}",
			Description = description ?? "Test collection for integration testing",
			ItemsCount = 0
		};

		// Set schema
		if (schema != null)
		{
			collection.Schema = schema;
		}
		else
		{
			collection.Schema = JsonSerializer.Serialize(CreateDefaultSchema());
		}

		// Set symbology
		if (symbology != null)
		{
			collection.Symbology = symbology;
		}
		else
		{
			collection.Symbology = JsonSerializer.Serialize(CreateDefaultSymbology());
		}

		return collection;
	}

	/// <summary>
	/// Create a test attachment with temporary file
	/// </summary>
	public Attachment CreateTestAttachment(
		string? featureId = null,
		AttachmentType type = AttachmentType.Photo,
		byte[]? fileContent = null)
	{
		var attachment = new Attachment
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = featureId ?? Guid.NewGuid().ToString(),
			Type = type.ToString(),
			Filename = $"test_{type.ToString().ToLower()}_{Guid.NewGuid()}.jpg",
			ContentType = GetContentType(type),
			UploadStatus = UploadStatus.Pending.ToString()
		};

		// Create temporary file
		var content = fileContent ?? CreateTestImageBytes();
		attachment.Filepath = Path.Combine(_testDataDirectory, attachment.Filename);
		File.WriteAllBytes(attachment.Filepath, content);
		attachment.Size = content.Length;

		// Create thumbnail for photos
		if (type == AttachmentType.Photo)
		{
			var thumbnailFilename = $"thumb_{attachment.Filename}";
			var thumbnailPath = Path.Combine(_testDataDirectory, thumbnailFilename);
			File.WriteAllBytes(thumbnailPath, CreateTestThumbnailBytes());
			attachment.Thumbnail = thumbnailPath;
		}

		// Set metadata
		attachment.Metadata = JsonSerializer.Serialize(new
		{
			capturedAt = DateTimeOffset.UtcNow.ToString("O"),
			width = 1920,
			height = 1080,
			location = new { latitude = _random.NextDouble() * 180 - 90, longitude = _random.NextDouble() * 360 - 180 }
		});

		return attachment;
	}

	/// <summary>
	/// Create a test GPS track with points
	/// </summary>
	public GpsTrack CreateTestGpsTrack(int pointCount = 10, string? status = null)
	{
		var track = new GpsTrack
		{
			Id = Guid.NewGuid().ToString(),
			Name = $"Test Track {_random.Next(1, 1000)}",
			StartTime = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds(),
			EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Status = status ?? "Completed",
			TotalDistance = 0,
			TotalDuration = 3600,
			AverageSpeed = 5.5,
			MaxSpeed = 10.0,
			MinElevation = 100,
			MaxElevation = 200,
			ElevationGain = 100,
			ElevationLoss = 50
		};

		return track;
	}

	/// <summary>
	/// Create test GPS track points for a track
	/// </summary>
	public List<GpsTrackPoint> CreateTestGpsTrackPoints(string trackId, int count = 10)
	{
		var points = new List<GpsTrackPoint>();
		var startTime = DateTimeOffset.UtcNow.AddHours(-1);
		var startLat = _random.NextDouble() * 180 - 90;
		var startLon = _random.NextDouble() * 360 - 180;

		for (int i = 0; i < count; i++)
		{
			var point = new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = trackId,
				Latitude = startLat + (_random.NextDouble() - 0.5) * 0.01,
				Longitude = startLon + (_random.NextDouble() - 0.5) * 0.01,
				Elevation = 100 + _random.NextDouble() * 100,
				Accuracy = _random.NextDouble() * 10,
				Speed = _random.NextDouble() * 10,
				Bearing = _random.NextDouble() * 360,
				Timestamp = startTime.AddMinutes(i * 6).ToUnixTimeSeconds()
			};
			points.Add(point);
		}

		return points;
	}

	/// <summary>
	/// Create a test user for authentication
	/// </summary>
	public TestUser CreateTestUser(string? username = null, string? password = null)
	{
		return new TestUser
		{
			Username = username ?? $"testuser{_random.Next(1, 1000)}",
			Password = password ?? "Test@Pass123",
			Email = $"test{_random.Next(1, 1000)}@example.com",
			AccessToken = $"access_token_{Guid.NewGuid()}",
			RefreshToken = $"refresh_token_{Guid.NewGuid()}",
			ExpiresIn = 3600
		};
	}

	/// <summary>
	/// Create a random point geometry
	/// </summary>
	public Point CreateRandomPoint(double? latitude = null, double? longitude = null)
	{
		var lat = latitude ?? _random.NextDouble() * 180 - 90;
		var lon = longitude ?? _random.NextDouble() * 360 - 180;
		return new Point(lon, lat);
	}

	/// <summary>
	/// Create a random line string geometry
	/// </summary>
	public LineString CreateRandomLineString(int pointCount = 5)
	{
		var coordinates = new Coordinate[pointCount];
		var startLat = _random.NextDouble() * 180 - 90;
		var startLon = _random.NextDouble() * 360 - 180;

		for (int i = 0; i < pointCount; i++)
		{
			coordinates[i] = new Coordinate(
				startLon + (_random.NextDouble() - 0.5) * 0.1,
				startLat + (_random.NextDouble() - 0.5) * 0.1
			);
		}

		return new LineString(coordinates);
	}

	/// <summary>
	/// Create a random polygon geometry
	/// </summary>
	public Polygon CreateRandomPolygon(int pointCount = 5)
	{
		var coordinates = new Coordinate[pointCount + 1]; // +1 to close the ring
		var centerLat = _random.NextDouble() * 180 - 90;
		var centerLon = _random.NextDouble() * 360 - 180;

		for (int i = 0; i < pointCount; i++)
		{
			var angle = (2 * Math.PI * i) / pointCount;
			var radius = 0.01;
			coordinates[i] = new Coordinate(
				centerLon + radius * Math.Cos(angle),
				centerLat + radius * Math.Sin(angle)
			);
		}

		// Close the ring
		coordinates[pointCount] = coordinates[0];

		return new Polygon(new LinearRing(coordinates));
	}

	/// <summary>
	/// Create random feature properties
	/// </summary>
	private Dictionary<string, object> CreateRandomProperties()
	{
		return new Dictionary<string, object>
		{
			{ "name", $"{_random.Next(100, 999)} {SampleStreets[_random.Next(SampleStreets.Length)]}" },
			{ "description", $"Test feature in {SampleCities[_random.Next(SampleCities.Length)]}" },
			{ "category", new[] { "Residential", "Commercial", "Industrial", "Parks" }[_random.Next(4)] },
			{ "status", new[] { "Active", "Inactive", "Pending" }[_random.Next(3)] },
			{ "value", _random.Next(100000, 1000000) },
			{ "created_date", DateTimeOffset.UtcNow.ToString("O") }
		};
	}

	/// <summary>
	/// Create a default schema for collections
	/// </summary>
	private object CreateDefaultSchema()
	{
		return new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				description = new { type = "string", title = "Description" },
				category = new
				{
					type = "string",
					title = "Category",
					@enum = new[] { "Residential", "Commercial", "Industrial", "Parks" }
				},
				status = new
				{
					type = "string",
					title = "Status",
					@enum = new[] { "Active", "Inactive", "Pending" }
				},
				value = new { type = "number", title = "Value" }
			},
			required = new[] { "name", "category" }
		};
	}

	/// <summary>
	/// Create default symbology for collections
	/// </summary>
	private object CreateDefaultSymbology()
	{
		return new
		{
			type = "simple",
			color = "#3388ff",
			fillColor = "#3388ff",
			fillOpacity = 0.2,
			weight = 3,
			opacity = 1.0
		};
	}

	/// <summary>
	/// Get content type for attachment type
	/// </summary>
	private string GetContentType(AttachmentType type)
	{
		return type switch
		{
			AttachmentType.Photo => "image/jpeg",
			AttachmentType.Video => "video/mp4",
			AttachmentType.Audio => "audio/mp3",
			AttachmentType.Document => "application/pdf",
			_ => "application/octet-stream"
		};
	}

	/// <summary>
	/// Create test image bytes (simple 1x1 pixel JPEG)
	/// </summary>
	private byte[] CreateTestImageBytes()
	{
		// Minimal JPEG header for 1x1 red pixel
		return new byte[]
		{
			0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
			0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
			0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
			0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
			0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
			0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
			0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
			0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xD9
		};
	}

	/// <summary>
	/// Create test thumbnail bytes (smaller JPEG)
	/// </summary>
	private byte[] CreateTestThumbnailBytes()
	{
		return CreateTestImageBytes(); // Same for simplicity
	}
}

/// <summary>
/// Test user data structure
/// </summary>
public class TestUser
{
	public string Username { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string AccessToken { get; set; } = string.Empty;
	public string RefreshToken { get; set; } = string.Empty;
	public int ExpiresIn { get; set; }
}
