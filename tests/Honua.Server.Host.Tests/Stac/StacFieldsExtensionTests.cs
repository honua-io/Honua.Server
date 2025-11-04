using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Honua.Server.Core.Stac;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

/// <summary>
/// Tests for the STAC API Fields Extension implementation.
/// </summary>
/// <remarks>
/// Tests field parsing, filtering logic, and compliance with the Fields Extension specification.
/// Reference: https://github.com/stac-api-extensions/fields
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Fast")]
public sealed class StacFieldsExtensionTests
{
    [Fact]
    public void ParseGetFields_WithIncludeOnly_ReturnsIncludeSet()
    {
        // Arrange
        var fieldsString = "id,geometry,properties.datetime";

        // Act
        var result = FieldsParser.ParseGetFields(fieldsString);

        // Assert
        Assert.NotNull(result.Include);
        Assert.Equal(3, result.Include.Count);
        Assert.Contains("id", result.Include);
        Assert.Contains("geometry", result.Include);
        Assert.Contains("properties.datetime", result.Include);
        Assert.Null(result.Exclude);
    }

    [Fact]
    public void ParseGetFields_WithExcludeOnly_ReturnsExcludeSet()
    {
        // Arrange
        var fieldsString = "-assets,-links,-properties.metadata";

        // Act
        var result = FieldsParser.ParseGetFields(fieldsString);

        // Assert
        Assert.NotNull(result.Exclude);
        Assert.Equal(3, result.Exclude.Count);
        Assert.Contains("assets", result.Exclude);
        Assert.Contains("links", result.Exclude);
        Assert.Contains("properties.metadata", result.Exclude);
        Assert.Null(result.Include);
    }

    [Fact]
    public void ParseGetFields_WithMixed_ReturnsBothSets()
    {
        // Arrange
        var fieldsString = "id,geometry,-assets.preview";

        // Act
        var result = FieldsParser.ParseGetFields(fieldsString);

        // Assert
        Assert.NotNull(result.Include);
        Assert.Equal(2, result.Include.Count);
        Assert.Contains("id", result.Include);
        Assert.Contains("geometry", result.Include);

        Assert.NotNull(result.Exclude);
        Assert.Single(result.Exclude);
        Assert.Contains("assets.preview", result.Exclude);
    }

    [Fact]
    public void ParseGetFields_WithWhitespace_HandlesCorrectly()
    {
        // Arrange
        var fieldsString = " id , geometry , properties.datetime ";

        // Act
        var result = FieldsParser.ParseGetFields(fieldsString);

        // Assert
        Assert.NotNull(result.Include);
        Assert.Equal(3, result.Include.Count);
    }

    [Fact]
    public void ParseGetFields_WithNullOrEmpty_ReturnsEmptySpec()
    {
        // Act
        var result1 = FieldsParser.ParseGetFields(null);
        var result2 = FieldsParser.ParseGetFields("");
        var result3 = FieldsParser.ParseGetFields("   ");

        // Assert
        Assert.True(result1.IsEmpty);
        Assert.True(result2.IsEmpty);
        Assert.True(result3.IsEmpty);
    }

    [Fact]
    public void ParsePostFields_WithIncludeOnly_ReturnsIncludeSet()
    {
        // Arrange
        var include = new List<string> { "id", "geometry", "properties.datetime" };

        // Act
        var result = FieldsParser.ParsePostFields(include, null);

        // Assert
        Assert.NotNull(result.Include);
        Assert.Equal(3, result.Include.Count);
        Assert.Null(result.Exclude);
    }

    [Fact]
    public void ParsePostFields_WithExcludeOnly_ReturnsExcludeSet()
    {
        // Arrange
        var exclude = new List<string> { "assets", "links" };

        // Act
        var result = FieldsParser.ParsePostFields(null, exclude);

        // Assert
        Assert.NotNull(result.Exclude);
        Assert.Equal(2, result.Exclude.Count);
        Assert.Null(result.Include);
    }

    [Fact]
    public void ApplyFieldsFilter_WithIncludeMode_IncludesOnlySpecifiedFields()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test-item",
            ["type"] = "Feature",
            ["geometry"] = new JsonObject { ["type"] = "Point" },
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject { ["thumbnail"] = new JsonObject() },
            ["links"] = new JsonArray()
        };

        var fields = new FieldsSpecification
        {
            Include = new HashSet<string> { "id", "type", "geometry" }
        };

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, fields);

        // Assert
        Assert.NotNull(result["id"]);
        Assert.NotNull(result["type"]);
        Assert.NotNull(result["geometry"]);
        Assert.Null(result["properties"]);
        Assert.Null(result["assets"]);
        Assert.Null(result["links"]);
    }

    [Fact]
    public void ApplyFieldsFilter_WithExcludeMode_ExcludesSpecifiedFields()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test-item",
            ["type"] = "Feature",
            ["geometry"] = new JsonObject { ["type"] = "Point" },
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject { ["thumbnail"] = new JsonObject() },
            ["links"] = new JsonArray()
        };

        var fields = new FieldsSpecification
        {
            Exclude = new HashSet<string> { "assets", "links" }
        };

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, fields);

        // Assert
        Assert.NotNull(result["id"]);
        Assert.NotNull(result["type"]);
        Assert.NotNull(result["geometry"]);
        Assert.NotNull(result["properties"]);
        Assert.Null(result["assets"]);
        Assert.Null(result["links"]);
    }

    [Fact]
    public void ApplyFieldsFilter_WithNestedInclude_IncludesNestedFieldOnly()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test-item",
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z",
                ["cloud_cover"] = 10,
                ["metadata"] = new JsonObject { ["sensor"] = "MSI" }
            }
        };

        var fields = new FieldsSpecification
        {
            Include = new HashSet<string> { "id", "properties.datetime", "properties.cloud_cover" }
        };

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, fields);

        // Assert
        Assert.NotNull(result["id"]);
        var properties = result["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.NotNull(properties["datetime"]);
        Assert.NotNull(properties["cloud_cover"]);
        Assert.Null(properties["metadata"]);
    }

    [Fact]
    public void ApplyFieldsFilter_WithNestedExclude_ExcludesNestedFieldOnly()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test-item",
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z",
                ["cloud_cover"] = 10,
                ["metadata"] = new JsonObject { ["sensor"] = "MSI" }
            }
        };

        var fields = new FieldsSpecification
        {
            Exclude = new HashSet<string> { "properties.metadata" }
        };

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, fields);

        // Assert
        Assert.NotNull(result["id"]);
        var properties = result["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.NotNull(properties["datetime"]);
        Assert.NotNull(properties["cloud_cover"]);
        Assert.Null(properties["metadata"]);
    }

    [Fact]
    public void ApplyFieldsFilter_WithEmptySpec_ReturnsUnmodified()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test-item",
            ["type"] = "Feature",
            ["geometry"] = new JsonObject()
        };

        var fields = new FieldsSpecification();

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, fields);

        // Assert
        Assert.Equal(item.Count, result.Count);
        Assert.NotNull(result["id"]);
        Assert.NotNull(result["type"]);
        Assert.NotNull(result["geometry"]);
    }

    [Fact]
    public void ApplyFieldsFilter_WithNullSpec_ReturnsUnmodified()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test-item",
            ["type"] = "Feature"
        };

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, null);

        // Assert
        Assert.Same(item, result);
    }

    [Fact]
    public void FieldsSpecification_DefaultSet_ContainsRequiredFields()
    {
        // Arrange & Act
        var defaultFields = FieldsSpecification.Default;

        // Assert
        Assert.NotNull(defaultFields.Include);
        Assert.Contains("type", defaultFields.Include);
        Assert.Contains("stac_version", defaultFields.Include);
        Assert.Contains("id", defaultFields.Include);
        Assert.Contains("geometry", defaultFields.Include);
        Assert.Contains("bbox", defaultFields.Include);
        Assert.Contains("links", defaultFields.Include);
        Assert.Contains("assets", defaultFields.Include);
        Assert.Contains("properties.datetime", defaultFields.Include);
    }

    [Fact]
    public void FieldsSpecification_IsIncludeMode_ReturnsTrueWhenIncludeSpecified()
    {
        // Arrange
        var spec = new FieldsSpecification
        {
            Include = new HashSet<string> { "id", "geometry" }
        };

        // Assert
        Assert.True(spec.IsIncludeMode);
        Assert.False(spec.IsExcludeMode);
    }

    [Fact]
    public void FieldsSpecification_IsExcludeMode_ReturnsTrueWhenOnlyExcludeSpecified()
    {
        // Arrange
        var spec = new FieldsSpecification
        {
            Exclude = new HashSet<string> { "assets" }
        };

        // Assert
        Assert.False(spec.IsIncludeMode);
        Assert.True(spec.IsExcludeMode);
    }

    [Fact]
    public void ApplyFieldsFilter_WithComplexNesting_HandlesCorrectly()
    {
        // Arrange
        var item = new JsonObject
        {
            ["id"] = "test",
            ["properties"] = new JsonObject
            {
                ["eo:cloud_cover"] = 15,
                ["sar:instrument_mode"] = "IW",
                ["metadata"] = new JsonObject
                {
                    ["processing"] = new JsonObject
                    {
                        ["level"] = "L1C",
                        ["version"] = "1.0"
                    }
                }
            }
        };

        var fields = new FieldsSpecification
        {
            Include = new HashSet<string> { "id", "properties.eo:cloud_cover", "properties.metadata.processing.level" }
        };

        // Act
        var result = FieldsFilter.ApplyFieldsFilter(item, fields);

        // Assert
        Assert.NotNull(result["id"]);
        var props = result["properties"]?.AsObject();
        Assert.NotNull(props);
        Assert.NotNull(props["eo:cloud_cover"]);
        Assert.Null(props["sar:instrument_mode"]);

        var metadata = props["metadata"]?.AsObject();
        Assert.NotNull(metadata);
        var processing = metadata["processing"]?.AsObject();
        Assert.NotNull(processing);
        Assert.NotNull(processing["level"]);
        Assert.Null(processing["version"]);
    }
}
