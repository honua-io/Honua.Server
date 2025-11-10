using SQLite;

namespace HonuaField.Models;

/// <summary>
/// Collection entity representing a feature layer/collection
/// Contains schema definition and symbology for features
/// </summary>
[Table("collections")]
public class Collection
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
	/// JSON schema definition for feature properties
	/// Defines property names, types, and constraints
	/// </summary>
	[Column("schema")]
	[NotNull]
	public string Schema { get; set; } = "{}";

	/// <summary>
	/// JSON symbology rules for rendering features
	/// Defines colors, icons, labels, and styling
	/// </summary>
	[Column("symbology")]
	[NotNull]
	public string Symbology { get; set; } = "{}";

	/// <summary>
	/// JSON bounding box for collection extent
	/// Format: {"min_x": -180, "min_y": -90, "max_x": 180, "max_y": 90}
	/// </summary>
	[Column("extent")]
	public string? Extent { get; set; }

	[Column("items_count")]
	public int ItemsCount { get; set; } = 0;
}
