// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace HonuaField.Services;

/// <summary>
/// Service for camera and media capture functionality
/// Handles photo, video, and audio capture with permission management
/// </summary>
public interface ICameraService
{
	/// <summary>
	/// Take a photo using the device camera
	/// </summary>
	/// <returns>Path to captured photo file, or null if cancelled/failed</returns>
	Task<CameraResult?> TakePhotoAsync();

	/// <summary>
	/// Record a video using the device camera
	/// </summary>
	/// <returns>Path to recorded video file, or null if cancelled/failed</returns>
	Task<CameraResult?> RecordVideoAsync();

	/// <summary>
	/// Record audio using the device microphone
	/// </summary>
	/// <returns>Path to recorded audio file, or null if cancelled/failed</returns>
	Task<CameraResult?> RecordAudioAsync();

	/// <summary>
	/// Pick an existing photo from the device gallery
	/// </summary>
	/// <returns>Path to selected photo file, or null if cancelled/failed</returns>
	Task<CameraResult?> PickPhotoAsync();

	/// <summary>
	/// Pick an existing video from the device gallery
	/// </summary>
	/// <returns>Path to selected video file, or null if cancelled/failed</returns>
	Task<CameraResult?> PickVideoAsync();

	/// <summary>
	/// Check if camera permission has been granted
	/// </summary>
	Task<CameraPermissionStatus> CheckCameraPermissionAsync();

	/// <summary>
	/// Request camera permission from the user
	/// </summary>
	Task<CameraPermissionStatus> RequestCameraPermissionAsync();

	/// <summary>
	/// Check if microphone permission has been granted
	/// </summary>
	Task<CameraPermissionStatus> CheckMicrophonePermissionAsync();

	/// <summary>
	/// Request microphone permission from the user
	/// </summary>
	Task<CameraPermissionStatus> RequestMicrophonePermissionAsync();

	/// <summary>
	/// Check if storage/photos permission has been granted
	/// </summary>
	Task<CameraPermissionStatus> CheckStoragePermissionAsync();

	/// <summary>
	/// Request storage/photos permission from the user
	/// </summary>
	Task<CameraPermissionStatus> RequestStoragePermissionAsync();

	/// <summary>
	/// Generate a thumbnail for an image
	/// </summary>
	/// <param name="imagePath">Path to the image file</param>
	/// <param name="maxWidth">Maximum width of the thumbnail</param>
	/// <param name="maxHeight">Maximum height of the thumbnail</param>
	/// <returns>Path to the generated thumbnail file</returns>
	Task<string?> GenerateImageThumbnailAsync(string imagePath, int maxWidth = 200, int maxHeight = 200);

	/// <summary>
	/// Generate a thumbnail for a video
	/// </summary>
	/// <param name="videoPath">Path to the video file</param>
	/// <returns>Path to the generated thumbnail file</returns>
	Task<string?> GenerateVideoThumbnailAsync(string videoPath);

	/// <summary>
	/// Compress an image to reduce file size
	/// </summary>
	/// <param name="imagePath">Path to the image file</param>
	/// <param name="quality">Compression quality (0-100)</param>
	/// <returns>Path to the compressed image file</returns>
	Task<string?> CompressImageAsync(string imagePath, int quality = 70);

	/// <summary>
	/// Get media file metadata (dimensions, duration, etc.)
	/// </summary>
	/// <param name="filePath">Path to the media file</param>
	/// <returns>Metadata dictionary</returns>
	Task<Dictionary<string, object>?> GetMediaMetadataAsync(string filePath);
}

/// <summary>
/// Result of a camera/media operation
/// </summary>
public class CameraResult
{
	/// <summary>
	/// Path to the captured/selected media file
	/// </summary>
	public required string FilePath { get; init; }

	/// <summary>
	/// Original filename
	/// </summary>
	public required string FileName { get; init; }

	/// <summary>
	/// Content type (MIME type)
	/// </summary>
	public required string ContentType { get; init; }

	/// <summary>
	/// File size in bytes
	/// </summary>
	public long FileSize { get; init; }

	/// <summary>
	/// Path to thumbnail (if generated)
	/// </summary>
	public string? ThumbnailPath { get; init; }

	/// <summary>
	/// Metadata (dimensions, duration, location, etc.)
	/// </summary>
	public Dictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Whether the operation was successful
	/// </summary>
	public bool Success { get; init; } = true;

	/// <summary>
	/// Error message if operation failed
	/// </summary>
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Camera and media permission status
/// </summary>
public enum CameraPermissionStatus
{
	Unknown,
	Denied,
	Granted,
	Restricted
}
