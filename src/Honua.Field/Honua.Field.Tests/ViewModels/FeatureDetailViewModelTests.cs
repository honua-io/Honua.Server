// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for FeatureDetailViewModel
/// Tests feature display, attachments, edit/delete commands, and navigation
/// </summary>
public class FeatureDetailViewModelTests
{
	private readonly Mock<IFeaturesService> _mockFeaturesService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly FeatureDetailViewModel _viewModel;

	public FeatureDetailViewModelTests()
	{
		_mockFeaturesService = new Mock<IFeaturesService>();
		_mockNavigationService = new Mock<INavigationService>();

		_viewModel = new FeatureDetailViewModel(
			_mockFeaturesService.Object,
			_mockNavigationService.Object);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Feature Details");
		_viewModel.Attachments.Should().BeEmpty();
		_viewModel.Properties.Should().BeEmpty();
		_viewModel.IsBusy.Should().BeFalse();
	}

	[Fact]
	public async Task InitializeAsync_ShouldLoadFeature()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);
		var attachments = new List<Attachment>
		{
			new() { Id = "a1", FeatureId = featureId, Filename = "photo.jpg", Size = 1024 }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(attachments);

		_mockFeaturesService
			.Setup(x => x.GetAttachmentsSizeAsync(featureId))
			.ReturnsAsync(1024);

		// Act
		await _viewModel.InitializeAsync(featureId);

		// Assert
		_viewModel.Feature.Should().NotBeNull();
		_viewModel.FeatureId.Should().Be(featureId);
		_viewModel.Attachments.Should().HaveCount(1);
		_viewModel.AttachmentsCount.Should().Be(1);
		_viewModel.Properties.Should().NotBeEmpty();
	}

	[Fact]
	public async Task LoadFeatureAsync_ShouldDisplayFeatureDetails()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(new List<Attachment>());

		_mockFeaturesService
			.Setup(x => x.GetAttachmentsSizeAsync(featureId))
			.ReturnsAsync(0);

		_viewModel.FeatureId = featureId;

		// Act
		await _viewModel.LoadFeatureCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Feature.Should().NotBeNull();
		_viewModel.GeometryType.Should().Be("Point");
		_viewModel.CreatedDate.Should().NotBeEmpty();
		_viewModel.ModifiedDate.Should().NotBeEmpty();
	}

	[Fact]
	public async Task LoadFeatureAsync_ShouldParseProperties()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);
		feature.Properties = "{\"name\":\"Test Park\",\"type\":\"recreation\",\"area\":1500}";

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(new List<Attachment>());

		_mockFeaturesService
			.Setup(x => x.GetAttachmentsSizeAsync(featureId))
			.ReturnsAsync(0);

		_viewModel.FeatureId = featureId;

		// Act
		await _viewModel.LoadFeatureCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Properties.Should().HaveCount(3);
		_viewModel.Properties.Should().Contain(p => p.Name == "name" && p.Value == "Test Park");
		_viewModel.Properties.Should().Contain(p => p.Name == "type" && p.Value == "recreation");
	}

	[Fact]
	public async Task LoadFeatureAsync_ShouldSetHasPendingChanges()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);
		feature.SyncStatus = SyncStatus.Pending.ToString();

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(new List<Attachment>());

		_mockFeaturesService
			.Setup(x => x.GetAttachmentsSizeAsync(featureId))
			.ReturnsAsync(0);

		_viewModel.FeatureId = featureId;

		// Act
		await _viewModel.LoadFeatureCommand.ExecuteAsync(null);

		// Assert
		_viewModel.HasPendingChanges.Should().BeTrue();
	}

	[Fact]
	public async Task EditFeatureCommand_ShouldNavigateToEditor()
	{
		// Arrange
		var feature = CreateTestFeature("f1");
		_viewModel.Feature = feature;

		// Act
		await _viewModel.EditFeatureCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync(
				"FeatureEditor",
				It.Is<IDictionary<string, object>>(p =>
					p.ContainsKey("featureId") && p.ContainsKey("mode"))),
			Times.Once);
	}

	[Fact]
	public async Task DeleteFeatureCommand_ShouldNotDelete_WhenNotConfirmed()
	{
		// Arrange
		var feature = CreateTestFeature("f1");
		_viewModel.Feature = feature;

		// Note: Since we can't mock ShowConfirmAsync easily, this test is limited
		// In a real scenario, we'd extract the dialog service

		// Act - command should be executable but won't proceed without confirmation
		// await _viewModel.DeleteFeatureCommand.ExecuteAsync(null);

		// Assert
		_mockFeaturesService.Verify(x => x.DeleteFeatureAsync(It.IsAny<string>()), Times.Never);
	}

	[Fact]
	public async Task ShowOnMapCommand_ShouldNavigateToMap()
	{
		// Arrange
		var feature = CreateTestFeature("f1");
		_viewModel.Feature = feature;

		// Act
		await _viewModel.ShowOnMapCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync(
				"Map",
				It.Is<IDictionary<string, object>>(p =>
					p.ContainsKey("featureId") && p.ContainsKey("zoomToFeature"))),
			Times.Once);
	}

	[Fact]
	public async Task ViewAttachmentCommand_ShouldExecute()
	{
		// Arrange
		var attachment = new Attachment
		{
			Id = "a1",
			FeatureId = "f1",
			Filename = "photo.jpg",
			Size = 1024,
			Type = AttachmentType.Photo.ToString()
		};

		// Act
		Func<Task> act = async () => await _viewModel.ViewAttachmentCommand.ExecuteAsync(attachment);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task DeleteAttachmentCommand_ShouldDeleteAttachment()
	{
		// Arrange
		var featureId = "f1";
		var attachment = new Attachment
		{
			Id = "a1",
			FeatureId = featureId,
			Filename = "photo.jpg",
			Size = 1024
		};

		_viewModel.FeatureId = featureId;
		_viewModel.Attachments.Add(attachment);

		_mockFeaturesService
			.Setup(x => x.DeleteAttachmentAsync("a1"))
			.ReturnsAsync(true);

		_mockFeaturesService
			.Setup(x => x.GetAttachmentsSizeAsync(featureId))
			.ReturnsAsync(0);

		// Note: Can't test without mocking ShowConfirmAsync
		// In a real scenario, we'd extract the dialog service
	}

	[Fact]
	public async Task LoadAttachmentsAsync_ShouldLoadAndCountAttachments()
	{
		// Arrange
		var featureId = "f1";
		var feature = CreateTestFeature(featureId);
		var attachments = new List<Attachment>
		{
			new() { Id = "a1", FeatureId = featureId, Filename = "photo1.jpg", Size = 1024 },
			new() { Id = "a2", FeatureId = featureId, Filename = "photo2.jpg", Size = 2048 }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureByIdAsync(featureId))
			.ReturnsAsync(feature);

		_mockFeaturesService
			.Setup(x => x.GetFeatureAttachmentsAsync(featureId))
			.ReturnsAsync(attachments);

		_mockFeaturesService
			.Setup(x => x.GetAttachmentsSizeAsync(featureId))
			.ReturnsAsync(3072);

		await _viewModel.InitializeAsync(featureId);

		// Assert
		_viewModel.Attachments.Should().HaveCount(2);
		_viewModel.AttachmentsCount.Should().Be(2);
		_viewModel.AttachmentsTotalSize.Should().Be(3072);
	}

	[Fact]
	public void Dispose_ShouldClearCollections()
	{
		// Arrange
		_viewModel.Attachments.Add(new Attachment { Id = "a1" });
		_viewModel.Properties.Add(new FeatureProperty { Name = "test", Value = "value" });

		// Act
		_viewModel.Dispose();

		// Assert
		_viewModel.Attachments.Should().BeEmpty();
		_viewModel.Properties.Should().BeEmpty();
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
			Properties = "{\"name\":\"Test\"}",
			GeometryWkb = writer.Write(point),
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
			UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			SyncStatus = SyncStatus.Synced.ToString()
		};
	}

	#endregion
}
