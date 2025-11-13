namespace Honua.MapSDK.Models;

public class MapComment
{
    public string Id { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string? LayerId { get; set; }
    public string? FeatureId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? AuthorUserId { get; set; }
    public bool IsGuest { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public string? Geometry { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ParentId { get; set; }
    public int ThreadDepth { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Category { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsApproved { get; set; }
    public bool IsPinned { get; set; }
    public int ReplyCount { get; set; }
    public int LikeCount { get; set; }
    public string? Attachments { get; set; }
}
