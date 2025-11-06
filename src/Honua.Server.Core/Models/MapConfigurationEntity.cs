using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Honua.Server.Core.Models;

/// <summary>
/// Database entity for saved map configurations
/// </summary>
[Table("map_configurations")]
public class MapConfigurationEntity
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Full configuration as JSON
    /// </summary>
    [Required]
    [Column("configuration", TypeName = "jsonb")]
    public string Configuration { get; set; } = "{}";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Column("is_public")]
    public bool IsPublic { get; set; } = false;

    [Column("is_template")]
    public bool IsTemplate { get; set; } = false;

    /// <summary>
    /// Tags for categorization (comma-separated)
    /// </summary>
    [Column("tags")]
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Thumbnail/preview image URL
    /// </summary>
    [Column("thumbnail_url")]
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// View count for analytics
    /// </summary>
    [Column("view_count")]
    public int ViewCount { get; set; } = 0;
}
