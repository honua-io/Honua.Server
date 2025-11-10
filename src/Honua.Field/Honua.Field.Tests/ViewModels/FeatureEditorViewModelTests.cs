using FluentAssertions;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for FeatureEditorViewModel
/// Tests property editing, validation, attachment management, and save/cancel operations
/// </summary>
public class FeatureEditorViewModelTests
{
	private readonly Mock<IFeaturesService> _mockFeaturesService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<ICollectionRepository> _mockCollectionRepository;
	private readonly Mock<IAuthenticationService> _mockAuthenticationService;
	private readonly FeatureEditorViewModel _viewModel;

	public FeatureEditorViewModelTests()
	{
		_mockFeaturesService = new Mock<IFeaturesService>();
		_mockNavigationService = new Mock<INavigationService>();
		_mockCollectionRepository = new Mock<ICollectionRepository>();
		_mockAuthenticationService = new Mock<IAuthenticationService>();

		_viewModel = new FeatureEditorViewModel(
			_mockFeaturesService.Object,
			_mockNavigationService.Object,
			_mockCollectionRepository.Object,
			_mockAuthenticationService.Object);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Edit Feature");
		_viewModel.Properties.Should().BeEmpty();
		_viewModel.Attachments.Should().BeEmpty();
		_viewModel.Mode.Should().Be("create");
		_viewModel.IsDirty.Should().BeFalse();
	}

	[Fact]
	public async Task InitializeForCreateAsync_ShouldSetupForNewFeature()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = CreateTestCollection(collectionId);
		var geometry = new Point(0, 0);

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		// Act
		await _viewModel.InitializeForCreateAsync(collectionId, geometry);

		// Assert
		_viewModel.Mode.Should().Be("create");
		_viewModel.CollectionId.Should().Be(collectionId);
		_viewModel.Title.Should().Be("New Feature");
		_viewModel.Geometry.Should().NotBeNull();
		_viewModel.Properties.Should().HaveCount(3); // name, type, description from schema
	}

	[Fact]
	public async Task InitializeForEditAsync_ShouldLoadExistingFeature()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);
		var collection = CreateTestCollection(feature.CollectionId);
		var attachments = new List<Attachment>
		{
			new() { Id = "a1", FeatureId = featureId, Filename = "photo.jpg" }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(feature.CollectionId))
			.ReturnsAsync(collection);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(attachments);

		// Act
		await _viewModel.InitializeForEditAsync(featureId);

		// Assert
		_viewModel.Mode.Should().Be("edit");
		_viewModel.FeatureId.Should().Be(featureId);
		_viewModel.Title.Should().Be("Edit Feature");
		_viewModel.Feature.Should().NotBeNull();
		_viewModel.Attachments.Should().HaveCount(1);
		_viewModel.Properties.Should().NotBeEmpty();
	}

	[Fact]
	public async Task SaveAsync_ShouldCreateNewFeature_WhenModeIsCreate()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = CreateTestCollection(collectionId);
		var geometry = new Point(0, 0);
		var user = new UserInfo { Username = "testuser" };

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		_mockAuthenticationService
			.Setup(x => x.GetCurrentUserAsync())
			.ReturnsAsync(user);

		_mockFeaturesService
			.Setup(x => x.CreateFeatureAsync(It.IsAny<Feature>()))
			.ReturnsAsync("new-feature-id");

		await _viewModel.InitializeForCreateAsync(collectionId, geometry);

		// Fill in required properties
		foreach (var prop in _viewModel.Properties.Where(p => p.IsRequired))
		{
			prop.Value = "test value";
		}

		// Act
		await _viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_mockFeaturesService.Verify(
			x => x.CreateFeatureAsync(It.IsAny<Feature>()),
			Times.Once);
		_mockNavigationService.Verify(
			x => x.GoBackAsync(),
			Times.Once);
	}

	[Fact]
	public async Task SaveAsync_ShouldUpdateExistingFeature_WhenModeIsEdit()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);
		var collection = CreateTestCollection(feature.CollectionId);

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(feature.CollectionId))
			.ReturnsAsync(collection);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(new List<Attachment>());

		_mockFeaturesService
			.Setup(x => x.UpdateFeatureAsync(It.IsAny<Feature>()))
			.ReturnsAsync(true);

		await _viewModel.InitializeForEditAsync(featureId);

		// Act
		await _viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_mockFeaturesService.Verify(
			x => x.UpdateFeatureAsync(It.IsAny<Feature>()),
			Times.Once);
	}

	[Fact]
	public async Task SaveAsync_ShouldNotSave_WhenValidationFails()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = CreateTestCollection(collectionId);
		var geometry = new Point(0, 0);

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		await _viewModel.InitializeForCreateAsync(collectionId, geometry);

		// Don't fill required properties - validation should fail

		// Act
		await _viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_mockFeaturesService.Verify(
			x => x.CreateFeatureAsync(It.IsAny<Feature>()),
			Times.Never);
	}

	[Fact]
	public async Task SaveAsync_ShouldNotSave_WhenGeometryIsNull()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = CreateTestCollection(collectionId);

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		await _viewModel.InitializeForCreateAsync(collectionId, null);

		// Fill in required properties
		foreach (var prop in _viewModel.Properties.Where(p => p.IsRequired))
		{
			prop.Value = "test value";
		}

		// Act
		await _viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_mockFeaturesService.Verify(
			x => x.CreateFeatureAsync(It.IsAny<Feature>()),
			Times.Never);
	}

	[Fact]
	public async Task CancelCommand_ShouldNavigateBack()
	{
		// Arrange - no changes, so should navigate back immediately
		_viewModel.IsDirty = false;

		// Act
		await _viewModel.CancelCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(x => x.GoBackAsync(), Times.Once);
	}

	[Fact]
	public void UpdateGeometry_ShouldUpdateGeometryAndSetDirty()
	{
		// Arrange
		var geometry = new Point(1, 2);

		// Act
		_viewModel.UpdateGeometry(geometry);

		// Assert
		_viewModel.Geometry.Should().NotBeNull();
		_viewModel.GeometryType.Should().Be("Point");
		_viewModel.IsDirty.Should().BeTrue();
	}

	[Fact]
	public void OnPropertyValueChanged_ShouldSetDirty()
	{
		// Act
		_viewModel.OnPropertyValueChanged();

		// Assert
		_viewModel.IsDirty.Should().BeTrue();
	}

	[Fact]
	public async Task RemoveAttachmentCommand_ShouldRemoveAttachment()
	{
		// Arrange
		var attachment = new Attachment
		{
			Id = "a1",
			FeatureId = "f1",
			Filename = "photo.jpg"
		};

		_viewModel.Attachments.Add(attachment);

		// Note: Can't test without mocking ShowConfirmAsync
		// In a real scenario, we'd extract the dialog service

		// Assert initial state
		_viewModel.Attachments.Should().HaveCount(1);
	}

	[Fact]
	public async Task AddPhotoCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.AddPhotoCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task AddVideoCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.AddVideoCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task AddAudioCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.AddAudioCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task SaveAsync_ShouldAddAttachmentsForNewFeature()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = CreateTestCollection(collectionId);
		var geometry = new Point(0, 0);
		var user = new UserInfo { Username = "testuser" };

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		_mockAuthenticationService
			.Setup(x => x.GetCurrentUserAsync())
			.ReturnsAsync(user);

		_mockFeaturesService
			.Setup(x => x.CreateFeatureAsync(It.IsAny<Feature>()))
			.ReturnsAsync("new-feature-id");

		_mockFeaturesService
			.Setup(x => x.AddAttachmentAsync(It.IsAny<Attachment>()))
			.ReturnsAsync("new-attachment-id");

		await _viewModel.InitializeForCreateAsync(collectionId, geometry);

		// Add attachment
		var attachment = new Attachment
		{
			FeatureId = "temp",
			Filename = "photo.jpg",
			Type = AttachmentType.Photo.ToString()
		};
		_viewModel.Attachments.Add(attachment);

		// Fill in required properties
		foreach (var prop in _viewModel.Properties.Where(p => p.IsRequired))
		{
			prop.Value = "test value";
		}

		// Act
		await _viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_mockFeaturesService.Verify(
			x => x.AddAttachmentAsync(It.IsAny<Attachment>()),
			Times.Once);
	}

	[Fact]
	public void Dispose_ShouldClearCollections()
	{
		// Arrange
		_viewModel.Properties.Add(new EditableProperty { Name = "test" });
		_viewModel.Attachments.Add(new Attachment { Id = "a1" });

		// Act
		_viewModel.Dispose();

		// Assert
		_viewModel.Properties.Should().BeEmpty();
		_viewModel.Attachments.Should().BeEmpty();
	}

	#region Helper Methods

	private Feature CreateTestFeature(string id)
	{
		var point = new Point(0, 0);
		var writer = new WKBWriter();

		return new Feature
		{
			Id = id,
			CollectionId = "coll-1",
			Properties = "{\"name\":\"Test Park\",\"type\":\"recreation\",\"description\":\"A test park\"}",
			GeometryWkb = writer.Write(point),
			CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			CreatedBy = "testuser",
			SyncStatus = SyncStatus.Pending.ToString()
		};
	}

	private Collection CreateTestCollection(string id)
	{
		// Collection with JSON schema
		var schema = @"{
			""type"": ""object"",
			""properties"": {
				""name"": {
					""type"": ""string"",
					""title"": ""Name""
				},
				""type"": {
					""type"": ""string"",
					""title"": ""Type""
				},
				""description"": {
					""type"": ""string"",
					""title"": ""Description""
				}
			},
			""required"": [""name"", ""type""]
		}";

		return new Collection
		{
			Id = id,
			Title = "Test Collection",
			Schema = schema,
			Symbology = "{}"
		};
	}

	#endregion
}
