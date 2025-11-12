// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Honua.Server.Core.Models;

/// <summary>
/// Represents a shareable link token for maps
/// </summary>
[Table("share_tokens")]
public class ShareToken
{
    /// <summary>
    /// Unique token (GUID-based URL)
    /// </summary>
    [Key]
    [Column("token")]
    [MaxLength(100)]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Map configuration ID being shared
    /// </summary>
    [Required]
    [Column("map_id")]
    [MaxLength(100)]
    public string MapId { get; set; } = string.Empty;

    /// <summary>
    /// User who created the share
    /// </summary>
    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Permission level: view, comment, edit
    /// </summary>
    [Required]
    [Column("permission")]
    [MaxLength(20)]
    public string Permission { get; set; } = "view";

    /// <summary>
    /// Whether the share allows guest (non-authenticated) access
    /// </summary>
    [Column("allow_guest_access")]
    public bool AllowGuestAccess { get; set; } = true;

    /// <summary>
    /// Expiration date (null = never expires)
    /// </summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the token was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this share has been accessed
    /// </summary>
    [Column("access_count")]
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// Last time the share was accessed
    /// </summary>
    [Column("last_accessed_at")]
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Whether the share is still active
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional password for protected shares
    /// </summary>
    [Column("password_hash")]
    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Custom embed settings (JSON)
    /// </summary>
    [Column("embed_settings", TypeName = "jsonb")]
    public string? EmbedSettings { get; set; }

    /// <summary>
    /// Check if the token is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    /// <summary>
    /// Check if the token is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired;
}

/// <summary>
/// Permission levels for shared maps
/// </summary>
public static class SharePermission
{
    public const string View = "view";
    public const string Comment = "comment";
    public const string Edit = "edit";

    public static bool IsValid(string permission) =>
        permission == View || permission == Comment || permission == Edit;
}

/// <summary>
/// Settings for embedded maps
/// </summary>
public class EmbedSettings
{
    /// <summary>
    /// Width of the embedded map (e.g., "100%", "800px")
    /// </summary>
    public string Width { get; set; } = "100%";

    /// <summary>
    /// Height of the embedded map (e.g., "600px", "100vh")
    /// </summary>
    public string Height { get; set; } = "600px";

    /// <summary>
    /// Show zoom controls
    /// </summary>
    public bool ShowZoomControls { get; set; } = true;

    /// <summary>
    /// Show layer switcher
    /// </summary>
    public bool ShowLayerSwitcher { get; set; } = true;

    /// <summary>
    /// Show search box
    /// </summary>
    public bool ShowSearch { get; set; } = false;

    /// <summary>
    /// Show scale bar
    /// </summary>
    public bool ShowScaleBar { get; set; } = true;

    /// <summary>
    /// Show attribution
    /// </summary>
    public bool ShowAttribution { get; set; } = true;

    /// <summary>
    /// Allow fullscreen mode
    /// </summary>
    public bool AllowFullscreen { get; set; } = true;

    /// <summary>
    /// Custom CSS for the embedded map
    /// </summary>
    public string? CustomCss { get; set; }
}
