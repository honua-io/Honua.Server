using HonuaField.Data;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using Xunit;
using FluentAssertions;

namespace HonuaField.Tests.Data.Repositories;

/// <summary>
/// Unit tests for AttachmentRepository
/// </summary>
public class AttachmentRepositoryTests : IAsyncLifetime
{
	private HonuaFieldDatabase? _database;
	private AttachmentRepository? _repository;
	private readonly string _testDbPath;

	public AttachmentRepositoryTests()
	{
		_testDbPath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
	}

	public async Task InitializeAsync()
	{
		_database = new HonuaFieldDatabase(_testDbPath);
		await _database.InitializeAsync();
		_repository = new AttachmentRepository(_database);
	}

	public async Task DisposeAsync()
	{
		if (_database != null)
			await _database.CloseAsync();

		if (File.Exists(_testDbPath))
			File.Delete(_testDbPath);
	}

	[Fact]
	public async Task InsertAsync_ShouldInsertAttachment_AndReturnId()
	{
		// Arrange
		var attachment = new Attachment
		{
			FeatureId = "feature1",
			Filename = "test.jpg",
			Filepath = "/path/to/test.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		};

		// Act
		var id = await _repository!.InsertAsync(attachment);

		// Assert
		id.Should().NotBeNullOrEmpty();
		attachment.Id.Should().Be(id);
	}

	[Fact]
	public async Task GetByFeatureIdAsync_ShouldReturnAttachmentsForFeature()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "feature1",
			Filename = "photo1.jpg",
			Filepath = "/path/photo1.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "feature1",
			Filename = "photo2.jpg",
			Filepath = "/path/photo2.jpg",
			ContentType = "image/jpeg",
			Size = 2048
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "feature2",
			Filename = "photo3.jpg",
			Filepath = "/path/photo3.jpg",
			ContentType = "image/jpeg",
			Size = 512
		});

		// Act
		var attachments = await _repository.GetByFeatureIdAsync("feature1");

		// Assert
		attachments.Should().HaveCount(2);
		attachments.Should().AllSatisfy(a => a.FeatureId.Should().Be("feature1"));
	}

	[Fact]
	public async Task DeleteByFeatureIdAsync_ShouldDeleteAllAttachmentsForFeature()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "feature1",
			Filename = "photo1.jpg",
			Filepath = "/path/photo1.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "feature1",
			Filename = "photo2.jpg",
			Filepath = "/path/photo2.jpg",
			ContentType = "image/jpeg",
			Size = 2048
		});

		// Act
		var result = await _repository.DeleteByFeatureIdAsync("feature1");

		// Assert
		result.Should().Be(2);
		var remaining = await _repository.GetByFeatureIdAsync("feature1");
		remaining.Should().BeEmpty();
	}

	[Fact]
	public async Task GetByUploadStatusAsync_ShouldReturnAttachmentsByStatus()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "pending.jpg",
			Filepath = "/path/pending.jpg",
			ContentType = "image/jpeg",
			Size = 1024,
			UploadStatus = UploadStatus.Pending.ToString()
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "uploaded.jpg",
			Filepath = "/path/uploaded.jpg",
			ContentType = "image/jpeg",
			Size = 2048,
			UploadStatus = UploadStatus.Uploaded.ToString()
		});

		// Act
		var pending = await _repository.GetByUploadStatusAsync(UploadStatus.Pending);

		// Assert
		pending.Should().HaveCount(1);
		pending[0].Filename.Should().Be("pending.jpg");
	}

	[Fact]
	public async Task UpdateUploadStatusAsync_ShouldUpdateStatus()
	{
		// Arrange
		var attachment = new Attachment
		{
			FeatureId = "f1",
			Filename = "test.jpg",
			Filepath = "/path/test.jpg",
			ContentType = "image/jpeg",
			Size = 1024,
			UploadStatus = UploadStatus.Pending.ToString()
		};
		var id = await _repository!.InsertAsync(attachment);

		// Act
		await _repository.UpdateUploadStatusAsync(id, UploadStatus.Uploaded);

		// Assert
		var updated = await _repository.GetByIdAsync(id);
		updated!.UploadStatus.Should().Be(UploadStatus.Uploaded.ToString());
	}

	[Fact]
	public async Task GetByTypeAsync_ShouldReturnAttachmentsByType()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Type = AttachmentType.Photo.ToString(),
			Filename = "photo.jpg",
			Filepath = "/path/photo.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Type = AttachmentType.Video.ToString(),
			Filename = "video.mp4",
			Filepath = "/path/video.mp4",
			ContentType = "video/mp4",
			Size = 10240
		});

		// Act
		var photos = await _repository.GetByTypeAsync(AttachmentType.Photo);

		// Assert
		photos.Should().HaveCount(1);
		photos[0].Type.Should().Be(AttachmentType.Photo.ToString());
	}

	[Fact]
	public async Task GetByFeatureAndTypeAsync_ShouldReturnFilteredAttachments()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Type = AttachmentType.Photo.ToString(),
			Filename = "photo1.jpg",
			Filepath = "/path/photo1.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Type = AttachmentType.Video.ToString(),
			Filename = "video.mp4",
			Filepath = "/path/video.mp4",
			ContentType = "video/mp4",
			Size = 10240
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f2",
			Type = AttachmentType.Photo.ToString(),
			Filename = "photo2.jpg",
			Filepath = "/path/photo2.jpg",
			ContentType = "image/jpeg",
			Size = 2048
		});

		// Act
		var photos = await _repository.GetByFeatureAndTypeAsync("f1", AttachmentType.Photo);

		// Assert
		photos.Should().HaveCount(1);
		photos[0].FeatureId.Should().Be("f1");
		photos[0].Type.Should().Be(AttachmentType.Photo.ToString());
	}

	[Fact]
	public async Task GetTotalSizeAsync_ShouldReturnSumOfAllSizes()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "file1.jpg",
			Filepath = "/path/file1.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "file2.jpg",
			Filepath = "/path/file2.jpg",
			ContentType = "image/jpeg",
			Size = 2048
		});

		// Act
		var totalSize = await _repository.GetTotalSizeAsync();

		// Assert
		totalSize.Should().Be(3072);
	}

	[Fact]
	public async Task GetTotalSizeByFeatureAsync_ShouldReturnSumForFeature()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "file1.jpg",
			Filepath = "/path/file1.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "file2.jpg",
			Filepath = "/path/file2.jpg",
			ContentType = "image/jpeg",
			Size = 2048
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f2",
			Filename = "file3.jpg",
			Filepath = "/path/file3.jpg",
			ContentType = "image/jpeg",
			Size = 512
		});

		// Act
		var featureSize = await _repository.GetTotalSizeByFeatureAsync("f1");

		// Assert
		featureSize.Should().Be(3072);
	}

	[Fact]
	public async Task GetCountByFeatureAsync_ShouldReturnCountForFeature()
	{
		// Arrange
		await _repository!.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "file1.jpg",
			Filepath = "/path/file1.jpg",
			ContentType = "image/jpeg",
			Size = 1024
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f1",
			Filename = "file2.jpg",
			Filepath = "/path/file2.jpg",
			ContentType = "image/jpeg",
			Size = 2048
		});
		await _repository.InsertAsync(new Attachment
		{
			FeatureId = "f2",
			Filename = "file3.jpg",
			Filepath = "/path/file3.jpg",
			ContentType = "image/jpeg",
			Size = 512
		});

		// Act
		var count = await _repository.GetCountByFeatureAsync("f1");

		// Assert
		count.Should().Be(2);
	}
}
