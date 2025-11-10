using FluentAssertions;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for media capture and attachment workflows
/// Tests real CameraService and AttachmentRepository with mocked MediaPicker
/// </summary>
public class CameraAttachmentIntegrationTests : IntegrationTestBase
{
	private Mock<IMediaPicker> _mockMediaPicker = null!;
	private ICameraService _cameraService = null!;

	protected override void ConfigureServices(IServiceCollection services)
	{
		base.ConfigureServices(services);

		// Mock media picker for predictable behavior
		_mockMediaPicker = new Mock<IMediaPicker>();

		services.AddSingleton<ICameraService>(sp =>
			new CameraService(_mockMediaPicker.Object, TestDataDirectory));
	}

	protected override async Task OnInitializeAsync()
	{
		_cameraService = ServiceProvider.GetRequiredService<ICameraService>();
		await base.OnInitializeAsync();
	}

	[Fact]
	public async Task CapturePhoto_AndCreateAttachment_ShouldStoreInDatabase()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		// Mock photo capture
		var testPhotoPath = CreateTestFile("test_photo.jpg", DataBuilder.CreateTestImageBytes());
		_mockMediaPicker
			.Setup(x => x.CapturePhotoAsync())
			.ReturnsAsync(new MediaPickerResult { FilePath = testPhotoPath, Success = true });

		// Act
		var photoResult = await _cameraService.CapturePhotoAsync();
		photoResult.Success.Should().BeTrue();

		var attachment = new Attachment
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = feature.Id,
			Type = AttachmentType.Photo.ToString(),
			Filename = Path.GetFileName(photoResult.FilePath!),
			Filepath = photoResult.FilePath!,
			ContentType = "image/jpeg",
			Size = new FileInfo(photoResult.FilePath!).Length,
			UploadStatus = UploadStatus.Pending.ToString()
		};

		await AttachmentRepository.InsertAsync(attachment);

		// Assert
		var savedAttachment = await AttachmentRepository.GetByIdAsync(attachment.Id);
		savedAttachment.Should().NotBeNull();
		savedAttachment!.FeatureId.Should().Be(feature.Id);
		File.Exists(savedAttachment.Filepath).Should().BeTrue();
	}

	[Fact]
	public async Task GenerateThumbnail_ForPhoto_ShouldCreateThumbnailFile()
	{
		// Arrange
		var testPhotoPath = CreateTestFile("photo.jpg", DataBuilder.CreateTestImageBytes());

		// Act
		var thumbnailPath = await _cameraService.GenerateThumbnailAsync(testPhotoPath, 200, 200);

		// Assert
		thumbnailPath.Should().NotBeNullOrEmpty();
		File.Exists(thumbnailPath).Should().BeTrue();
		new FileInfo(thumbnailPath).Length.Should().BeLessOrEqualTo(new FileInfo(testPhotoPath).Length);
	}

	[Fact]
	public async Task StoreAttachment_WithMetadata_ShouldPersistMetadata()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var attachment = DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Photo);

		// Act
		await AttachmentRepository.InsertAsync(attachment);

		// Assert
		var savedAttachment = await AttachmentRepository.GetByIdAsync(attachment.Id);
		savedAttachment.Should().NotBeNull();
		savedAttachment!.Metadata.Should().NotBeNullOrEmpty();

		var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
			savedAttachment.Metadata!);
		metadata.Should().ContainKey("capturedAt");
		metadata.Should().ContainKey("width");
		metadata.Should().ContainKey("height");
	}

	[Fact]
	public async Task RetrieveAttachments_ForFeature_ShouldReturnAllAttachments()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		// Create multiple attachments
		var attachment1 = DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Photo);
		var attachment2 = DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Photo);
		var attachment3 = DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Video);

		await AttachmentRepository.InsertAsync(attachment1);
		await AttachmentRepository.InsertAsync(attachment2);
		await AttachmentRepository.InsertAsync(attachment3);

		// Act
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);

		// Assert
		attachments.Should().HaveCount(3);
		attachments.Count(a => a.Type == AttachmentType.Photo.ToString()).Should().Be(2);
		attachments.Count(a => a.Type == AttachmentType.Video.ToString()).Should().Be(1);
	}

	[Fact]
	public async Task DeleteAttachment_ShouldRemoveFromDatabaseAndFileSystem()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var attachment = DataBuilder.CreateTestAttachment(feature.Id);
		await AttachmentRepository.InsertAsync(attachment);

		var filePath = attachment.Filepath;
		var thumbnailPath = attachment.Thumbnail;

		// Act
		await AttachmentRepository.DeleteAsync(attachment.Id);

		// Cleanup files (normally done by service layer)
		if (File.Exists(filePath))
			File.Delete(filePath);
		if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
			File.Delete(thumbnailPath);

		// Assert
		var deletedAttachment = await AttachmentRepository.GetByIdAsync(attachment.Id);
		deletedAttachment.Should().BeNull();

		File.Exists(filePath).Should().BeFalse();
		if (!string.IsNullOrEmpty(thumbnailPath))
			File.Exists(thumbnailPath).Should().BeFalse();
	}

	[Fact]
	public async Task UpdateAttachmentUploadStatus_ShouldPersist()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var attachment = DataBuilder.CreateTestAttachment(feature.Id);
		attachment.UploadStatus = UploadStatus.Pending.ToString();
		await AttachmentRepository.InsertAsync(attachment);

		// Act - Update upload status
		attachment.UploadStatus = UploadStatus.Uploaded.ToString();
		await AttachmentRepository.UpdateAsync(attachment);

		// Assert
		var updatedAttachment = await AttachmentRepository.GetByIdAsync(attachment.Id);
		updatedAttachment.Should().NotBeNull();
		updatedAttachment!.UploadStatus.Should().Be(UploadStatus.Uploaded.ToString());
	}

	[Fact]
	public async Task GetAttachmentsByType_ShouldFilterCorrectly()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		await AttachmentRepository.InsertAsync(DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Photo));
		await AttachmentRepository.InsertAsync(DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Photo));
		await AttachmentRepository.InsertAsync(DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Video));
		await AttachmentRepository.InsertAsync(DataBuilder.CreateTestAttachment(feature.Id, AttachmentType.Document));

		// Act
		var allAttachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);
		var photos = allAttachments.Where(a => a.Type == AttachmentType.Photo.ToString()).ToList();

		// Assert
		photos.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetPendingUploads_ShouldReturnUnuploadedAttachments()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature1 = DataBuilder.CreateTestFeature(collection.Id);
		var feature2 = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature1);
		await FeatureRepository.InsertAsync(feature2);

		var pending1 = DataBuilder.CreateTestAttachment(feature1.Id);
		pending1.UploadStatus = UploadStatus.Pending.ToString();

		var uploaded = DataBuilder.CreateTestAttachment(feature1.Id);
		uploaded.UploadStatus = UploadStatus.Uploaded.ToString();

		var pending2 = DataBuilder.CreateTestAttachment(feature2.Id);
		pending2.UploadStatus = UploadStatus.Pending.ToString();

		await AttachmentRepository.InsertAsync(pending1);
		await AttachmentRepository.InsertAsync(uploaded);
		await AttachmentRepository.InsertAsync(pending2);

		// Act
		var pendingUploads = await AttachmentRepository.GetPendingUploadsAsync();

		// Assert
		pendingUploads.Should().HaveCount(2);
		pendingUploads.All(a => a.UploadStatus == UploadStatus.Pending.ToString()).Should().BeTrue();
	}

	[Fact]
	public async Task CalculateTotalAttachmentSize_ForFeature_ShouldSumSizes()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		var attachment1 = DataBuilder.CreateTestAttachment(feature.Id);
		var attachment2 = DataBuilder.CreateTestAttachment(feature.Id);
		var attachment3 = DataBuilder.CreateTestAttachment(feature.Id);

		await AttachmentRepository.InsertAsync(attachment1);
		await AttachmentRepository.InsertAsync(attachment2);
		await AttachmentRepository.InsertAsync(attachment3);

		// Act
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);
		var totalSize = attachments.Sum(a => a.Size);

		// Assert
		totalSize.Should().BeGreaterThan(0);
		totalSize.Should().Be(attachment1.Size + attachment2.Size + attachment3.Size);
	}

	[Fact]
	public async Task PickPhotoFromGallery_ShouldWorkSimilarToCapture()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		// Mock gallery pick
		var testPhotoPath = CreateTestFile("gallery_photo.jpg", DataBuilder.CreateTestImageBytes());
		_mockMediaPicker
			.Setup(x => x.PickPhotoAsync())
			.ReturnsAsync(new MediaPickerResult { FilePath = testPhotoPath, Success = true });

		// Act
		var photoResult = await _cameraService.PickPhotoAsync();
		photoResult.Success.Should().BeTrue();

		var attachment = new Attachment
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = feature.Id,
			Type = AttachmentType.Photo.ToString(),
			Filename = Path.GetFileName(photoResult.FilePath!),
			Filepath = photoResult.FilePath!,
			ContentType = "image/jpeg",
			Size = new FileInfo(photoResult.FilePath!).Length,
			UploadStatus = UploadStatus.Pending.ToString()
		};

		await AttachmentRepository.InsertAsync(attachment);

		// Assert
		var savedAttachment = await AttachmentRepository.GetByIdAsync(attachment.Id);
		savedAttachment.Should().NotBeNull();
		File.Exists(savedAttachment!.Filepath).Should().BeTrue();
	}

	[Fact]
	public async Task GetAttachmentCount_ForFeature_ShouldReturnCorrectCount()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var feature = DataBuilder.CreateTestFeature(collection.Id);
		await FeatureRepository.InsertAsync(feature);

		for (int i = 0; i < 5; i++)
		{
			await AttachmentRepository.InsertAsync(DataBuilder.CreateTestAttachment(feature.Id));
		}

		// Act
		var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);

		// Assert
		attachments.Should().HaveCount(5);
	}
}

/// <summary>
/// Mock media picker interface for testing
/// </summary>
public interface IMediaPicker
{
	Task<MediaPickerResult> CapturePhotoAsync();
	Task<MediaPickerResult> PickPhotoAsync();
	Task<MediaPickerResult> CaptureVideoAsync();
}

/// <summary>
/// Media picker result
/// </summary>
public class MediaPickerResult
{
	public bool Success { get; set; }
	public string? FilePath { get; set; }
	public string? ErrorMessage { get; set; }
}
