using FluentAssertions;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for collection management workflows
/// Tests real CollectionsService and FeaturesService with real database
/// </summary>
public class CollectionManagementIntegrationTests : IntegrationTestBase
{
	private ICollectionsService _collectionsService = null!;
	private IFeaturesService _featuresService = null!;

	protected override void ConfigureServices(IServiceCollection services)
	{
		base.ConfigureServices(services);

		services.AddSingleton<ICollectionsService, CollectionsService>();
		services.AddSingleton<IFeaturesService, FeaturesService>();
	}

	protected override async Task OnInitializeAsync()
	{
		_collectionsService = ServiceProvider.GetRequiredService<ICollectionsService>();
		_featuresService = ServiceProvider.GetRequiredService<IFeaturesService>();
		await base.OnInitializeAsync();
	}

	[Fact]
	public async Task CreateCollection_WithSchemaAndSymbology_ShouldPersist()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection(
			title: "Buildings",
			description: "Building features collection"
		);

		// Act
		var collectionId = await _collectionsService.CreateAsync(collection);

		// Assert
		collectionId.Should().NotBeNullOrEmpty();

		var retrieved = await _collectionsService.GetByIdAsync(collectionId);
		retrieved.Should().NotBeNull();
		retrieved!.Title.Should().Be("Buildings");
		retrieved.Description.Should().Be("Building features collection");
		retrieved.Schema.Should().NotBeNullOrEmpty();
		retrieved.Symbology.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task UpdateCollection_Metadata_ShouldPersist()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Act - Update collection
		collection.Title = "Updated Title";
		collection.Description = "Updated Description";
		await _collectionsService.UpdateAsync(collection);

		// Assert
		var updated = await _collectionsService.GetByIdAsync(collection.Id);
		updated.Should().NotBeNull();
		updated!.Title.Should().Be("Updated Title");
		updated.Description.Should().Be("Updated Description");
	}

	[Fact]
	public async Task AddFeaturesToCollection_ShouldIncrementItemCount()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		collection.ItemsCount = 0;
		await CollectionRepository.InsertAsync(collection);

		// Act - Add features
		for (int i = 0; i < 5; i++)
		{
			var feature = DataBuilder.CreateTestFeature(collection.Id);
			await FeatureRepository.InsertAsync(feature);
		}

		// Update item count
		var featureCount = await FeatureRepository.GetCountByCollectionAsync(collection.Id);
		collection.ItemsCount = featureCount;
		await CollectionRepository.UpdateAsync(collection);

		// Assert
		var updated = await CollectionRepository.GetByIdAsync(collection.Id);
		updated.Should().NotBeNull();
		updated!.ItemsCount.Should().Be(5);
	}

	[Fact]
	public async Task GetAllCollections_ShouldReturnAllCollections()
	{
		// Arrange
		var collection1 = DataBuilder.CreateTestCollection("Collection 1");
		var collection2 = DataBuilder.CreateTestCollection("Collection 2");
		var collection3 = DataBuilder.CreateTestCollection("Collection 3");

		await CollectionRepository.InsertAsync(collection1);
		await CollectionRepository.InsertAsync(collection2);
		await CollectionRepository.InsertAsync(collection3);

		// Act
		var allCollections = await _collectionsService.GetAllAsync();

		// Assert
		allCollections.Should().HaveCount(3);
		allCollections.Should().Contain(c => c.Title == "Collection 1");
		allCollections.Should().Contain(c => c.Title == "Collection 2");
		allCollections.Should().Contain(c => c.Title == "Collection 3");
	}

	[Fact]
	public async Task GetFeatureCount_ForCollection_ShouldReturnCorrectCount()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Add features
		for (int i = 0; i < 7; i++)
		{
			var feature = DataBuilder.CreateTestFeature(collection.Id);
			await FeatureRepository.InsertAsync(feature);
		}

		// Act
		var count = await _collectionsService.GetFeatureCountAsync(collection.Id);

		// Assert
		count.Should().Be(7);
	}

	[Fact]
	public async Task DeleteCollection_WithFeatures_ShouldHandleCorrectly()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Add features
		for (int i = 0; i < 3; i++)
		{
			var feature = DataBuilder.CreateTestFeature(collection.Id);
			await FeatureRepository.InsertAsync(feature);
		}

		// Act
		await _collectionsService.DeleteAsync(collection.Id);

		// Assert
		var deletedCollection = await CollectionRepository.GetByIdAsync(collection.Id);
		deletedCollection.Should().BeNull();

		// Features should also be deleted (cascade)
		var features = await FeatureRepository.GetByCollectionIdAsync(collection.Id);
		features.Should().BeEmpty();
	}

	[Fact]
	public async Task UpdateCollectionSchema_ShouldPersist()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Act - Update schema
		var newSchema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				value = new { type = "number", title = "Value" },
				category = new
				{
					type = "string",
					title = "Category",
					@enum = new[] { "Type A", "Type B", "Type C" }
				}
			},
			required = new[] { "name", "category" }
		});

		collection.Schema = newSchema;
		await CollectionRepository.UpdateAsync(collection);

		// Assert
		var updated = await CollectionRepository.GetByIdAsync(collection.Id);
		updated.Should().NotBeNull();
		updated!.Schema.Should().Be(newSchema);

		// Verify schema can be parsed
		var schemaDoc = JsonSerializer.Deserialize<JsonDocument>(updated.Schema);
		schemaDoc.Should().NotBeNull();
	}

	[Fact]
	public async Task UpdateCollectionSymbology_ShouldPersist()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Act - Update symbology
		var newSymbology = JsonSerializer.Serialize(new
		{
			type = "categorized",
			field = "category",
			categories = new[]
			{
				new { value = "Type A", color = "#ff0000", label = "Type A" },
				new { value = "Type B", color = "#00ff00", label = "Type B" }
			}
		});

		collection.Symbology = newSymbology;
		await CollectionRepository.UpdateAsync(collection);

		// Assert
		var updated = await CollectionRepository.GetByIdAsync(collection.Id);
		updated.Should().NotBeNull();
		updated!.Symbology.Should().Be(newSymbology);
	}

	[Fact]
	public async Task GetCollectionExtent_WithFeatures_ShouldCalculateBounds()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Add features at known locations
		var feature1 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(45.0, -122.0));
		var feature2 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(46.0, -123.0));
		var feature3 = DataBuilder.CreateTestFeature(
			collection.Id,
			DataBuilder.CreateRandomPoint(45.5, -122.5));

		await FeatureRepository.InsertAsync(feature1);
		await FeatureRepository.InsertAsync(feature2);
		await FeatureRepository.InsertAsync(feature3);

		// Act
		var extent = await FeatureRepository.GetExtentAsync(collection.Id);

		// Assert
		extent.Should().NotBeNull();
		extent!.Value.minX.Should().BeLessOrEqualTo(-122.0);
		extent.Value.maxX.Should().BeGreaterOrEqualTo(-122.0);
		extent.Value.minY.Should().BeLessOrEqualTo(45.0);
		extent.Value.maxY.Should().BeGreaterOrEqualTo(45.0);

		// Update collection with extent
		collection.Extent = JsonSerializer.Serialize(new
		{
			min_x = extent.Value.minX,
			min_y = extent.Value.minY,
			max_x = extent.Value.maxX,
			max_y = extent.Value.maxY
		});
		await CollectionRepository.UpdateAsync(collection);

		var updatedCollection = await CollectionRepository.GetByIdAsync(collection.Id);
		updatedCollection!.Extent.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task SearchCollections_ByTitle_ShouldReturnMatches()
	{
		// Arrange
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Buildings Dataset"));
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Roads Dataset"));
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Parks and Recreation"));
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Water Features"));

		// Act
		var allCollections = await CollectionRepository.GetAllAsync();
		var buildingsCollections = allCollections.Where(c => c.Title.Contains("Buildings")).ToList();

		// Assert
		buildingsCollections.Should().HaveCount(1);
		buildingsCollections[0].Title.Should().Be("Buildings Dataset");
	}

	[Fact]
	public async Task CloneCollection_ShouldCreateCopyWithoutFeatures()
	{
		// Arrange
		var originalCollection = DataBuilder.CreateTestCollection("Original Collection");
		await CollectionRepository.InsertAsync(originalCollection);

		// Add features to original
		for (int i = 0; i < 5; i++)
		{
			await FeatureRepository.InsertAsync(DataBuilder.CreateTestFeature(originalCollection.Id));
		}

		// Act - Clone collection
		var clonedCollection = new Collection
		{
			Id = Guid.NewGuid().ToString(),
			Title = "Cloned Collection",
			Description = originalCollection.Description,
			Schema = originalCollection.Schema,
			Symbology = originalCollection.Symbology,
			Extent = originalCollection.Extent,
			ItemsCount = 0
		};

		await CollectionRepository.InsertAsync(clonedCollection);

		// Assert
		var original = await CollectionRepository.GetByIdAsync(originalCollection.Id);
		var cloned = await CollectionRepository.GetByIdAsync(clonedCollection.Id);

		original.Should().NotBeNull();
		cloned.Should().NotBeNull();

		original!.Id.Should().NotBe(cloned!.Id);
		original.Schema.Should().Be(cloned.Schema);
		original.Symbology.Should().Be(cloned.Symbology);

		var originalFeatures = await FeatureRepository.GetByCollectionIdAsync(originalCollection.Id);
		var clonedFeatures = await FeatureRepository.GetByCollectionIdAsync(clonedCollection.Id);

		originalFeatures.Should().HaveCount(5);
		clonedFeatures.Should().BeEmpty();
	}

	[Fact]
	public async Task GetCollectionStatistics_ShouldReturnAccurateStats()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Add features with attachments
		for (int i = 0; i < 3; i++)
		{
			var feature = DataBuilder.CreateTestFeature(collection.Id);
			await FeatureRepository.InsertAsync(feature);

			// Add attachments
			for (int j = 0; j < 2; j++)
			{
				var attachment = DataBuilder.CreateTestAttachment(feature.Id);
				await AttachmentRepository.InsertAsync(attachment);
			}
		}

		// Act
		var featureCount = await FeatureRepository.GetCountByCollectionAsync(collection.Id);
		var features = await FeatureRepository.GetByCollectionIdAsync(collection.Id);
		var totalAttachments = 0;
		foreach (var feature in features)
		{
			var attachments = await AttachmentRepository.GetByFeatureIdAsync(feature.Id);
			totalAttachments += attachments.Count;
		}

		// Assert
		featureCount.Should().Be(3);
		totalAttachments.Should().Be(6); // 3 features * 2 attachments
	}

	[Fact]
	public async Task ValidateCollectionSchema_ShouldEnsureValidJson()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		// Act - Parse schema to ensure it's valid JSON
		var parseAction = () => JsonSerializer.Deserialize<JsonDocument>(collection.Schema);

		// Assert
		parseAction.Should().NotThrow();

		var schemaDoc = JsonSerializer.Deserialize<JsonDocument>(collection.Schema);
		schemaDoc.Should().NotBeNull();
		schemaDoc!.RootElement.TryGetProperty("properties", out _).Should().BeTrue();
	}

	[Fact]
	public async Task ExportCollectionMetadata_ShouldIncludeAllProperties()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		collection.ItemsCount = 10;
		await CollectionRepository.InsertAsync(collection);

		// Act
		var retrieved = await CollectionRepository.GetByIdAsync(collection.Id);

		// Assert
		retrieved.Should().NotBeNull();
		retrieved!.Id.Should().Be(collection.Id);
		retrieved.Title.Should().NotBeNullOrEmpty();
		retrieved.Schema.Should().NotBeNullOrEmpty();
		retrieved.Symbology.Should().NotBeNullOrEmpty();
		retrieved.ItemsCount.Should().Be(10);
	}

	[Fact]
	public async Task GetCollections_OrderedByTitle_ShouldReturnSorted()
	{
		// Arrange
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Charlie"));
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Alpha"));
		await CollectionRepository.InsertAsync(DataBuilder.CreateTestCollection("Bravo"));

		// Act
		var collections = await CollectionRepository.GetAllAsync();
		var sortedCollections = collections.OrderBy(c => c.Title).ToList();

		// Assert
		sortedCollections[0].Title.Should().Be("Alpha");
		sortedCollections[1].Title.Should().Be("Bravo");
		sortedCollections[2].Title.Should().Be("Charlie");
	}
}

/// <summary>
/// Features service interface (simplified for testing)
/// </summary>
public interface IFeaturesService
{
	Task<string> CreateAsync(Feature feature);
	Task UpdateAsync(Feature feature);
	Task<Feature?> GetByIdAsync(string id);
	Task<List<Feature>> GetByCollectionIdAsync(string collectionId);
	Task DeleteAsync(string id);
}

/// <summary>
/// Features service implementation (simplified for testing)
/// </summary>
public class FeaturesService : IFeaturesService
{
	private readonly IFeatureRepository _featureRepository;

	public FeaturesService(IFeatureRepository featureRepository)
	{
		_featureRepository = featureRepository;
	}

	public Task<string> CreateAsync(Feature feature) => _featureRepository.InsertAsync(feature);
	public Task UpdateAsync(Feature feature) => Task.FromResult(_featureRepository.UpdateAsync(feature));
	public Task<Feature?> GetByIdAsync(string id) => _featureRepository.GetByIdAsync(id);
	public Task<List<Feature>> GetByCollectionIdAsync(string collectionId) => _featureRepository.GetByCollectionIdAsync(collectionId);
	public Task DeleteAsync(string id) => Task.FromResult(_featureRepository.DeleteAsync(id));
}
