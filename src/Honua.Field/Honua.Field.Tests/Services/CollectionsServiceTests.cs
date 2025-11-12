// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using Moq;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for CollectionsService
/// Tests CRUD operations, validation, and statistics
/// </summary>
public class CollectionsServiceTests
{
	private readonly Mock<ICollectionRepository> _mockCollectionRepository;
	private readonly Mock<IFeatureRepository> _mockFeatureRepository;
	private readonly CollectionsService _collectionsService;

	public CollectionsServiceTests()
	{
		_mockCollectionRepository = new Mock<ICollectionRepository>();
		_mockFeatureRepository = new Mock<IFeatureRepository>();
		_collectionsService = new CollectionsService(
			_mockCollectionRepository.Object,
			_mockFeatureRepository.Object);
	}

	#region GetByIdAsync Tests

	[Fact]
	public async Task GetByIdAsync_ShouldReturnCollection_WhenCollectionExists()
	{
		// Arrange
		var collectionId = "test-collection-id";
		var collection = new Collection
		{
			Id = collectionId,
			Title = "Test Collection",
			Description = "Test Description",
			Schema = "{}",
			Symbology = "{}"
		};

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		// Act
		var result = await _collectionsService.GetByIdAsync(collectionId);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(collectionId);
		result.Title.Should().Be("Test Collection");
		_mockCollectionRepository.Verify(x => x.GetByIdAsync(collectionId), Times.Once);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnNull_WhenCollectionDoesNotExist()
	{
		// Arrange
		var collectionId = "non-existent-id";

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync((Collection?)null);

		// Act
		var result = await _collectionsService.GetByIdAsync(collectionId);

		// Assert
		result.Should().BeNull();
		_mockCollectionRepository.Verify(x => x.GetByIdAsync(collectionId), Times.Once);
	}

	[Fact]
	public async Task GetByIdAsync_ShouldReturnNull_WhenIdIsNullOrEmpty()
	{
		// Act
		var resultNull = await _collectionsService.GetByIdAsync(null!);
		var resultEmpty = await _collectionsService.GetByIdAsync("");
		var resultWhitespace = await _collectionsService.GetByIdAsync("   ");

		// Assert
		resultNull.Should().BeNull();
		resultEmpty.Should().BeNull();
		resultWhitespace.Should().BeNull();
		_mockCollectionRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
	}

	#endregion

	#region GetAllAsync Tests

	[Fact]
	public async Task GetAllAsync_ShouldReturnAllCollections()
	{
		// Arrange
		var collections = new List<Collection>
		{
			new Collection { Id = "1", Title = "Collection 1", Schema = "{}", Symbology = "{}" },
			new Collection { Id = "2", Title = "Collection 2", Schema = "{}", Symbology = "{}" },
			new Collection { Id = "3", Title = "Collection 3", Schema = "{}", Symbology = "{}" }
		};

		_mockCollectionRepository
			.Setup(x => x.GetAllAsync())
			.ReturnsAsync(collections);

		// Act
		var result = await _collectionsService.GetAllAsync();

		// Assert
		result.Should().HaveCount(3);
		result.Should().BeEquivalentTo(collections);
		_mockCollectionRepository.Verify(x => x.GetAllAsync(), Times.Once);
	}

	[Fact]
	public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoCollectionsExist()
	{
		// Arrange
		_mockCollectionRepository
			.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());

		// Act
		var result = await _collectionsService.GetAllAsync();

		// Assert
		result.Should().BeEmpty();
		_mockCollectionRepository.Verify(x => x.GetAllAsync(), Times.Once);
	}

	#endregion

	#region CreateAsync Tests

	[Fact]
	public async Task CreateAsync_ShouldCreateCollection_WhenValidCollectionProvided()
	{
		// Arrange
		var collection = new Collection
		{
			Title = "New Collection",
			Description = "Test Description",
			Schema = "{\"type\": \"object\", \"properties\": {}}",
			Symbology = "{\"color\": \"#FF0000\", \"icon\": \"marker\"}"
		};

		var expectedId = "new-collection-id";
		_mockCollectionRepository
			.Setup(x => x.InsertAsync(It.IsAny<Collection>()))
			.ReturnsAsync(expectedId);

		// Act
		var result = await _collectionsService.CreateAsync(collection);

		// Assert
		result.Should().Be(expectedId);
		_mockCollectionRepository.Verify(x => x.InsertAsync(collection), Times.Once);
	}

	[Fact]
	public async Task CreateAsync_ShouldThrowArgumentNullException_WhenCollectionIsNull()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(() =>
			_collectionsService.CreateAsync(null!));

		_mockCollectionRepository.Verify(x => x.InsertAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task CreateAsync_ShouldThrowArgumentException_WhenTitleIsEmpty()
	{
		// Arrange
		var collection = new Collection
		{
			Title = "",
			Schema = "{}",
			Symbology = "{}"
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.CreateAsync(collection));

		exception.Message.Should().Contain("title");
		_mockCollectionRepository.Verify(x => x.InsertAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task CreateAsync_ShouldThrowArgumentException_WhenSchemaIsInvalid()
	{
		// Arrange
		var collection = new Collection
		{
			Title = "Test Collection",
			Schema = "invalid json {",
			Symbology = "{}"
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.CreateAsync(collection));

		exception.Message.Should().Contain("schema");
		_mockCollectionRepository.Verify(x => x.InsertAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task CreateAsync_ShouldThrowArgumentException_WhenSymbologyIsInvalid()
	{
		// Arrange
		var collection = new Collection
		{
			Title = "Test Collection",
			Schema = "{}",
			Symbology = "invalid json ["
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.CreateAsync(collection));

		exception.Message.Should().Contain("symbology");
		_mockCollectionRepository.Verify(x => x.InsertAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task CreateAsync_ShouldSetDefaultSchemaAndSymbology_WhenEmpty()
	{
		// Arrange
		var collection = new Collection
		{
			Title = "Test Collection",
			Schema = "",
			Symbology = ""
		};

		_mockCollectionRepository
			.Setup(x => x.InsertAsync(It.IsAny<Collection>()))
			.ReturnsAsync("new-id");

		// Act
		await _collectionsService.CreateAsync(collection);

		// Assert
		collection.Schema.Should().Be("{}");
		collection.Symbology.Should().Be("{}");
	}

	#endregion

	#region UpdateAsync Tests

	[Fact]
	public async Task UpdateAsync_ShouldUpdateCollection_WhenValidCollectionProvided()
	{
		// Arrange
		var collection = new Collection
		{
			Id = "test-id",
			Title = "Updated Collection",
			Schema = "{\"type\": \"object\"}",
			Symbology = "{\"color\": \"#00FF00\"}"
		};

		_mockCollectionRepository
			.Setup(x => x.UpdateAsync(It.IsAny<Collection>()))
			.ReturnsAsync(1);

		// Act
		var result = await _collectionsService.UpdateAsync(collection);

		// Assert
		result.Should().Be(1);
		_mockCollectionRepository.Verify(x => x.UpdateAsync(collection), Times.Once);
	}

	[Fact]
	public async Task UpdateAsync_ShouldThrowArgumentNullException_WhenCollectionIsNull()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(() =>
			_collectionsService.UpdateAsync(null!));

		_mockCollectionRepository.Verify(x => x.UpdateAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task UpdateAsync_ShouldThrowArgumentException_WhenIdIsEmpty()
	{
		// Arrange
		var collection = new Collection
		{
			Id = "",
			Title = "Test Collection",
			Schema = "{}",
			Symbology = "{}"
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.UpdateAsync(collection));

		exception.Message.Should().Contain("ID");
		_mockCollectionRepository.Verify(x => x.UpdateAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task UpdateAsync_ShouldThrowArgumentException_WhenTitleIsEmpty()
	{
		// Arrange
		var collection = new Collection
		{
			Id = "test-id",
			Title = "",
			Schema = "{}",
			Symbology = "{}"
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.UpdateAsync(collection));

		exception.Message.Should().Contain("title");
		_mockCollectionRepository.Verify(x => x.UpdateAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task UpdateAsync_ShouldThrowArgumentException_WhenSchemaIsInvalid()
	{
		// Arrange
		var collection = new Collection
		{
			Id = "test-id",
			Title = "Test Collection",
			Schema = "not valid json",
			Symbology = "{}"
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.UpdateAsync(collection));

		exception.Message.Should().Contain("schema");
		_mockCollectionRepository.Verify(x => x.UpdateAsync(It.IsAny<Collection>()), Times.Never);
	}

	[Fact]
	public async Task UpdateAsync_ShouldReturnZero_WhenCollectionDoesNotExist()
	{
		// Arrange
		var collection = new Collection
		{
			Id = "non-existent-id",
			Title = "Test Collection",
			Schema = "{}",
			Symbology = "{}"
		};

		_mockCollectionRepository
			.Setup(x => x.UpdateAsync(It.IsAny<Collection>()))
			.ReturnsAsync(0);

		// Act
		var result = await _collectionsService.UpdateAsync(collection);

		// Assert
		result.Should().Be(0);
	}

	#endregion

	#region DeleteAsync Tests

	[Fact]
	public async Task DeleteAsync_ShouldDeleteCollection_WhenCollectionExists()
	{
		// Arrange
		var collectionId = "test-id";

		_mockCollectionRepository
			.Setup(x => x.DeleteAsync(collectionId))
			.ReturnsAsync(1);

		// Act
		var result = await _collectionsService.DeleteAsync(collectionId);

		// Assert
		result.Should().Be(1);
		_mockCollectionRepository.Verify(x => x.DeleteAsync(collectionId), Times.Once);
	}

	[Fact]
	public async Task DeleteAsync_ShouldReturnZero_WhenCollectionDoesNotExist()
	{
		// Arrange
		var collectionId = "non-existent-id";

		_mockCollectionRepository
			.Setup(x => x.DeleteAsync(collectionId))
			.ReturnsAsync(0);

		// Act
		var result = await _collectionsService.DeleteAsync(collectionId);

		// Assert
		result.Should().Be(0);
	}

	[Fact]
	public async Task DeleteAsync_ShouldThrowArgumentException_WhenIdIsNullOrEmpty()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.DeleteAsync(null!));

		await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.DeleteAsync(""));

		await Assert.ThrowsAsync<ArgumentException>(() =>
			_collectionsService.DeleteAsync("   "));

		_mockCollectionRepository.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
	}

	#endregion

	#region GetFeatureCountAsync Tests

	[Fact]
	public async Task GetFeatureCountAsync_ShouldReturnFeatureCount_WhenCollectionExists()
	{
		// Arrange
		var collectionId = "test-id";
		var expectedCount = 42;

		_mockFeatureRepository
			.Setup(x => x.GetCountByCollectionAsync(collectionId))
			.ReturnsAsync(expectedCount);

		// Act
		var result = await _collectionsService.GetFeatureCountAsync(collectionId);

		// Assert
		result.Should().Be(expectedCount);
		_mockFeatureRepository.Verify(x => x.GetCountByCollectionAsync(collectionId), Times.Once);
	}

	[Fact]
	public async Task GetFeatureCountAsync_ShouldReturnZero_WhenCollectionHasNoFeatures()
	{
		// Arrange
		var collectionId = "test-id";

		_mockFeatureRepository
			.Setup(x => x.GetCountByCollectionAsync(collectionId))
			.ReturnsAsync(0);

		// Act
		var result = await _collectionsService.GetFeatureCountAsync(collectionId);

		// Assert
		result.Should().Be(0);
	}

	[Fact]
	public async Task GetFeatureCountAsync_ShouldReturnZero_WhenCollectionIdIsNullOrEmpty()
	{
		// Act
		var resultNull = await _collectionsService.GetFeatureCountAsync(null!);
		var resultEmpty = await _collectionsService.GetFeatureCountAsync("");

		// Assert
		resultNull.Should().Be(0);
		resultEmpty.Should().Be(0);
		_mockFeatureRepository.Verify(x => x.GetCountByCollectionAsync(It.IsAny<string>()), Times.Never);
	}

	#endregion

	#region RefreshFeatureCountAsync Tests

	[Fact]
	public async Task RefreshFeatureCountAsync_ShouldUpdateCollectionItemsCount()
	{
		// Arrange
		var collectionId = "test-id";
		var featureCount = 25;

		_mockFeatureRepository
			.Setup(x => x.GetCountByCollectionAsync(collectionId))
			.ReturnsAsync(featureCount);

		_mockCollectionRepository
			.Setup(x => x.UpdateItemsCountAsync(collectionId, featureCount))
			.ReturnsAsync(1);

		// Act
		var result = await _collectionsService.RefreshFeatureCountAsync(collectionId);

		// Assert
		result.Should().Be(1);
		_mockFeatureRepository.Verify(x => x.GetCountByCollectionAsync(collectionId), Times.Once);
		_mockCollectionRepository.Verify(x => x.UpdateItemsCountAsync(collectionId, featureCount), Times.Once);
	}

	[Fact]
	public async Task RefreshFeatureCountAsync_ShouldReturnZero_WhenCollectionIdIsNullOrEmpty()
	{
		// Act
		var result = await _collectionsService.RefreshFeatureCountAsync("");

		// Assert
		result.Should().Be(0);
		_mockFeatureRepository.Verify(x => x.GetCountByCollectionAsync(It.IsAny<string>()), Times.Never);
	}

	#endregion

	#region GetStatsAsync Tests

	[Fact]
	public async Task GetStatsAsync_ShouldReturnStats_WhenCollectionExists()
	{
		// Arrange
		var collectionId = "test-id";
		var collection = new Collection
		{
			Id = collectionId,
			Title = "Test Collection",
			Schema = "{}",
			Symbology = "{}"
		};

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		_mockFeatureRepository
			.Setup(x => x.GetCountByCollectionAsync(collectionId))
			.ReturnsAsync(10);

		_mockFeatureRepository
			.Setup(x => x.GetExtentAsync(collectionId))
			.ReturnsAsync((-180.0, -90.0, 180.0, 90.0));

		// Act
		var result = await _collectionsService.GetStatsAsync(collectionId);

		// Assert
		result.Should().NotBeNull();
		result!.CollectionId.Should().Be(collectionId);
		result.Title.Should().Be("Test Collection");
		result.FeatureCount.Should().Be(10);
		result.Extent.Should().NotBeNull();
		result.Extent!.MinX.Should().Be(-180.0);
		result.Extent.MinY.Should().Be(-90.0);
		result.Extent.MaxX.Should().Be(180.0);
		result.Extent.MaxY.Should().Be(90.0);
	}

	[Fact]
	public async Task GetStatsAsync_ShouldReturnStatsWithoutExtent_WhenNoFeaturesExist()
	{
		// Arrange
		var collectionId = "test-id";
		var collection = new Collection
		{
			Id = collectionId,
			Title = "Empty Collection",
			Schema = "{}",
			Symbology = "{}"
		};

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		_mockFeatureRepository
			.Setup(x => x.GetCountByCollectionAsync(collectionId))
			.ReturnsAsync(0);

		_mockFeatureRepository
			.Setup(x => x.GetExtentAsync(collectionId))
			.ReturnsAsync(((double minX, double minY, double maxX, double maxY)?)null);

		// Act
		var result = await _collectionsService.GetStatsAsync(collectionId);

		// Assert
		result.Should().NotBeNull();
		result!.FeatureCount.Should().Be(0);
		result.Extent.Should().BeNull();
	}

	[Fact]
	public async Task GetStatsAsync_ShouldReturnNull_WhenCollectionDoesNotExist()
	{
		// Arrange
		var collectionId = "non-existent-id";

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync((Collection?)null);

		// Act
		var result = await _collectionsService.GetStatsAsync(collectionId);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task GetStatsAsync_ShouldReturnNull_WhenCollectionIdIsNullOrEmpty()
	{
		// Act
		var result = await _collectionsService.GetStatsAsync("");

		// Assert
		result.Should().BeNull();
		_mockCollectionRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
	}

	#endregion

	#region RefreshExtentAsync Tests

	[Fact]
	public async Task RefreshExtentAsync_ShouldUpdateExtent_WhenFeaturesExist()
	{
		// Arrange
		var collectionId = "test-id";
		var extent = (-10.0, -5.0, 10.0, 5.0);

		_mockFeatureRepository
			.Setup(x => x.GetExtentAsync(collectionId))
			.ReturnsAsync(extent);

		_mockCollectionRepository
			.Setup(x => x.UpdateExtentAsync(collectionId, It.IsAny<string>()))
			.ReturnsAsync(1);

		// Act
		var result = await _collectionsService.RefreshExtentAsync(collectionId);

		// Assert
		result.Should().Be(1);
		_mockFeatureRepository.Verify(x => x.GetExtentAsync(collectionId), Times.Once);
		_mockCollectionRepository.Verify(x => x.UpdateExtentAsync(
			collectionId,
			It.Is<string>(json => json.Contains("min_x") && json.Contains("max_y"))),
			Times.Once);
	}

	[Fact]
	public async Task RefreshExtentAsync_ShouldReturnZero_WhenNoFeaturesExist()
	{
		// Arrange
		var collectionId = "test-id";

		_mockFeatureRepository
			.Setup(x => x.GetExtentAsync(collectionId))
			.ReturnsAsync(((double minX, double minY, double maxX, double maxY)?)null);

		// Act
		var result = await _collectionsService.RefreshExtentAsync(collectionId);

		// Assert
		result.Should().Be(0);
		_mockCollectionRepository.Verify(x => x.UpdateExtentAsync(
			It.IsAny<string>(),
			It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task RefreshExtentAsync_ShouldReturnZero_WhenCollectionIdIsNullOrEmpty()
	{
		// Act
		var result = await _collectionsService.RefreshExtentAsync("");

		// Assert
		result.Should().Be(0);
		_mockFeatureRepository.Verify(x => x.GetExtentAsync(It.IsAny<string>()), Times.Never);
	}

	#endregion

	#region ValidateSchema Tests

	[Fact]
	public void ValidateSchema_ShouldReturnTrue_ForValidJsonSchema()
	{
		// Arrange
		var validSchemas = new[]
		{
			"{}",
			"{\"type\": \"object\"}",
			"{\"type\": \"object\", \"properties\": {\"name\": {\"type\": \"string\"}}}",
			"{\"properties\": {\"age\": {\"type\": \"number\"}, \"name\": {\"type\": \"string\"}}}"
		};

		// Act & Assert
		foreach (var schema in validSchemas)
		{
			var result = _collectionsService.ValidateSchema(schema);
			result.Should().BeTrue($"schema '{schema}' should be valid");
		}
	}

	[Fact]
	public void ValidateSchema_ShouldReturnTrue_ForEmptyOrNullSchema()
	{
		// Act & Assert
		_collectionsService.ValidateSchema(null!).Should().BeTrue();
		_collectionsService.ValidateSchema("").Should().BeTrue();
		_collectionsService.ValidateSchema("   ").Should().BeTrue();
	}

	[Fact]
	public void ValidateSchema_ShouldReturnFalse_ForInvalidJson()
	{
		// Arrange
		var invalidSchemas = new[]
		{
			"not json",
			"{invalid}",
			"[array, not, object]",
			"{\"key\": }",
			"\"just a string\""
		};

		// Act & Assert
		foreach (var schema in invalidSchemas)
		{
			var result = _collectionsService.ValidateSchema(schema);
			result.Should().BeFalse($"schema '{schema}' should be invalid");
		}
	}

	[Fact]
	public void ValidateSchema_ShouldReturnFalse_WhenPropertiesIsNotAnObject()
	{
		// Arrange
		var schema = "{\"properties\": \"not an object\"}";

		// Act
		var result = _collectionsService.ValidateSchema(schema);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateSchema_ShouldReturnFalse_WhenTypeIsNotAString()
	{
		// Arrange
		var schema = "{\"type\": 123}";

		// Act
		var result = _collectionsService.ValidateSchema(schema);

		// Assert
		result.Should().BeFalse();
	}

	#endregion

	#region ValidateSymbology Tests

	[Fact]
	public void ValidateSymbology_ShouldReturnTrue_ForValidSymbologyJson()
	{
		// Arrange
		var validSymbologies = new[]
		{
			"{}",
			"{\"color\": \"#FF0000\"}",
			"{\"icon\": \"marker\"}",
			"{\"style\": {\"strokeWidth\": 2}}",
			"{\"color\": {\"r\": 255, \"g\": 0, \"b\": 0}, \"icon\": \"pin\", \"style\": {\"opacity\": 0.8}}"
		};

		// Act & Assert
		foreach (var symbology in validSymbologies)
		{
			var result = _collectionsService.ValidateSymbology(symbology);
			result.Should().BeTrue($"symbology '{symbology}' should be valid");
		}
	}

	[Fact]
	public void ValidateSymbology_ShouldReturnTrue_ForEmptyOrNullSymbology()
	{
		// Act & Assert
		_collectionsService.ValidateSymbology(null!).Should().BeTrue();
		_collectionsService.ValidateSymbology("").Should().BeTrue();
		_collectionsService.ValidateSymbology("   ").Should().BeTrue();
	}

	[Fact]
	public void ValidateSymbology_ShouldReturnFalse_ForInvalidJson()
	{
		// Arrange
		var invalidSymbologies = new[]
		{
			"not json",
			"{invalid}",
			"[array]",
			"{\"key\": }",
			"\"just a string\""
		};

		// Act & Assert
		foreach (var symbology in invalidSymbologies)
		{
			var result = _collectionsService.ValidateSymbology(symbology);
			result.Should().BeFalse($"symbology '{symbology}' should be invalid");
		}
	}

	[Fact]
	public void ValidateSymbology_ShouldReturnFalse_WhenIconIsNotAString()
	{
		// Arrange
		var symbology = "{\"icon\": 123}";

		// Act
		var result = _collectionsService.ValidateSymbology(symbology);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateSymbology_ShouldReturnFalse_WhenStyleIsNotAnObject()
	{
		// Arrange
		var symbology = "{\"style\": \"not an object\"}";

		// Act
		var result = _collectionsService.ValidateSymbology(symbology);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateSymbology_ShouldReturnFalse_WhenColorIsInvalidType()
	{
		// Arrange
		var symbology = "{\"color\": 123}";

		// Act
		var result = _collectionsService.ValidateSymbology(symbology);

		// Assert
		result.Should().BeFalse();
	}

	#endregion
}
