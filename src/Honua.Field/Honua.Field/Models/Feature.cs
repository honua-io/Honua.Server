// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using SQLite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Text.Json;

namespace HonuaField.Models;

/// <summary>
/// Feature entity representing a spatial feature with geometry and properties
/// Stored in SQLite with WKB (Well-Known Binary) geometry format
/// </summary>
[Table("features")]
public class Feature
{
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Column("server_id")]
	public string? ServerId { get; set; }

	[Column("collection_id")]
	[NotNull]
	public string CollectionId { get; set; } = string.Empty;

	/// <summary>
	/// Geometry stored as WKB (Well-Known Binary) format
	/// Use GetGeometry() and SetGeometry() to work with NetTopologySuite geometry
	/// </summary>
	[Column("geometry")]
	[NotNull]
	public byte[] GeometryWkb { get; set; } = Array.Empty<byte>();

	[Column("properties")]
	[NotNull]
	public string Properties { get; set; } = "{}";

	[Column("created_at")]
	[NotNull]
	public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	[Column("updated_at")]
	[NotNull]
	public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	[Column("created_by")]
	[NotNull]
	public string CreatedBy { get; set; } = string.Empty;

	[Column("version")]
	[NotNull]
	public int Version { get; set; } = 1;

	[Column("sync_status")]
	[NotNull]
	public string SyncStatus { get; set; } = "Pending";

	// Navigation properties (not stored in database)
	[Ignore]
	public Collection? Collection { get; set; }

	/// <summary>
	/// Get the geometry as NetTopologySuite Geometry object
	/// </summary>
	public Geometry? GetGeometry()
	{
		if (GeometryWkb == null || GeometryWkb.Length == 0)
			return null;

		try
		{
			var reader = new WKBReader();
			return reader.Read(GeometryWkb);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error reading WKB geometry: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Set the geometry from NetTopologySuite Geometry object
	/// </summary>
	public void SetGeometry(Geometry geometry)
	{
		if (geometry == null)
		{
			GeometryWkb = Array.Empty<byte>();
			return;
		}

		try
		{
			var writer = new WKBWriter();
			GeometryWkb = writer.Write(geometry);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error writing WKB geometry: {ex.Message}");
			GeometryWkb = Array.Empty<byte>();
		}
	}

	/// <summary>
	/// Get bounding box for spatial indexing
	/// Returns min_x, max_x, min_y, max_y
	/// </summary>
	public (double minX, double maxX, double minY, double maxY)? GetBounds()
	{
		var geometry = GetGeometry();
		if (geometry == null)
			return null;

		var envelope = geometry.EnvelopeInternal;
		return (envelope.MinX, envelope.MaxX, envelope.MinY, envelope.MaxY);
	}

	/// <summary>
	/// Get the properties as a dictionary
	/// </summary>
	public Dictionary<string, object> GetPropertiesDict()
	{
		if (string.IsNullOrWhiteSpace(Properties))
			return new Dictionary<string, object>();

		try
		{
			var props = JsonSerializer.Deserialize<Dictionary<string, object>>(Properties);
			return props ?? new Dictionary<string, object>();
		}
		catch
		{
			return new Dictionary<string, object>();
		}
	}
}

/// <summary>
/// Sync status enum for feature synchronization
/// </summary>
public enum SyncStatus
{
	Synced,     // Feature synchronized with server
	Pending,    // Created/edited locally, not yet synced
	Conflict,   // Conflicting edits from multiple users
	Error       // Sync failed with error
}
