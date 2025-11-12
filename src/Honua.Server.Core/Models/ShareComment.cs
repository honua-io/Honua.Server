// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Honua.Server.Core.Models;

/// <summary>
/// Represents a comment on a shared map
/// </summary>
[Table("share_comments")]
public class ShareComment
{
    /// <summary>
    /// Unique comment ID
    /// </summary>
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Share token this comment belongs to
    /// </summary>
    [Required]
    [Column("share_token")]
    [MaxLength(100)]
    public string ShareToken { get; set; } = string.Empty;

    /// <summary>
    /// Map ID for direct reference
    /// </summary>
    [Required]
    [Column("map_id")]
    [MaxLength(100)]
    public string MapId { get; set; } = string.Empty;

    /// <summary>
    /// Comment author (user ID or guest name)
    /// </summary>
    [Required]
    [Column("author")]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a guest comment
    /// </summary>
    [Column("is_guest")]
    public bool IsGuest { get; set; } = true;

    /// <summary>
    /// Guest email (for notifications)
    /// </summary>
    [Column("guest_email")]
    [MaxLength(200)]
    public string? GuestEmail { get; set; }

    /// <summary>
    /// Comment text
    /// </summary>
    [Required]
    [Column("comment_text")]
    [MaxLength(5000)]
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// When the comment was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the comment has been moderated/approved
    /// </summary>
    [Column("is_approved")]
    public bool IsApproved { get; set; } = false;

    /// <summary>
    /// Whether the comment has been deleted
    /// </summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Parent comment ID for threaded discussions
    /// </summary>
    [Column("parent_id")]
    [MaxLength(100)]
    public string? ParentId { get; set; }

    /// <summary>
    /// Optional location/coordinates for map annotations
    /// </summary>
    [Column("location_x")]
    public double? LocationX { get; set; }

    [Column("location_y")]
    public double? LocationY { get; set; }

    /// <summary>
    /// IP address for spam prevention
    /// </summary>
    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent for spam prevention
    /// </summary>
    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }
}
