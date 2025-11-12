// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Cross-platform camera and media capture service
/// Uses .NET MAUI MediaPicker API for capturing and selecting media
/// </summary>
public class CameraService : ICameraService
{
	private readonly ILogger<CameraService> _logger;
	private readonly string _mediaStoragePath;

	public CameraService(ILogger<CameraService> logger)
	{
		_logger = logger;
		_mediaStoragePath = Path.Combine(FileSystem.AppDataDirectory, "media");

		// Ensure media directory exists
		if (!Directory.Exists(_mediaStoragePath))
		{
			Directory.CreateDirectory(_mediaStoragePath);
		}
	}

	/// <summary>
	/// Take a photo using the device camera
	/// </summary>
	public async Task<CameraResult?> TakePhotoAsync()
	{
		try
		{
			// Check camera permission
			var permissionStatus = await RequestCameraPermissionAsync();
			if (permissionStatus != CameraPermissionStatus.Granted)
			{
				_logger.LogWarning("Camera permission not granted");
				return null;
			}

			// Check if camera is available
			if (!MediaPicker.Default.IsCaptureSupported)
			{
				_logger.LogWarning("Camera capture is not supported on this device");
				return null;
			}

			// Capture photo
			var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
			{
				Title = "Take Photo"
			});

			if (photo == null)
			{
				_logger.LogInformation("Photo capture was cancelled");
				return null;
			}

			// Save to app storage
			return await ProcessMediaFileAsync(photo, "photo");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to capture photo");
			return new CameraResult
			{
				FilePath = string.Empty,
				FileName = string.Empty,
				ContentType = string.Empty,
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <summary>
	/// Record a video using the device camera
	/// </summary>
	public async Task<CameraResult?> RecordVideoAsync()
	{
		try
		{
			// Check camera permission
			var permissionStatus = await RequestCameraPermissionAsync();
			if (permissionStatus != CameraPermissionStatus.Granted)
			{
				_logger.LogWarning("Camera permission not granted");
				return null;
			}

			// Check if video capture is available
			if (!MediaPicker.Default.IsCaptureSupported)
			{
				_logger.LogWarning("Video capture is not supported on this device");
				return null;
			}

			// Capture video
			var video = await MediaPicker.Default.CaptureVideoAsync(new MediaPickerOptions
			{
				Title = "Record Video"
			});

			if (video == null)
			{
				_logger.LogInformation("Video capture was cancelled");
				return null;
			}

			// Save to app storage
			var result = await ProcessMediaFileAsync(video, "video");

			// Generate thumbnail for video
			if (result != null && result.Success)
			{
				var thumbnailPath = await GenerateVideoThumbnailAsync(result.FilePath);
				if (thumbnailPath != null)
				{
					result = result with { ThumbnailPath = thumbnailPath };
				}
			}

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to capture video");
			return new CameraResult
			{
				FilePath = string.Empty,
				FileName = string.Empty,
				ContentType = string.Empty,
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <summary>
	/// Record audio using the device microphone
	/// </summary>
	public async Task<CameraResult?> RecordAudioAsync()
	{
		try
		{
			// Check microphone permission
			var permissionStatus = await RequestMicrophonePermissionAsync();
			if (permissionStatus != CameraPermissionStatus.Granted)
			{
				_logger.LogWarning("Microphone permission not granted");
				return null;
			}

			// Note: .NET MAUI doesn't have built-in audio recording
			// This would require platform-specific implementations
			// For now, we'll log a warning and return null
			_logger.LogWarning("Audio recording requires platform-specific implementation");

			return new CameraResult
			{
				FilePath = string.Empty,
				FileName = string.Empty,
				ContentType = string.Empty,
				Success = false,
				ErrorMessage = "Audio recording is not yet implemented. Requires platform-specific code."
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to record audio");
			return new CameraResult
			{
				FilePath = string.Empty,
				FileName = string.Empty,
				ContentType = string.Empty,
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <summary>
	/// Pick an existing photo from the device gallery
	/// </summary>
	public async Task<CameraResult?> PickPhotoAsync()
	{
		try
		{
			// Check storage permission
			var permissionStatus = await RequestStoragePermissionAsync();
			if (permissionStatus != CameraPermissionStatus.Granted)
			{
				_logger.LogWarning("Storage permission not granted");
				return null;
			}

			// Pick photo
			var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
			{
				Title = "Select Photo"
			});

			if (photo == null)
			{
				_logger.LogInformation("Photo selection was cancelled");
				return null;
			}

			// Save to app storage
			return await ProcessMediaFileAsync(photo, "photo");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to pick photo");
			return new CameraResult
			{
				FilePath = string.Empty,
				FileName = string.Empty,
				ContentType = string.Empty,
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <summary>
	/// Pick an existing video from the device gallery
	/// </summary>
	public async Task<CameraResult?> PickVideoAsync()
	{
		try
		{
			// Check storage permission
			var permissionStatus = await RequestStoragePermissionAsync();
			if (permissionStatus != CameraPermissionStatus.Granted)
			{
				_logger.LogWarning("Storage permission not granted");
				return null;
			}

			// Pick video
			var video = await MediaPicker.Default.PickVideoAsync(new MediaPickerOptions
			{
				Title = "Select Video"
			});

			if (video == null)
			{
				_logger.LogInformation("Video selection was cancelled");
				return null;
			}

			// Save to app storage
			var result = await ProcessMediaFileAsync(video, "video");

			// Generate thumbnail for video
			if (result != null && result.Success)
			{
				var thumbnailPath = await GenerateVideoThumbnailAsync(result.FilePath);
				if (thumbnailPath != null)
				{
					result = result with { ThumbnailPath = thumbnailPath };
				}
			}

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to pick video");
			return new CameraResult
			{
				FilePath = string.Empty,
				FileName = string.Empty,
				ContentType = string.Empty,
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <summary>
	/// Check if camera permission has been granted
	/// </summary>
	public async Task<CameraPermissionStatus> CheckCameraPermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
			return MapPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check camera permission");
			return CameraPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Request camera permission from the user
	/// </summary>
	public async Task<CameraPermissionStatus> RequestCameraPermissionAsync()
	{
		try
		{
			var status = await Permissions.RequestAsync<Permissions.Camera>();
			return MapPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to request camera permission");
			return CameraPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Check if microphone permission has been granted
	/// </summary>
	public async Task<CameraPermissionStatus> CheckMicrophonePermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
			return MapPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check microphone permission");
			return CameraPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Request microphone permission from the user
	/// </summary>
	public async Task<CameraPermissionStatus> RequestMicrophonePermissionAsync()
	{
		try
		{
			var status = await Permissions.RequestAsync<Permissions.Microphone>();
			return MapPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to request microphone permission");
			return CameraPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Check if storage/photos permission has been granted
	/// </summary>
	public async Task<CameraPermissionStatus> CheckStoragePermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.Photos>();
			return MapPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check storage permission");
			return CameraPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Request storage/photos permission from the user
	/// </summary>
	public async Task<CameraPermissionStatus> RequestStoragePermissionAsync()
	{
		try
		{
			var status = await Permissions.RequestAsync<Permissions.Photos>();
			return MapPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to request storage permission");
			return CameraPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Generate a thumbnail for an image
	/// </summary>
	public async Task<string?> GenerateImageThumbnailAsync(string imagePath, int maxWidth = 200, int maxHeight = 200)
	{
		try
		{
			if (!File.Exists(imagePath))
			{
				_logger.LogWarning($"Image file not found: {imagePath}");
				return null;
			}

			// Load image stream
			using var sourceStream = File.OpenRead(imagePath);

			// Load image
			var image = await Task.Run(() =>
			{
				using var ms = new MemoryStream();
				sourceStream.CopyTo(ms);
				return ms.ToArray();
			});

			// For thumbnail generation, we would typically use a library like SkiaSharp
			// For now, we'll just copy the file and log a warning
			var thumbnailFileName = $"thumb_{Path.GetFileName(imagePath)}";
			var thumbnailPath = Path.Combine(_mediaStoragePath, "thumbnails", thumbnailFileName);

			// Ensure thumbnails directory exists
			var thumbnailsDir = Path.Combine(_mediaStoragePath, "thumbnails");
			if (!Directory.Exists(thumbnailsDir))
			{
				Directory.CreateDirectory(thumbnailsDir);
			}

			// Copy file as placeholder (in production, should resize)
			File.Copy(imagePath, thumbnailPath, true);

			_logger.LogInformation($"Generated thumbnail (copy): {thumbnailPath}");
			return thumbnailPath;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate image thumbnail");
			return null;
		}
	}

	/// <summary>
	/// Generate a thumbnail for a video
	/// </summary>
	public async Task<string?> GenerateVideoThumbnailAsync(string videoPath)
	{
		try
		{
			if (!File.Exists(videoPath))
			{
				_logger.LogWarning($"Video file not found: {videoPath}");
				return null;
			}

			// Video thumbnail generation requires platform-specific implementation
			// or third-party libraries like FFmpeg
			_logger.LogWarning("Video thumbnail generation requires platform-specific implementation");

			// Return null for now
			return null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate video thumbnail");
			return null;
		}
	}

	/// <summary>
	/// Compress an image to reduce file size
	/// </summary>
	public async Task<string?> CompressImageAsync(string imagePath, int quality = 70)
	{
		try
		{
			if (!File.Exists(imagePath))
			{
				_logger.LogWarning($"Image file not found: {imagePath}");
				return null;
			}

			if (quality < 0 || quality > 100)
			{
				throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 0 and 100");
			}

			// Image compression requires a library like SkiaSharp or ImageSharp
			// For now, we'll just return the original path
			_logger.LogWarning("Image compression requires third-party library (e.g., SkiaSharp)");
			return imagePath;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to compress image");
			return null;
		}
	}

	/// <summary>
	/// Get media file metadata (dimensions, duration, etc.)
	/// </summary>
	public async Task<Dictionary<string, object>?> GetMediaMetadataAsync(string filePath)
	{
		try
		{
			if (!File.Exists(filePath))
			{
				_logger.LogWarning($"Media file not found: {filePath}");
				return null;
			}

			var metadata = new Dictionary<string, object>();
			var fileInfo = new FileInfo(filePath);

			metadata["fileSize"] = fileInfo.Length;
			metadata["createdAt"] = fileInfo.CreationTimeUtc.ToString("o");
			metadata["modifiedAt"] = fileInfo.LastWriteTimeUtc.ToString("o");
			metadata["extension"] = fileInfo.Extension;

			// Additional metadata extraction would require platform-specific code
			// or third-party libraries for EXIF, dimensions, duration, etc.

			return metadata;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get media metadata");
			return null;
		}
	}

	#region Private Helper Methods

	/// <summary>
	/// Process media file by copying to app storage and extracting metadata
	/// </summary>
	private async Task<CameraResult?> ProcessMediaFileAsync(FileResult file, string mediaType)
	{
		try
		{
			// Generate unique filename
			var extension = Path.GetExtension(file.FileName);
			var newFileName = $"{mediaType}_{Guid.NewGuid()}{extension}";
			var newFilePath = Path.Combine(_mediaStoragePath, newFileName);

			// Copy file to app storage
			using (var sourceStream = await file.OpenReadAsync())
			using (var destStream = File.Create(newFilePath))
			{
				await sourceStream.CopyToAsync(destStream);
			}

			// Get file info
			var fileInfo = new FileInfo(newFilePath);

			// Get metadata
			var metadata = await GetMediaMetadataAsync(newFilePath);

			// Generate thumbnail for images
			string? thumbnailPath = null;
			if (mediaType == "photo" && file.ContentType?.StartsWith("image/") == true)
			{
				thumbnailPath = await GenerateImageThumbnailAsync(newFilePath);
			}

			_logger.LogInformation($"Processed {mediaType} file: {newFileName} ({fileInfo.Length} bytes)");

			return new CameraResult
			{
				FilePath = newFilePath,
				FileName = newFileName,
				ContentType = file.ContentType ?? "application/octet-stream",
				FileSize = fileInfo.Length,
				ThumbnailPath = thumbnailPath,
				Metadata = metadata,
				Success = true
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"Failed to process {mediaType} file");
			return null;
		}
	}

	/// <summary>
	/// Map MAUI permission status to CameraPermissionStatus
	/// </summary>
	private CameraPermissionStatus MapPermissionStatus(PermissionStatus status)
	{
		return status switch
		{
			PermissionStatus.Granted => CameraPermissionStatus.Granted,
			PermissionStatus.Denied => CameraPermissionStatus.Denied,
			PermissionStatus.Disabled => CameraPermissionStatus.Denied,
			PermissionStatus.Restricted => CameraPermissionStatus.Restricted,
			PermissionStatus.Limited => CameraPermissionStatus.Granted,
			_ => CameraPermissionStatus.Unknown
		};
	}

	#endregion
}
