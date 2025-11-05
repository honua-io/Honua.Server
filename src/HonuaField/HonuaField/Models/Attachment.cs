using SQLite;

namespace HonuaField.Models;

/// <summary>
/// Attachment entity for photos, videos, audio, and documents
/// Linked to features with cascade delete
/// </summary>
[Table("attachments")]
public class Attachment
{
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Column("feature_id")]
	[NotNull]
	public string FeatureId { get; set; } = string.Empty;

	[Column("type")]
	[NotNull]
	public string Type { get; set; } = AttachmentType.Photo.ToString();

	[Column("filename")]
	[NotNull]
	public string Filename { get; set; } = string.Empty;

	[Column("filepath")]
	[NotNull]
	public string Filepath { get; set; } = string.Empty;

	[Column("content_type")]
	[NotNull]
	public string ContentType { get; set; } = string.Empty;

	[Column("size")]
	[NotNull]
	public long Size { get; set; }

	[Column("thumbnail")]
	public string? Thumbnail { get; set; }

	/// <summary>
	/// JSON metadata including EXIF, location, dimensions, duration
	/// Example: {"capturedAt": "2024-01-01T12:00:00Z", "location": {...}, "width": 1920, "height": 1080}
	/// </summary>
	[Column("metadata")]
	public string? Metadata { get; set; }

	[Column("upload_status")]
	[NotNull]
	public string UploadStatus { get; set; } = UploadStatus.Pending.ToString();

	// Navigation properties
	[Ignore]
	public Feature? Feature { get; set; }
}

/// <summary>
/// Attachment type enum
/// </summary>
public enum AttachmentType
{
	Photo,
	Video,
	Audio,
	Document
}

/// <summary>
/// Upload status enum for attachment sync
/// </summary>
public enum UploadStatus
{
	Pending,
	Uploading,
	Uploaded,
	Failed
}
