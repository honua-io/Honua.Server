using SQLite;

namespace HonuaField.Models;

/// <summary>
/// Map entity for map configurations and offline downloads
/// Contains basemap, layers, and extent information
/// </summary>
[Table("maps")]
public class Map
{
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Column("title")]
	[NotNull]
	public string Title { get; set; } = string.Empty;

	[Column("description")]
	public string? Description { get; set; }

	/// <summary>
	/// JSON bounding box for map extent
	/// Format: {"min_x": -180, "min_y": -90, "max_x": 180, "max_y": 90}
	/// </summary>
	[Column("extent")]
	[NotNull]
	public string Extent { get; set; } = "{}";

	/// <summary>
	/// JSON basemap configuration
	/// Contains tile source, zoom levels, attribution
	/// Example: {"source": "OpenStreetMap", "minZoom": 0, "maxZoom": 19}
	/// </summary>
	[Column("basemap")]
	[NotNull]
	public string Basemap { get; set; } = "{}";

	/// <summary>
	/// JSON array of MapLayer objects
	/// Defines which layers to display on this map
	/// </summary>
	[Column("layers")]
	[NotNull]
	public string Layers { get; set; } = "[]";

	/// <summary>
	/// JSON geometry of downloaded offline area
	/// Null if map is online-only
	/// </summary>
	[Column("downloaded_extent")]
	public string? DownloadedExtent { get; set; }

	/// <summary>
	/// Storage size in bytes for offline tiles
	/// 0 if map is online-only
	/// </summary>
	[Column("download_size")]
	public long DownloadSize { get; set; } = 0;
}
