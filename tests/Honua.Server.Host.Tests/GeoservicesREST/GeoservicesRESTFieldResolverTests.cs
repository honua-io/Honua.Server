using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Unit tests for GeoservicesFieldResolver functionality.
/// Tests cover field resolution, outFields parsing, geometry field exclusion,
/// orderByFields parsing, and groupByFields resolution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Speed", "Fast")]
public sealed class GeoservicesRESTFieldResolverTests
{
    #region Test Infrastructure

    private static QueryCollection CreateQueryCollection(Dictionary<string, StringValues> values)
    {
        return new QueryCollection(values);
    }

    private static LayerDefinition CreateTestLayer()
    {
        var fields = GeoservicesTestFactory.DefaultFields()
            .Concat(new[]
            {
                new FieldDefinition { Name = "created_date", DataType = "date", Nullable = true }
            })
            .ToArray();

        return GeoservicesTestFactory.CreateLayerDefinition(fields: fields, geometryField: "shape", geometryType: "esriGeometryPolygon");
    }

    #endregion

    #region ResolveOutFields Tests

    [Fact]
    public void ResolveOutFields_Asterisk_ReturnsAllFieldsExcludingGeometry()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "*"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        outFields.Should().Be("*");
        propertyNames.Should().BeNull(); // null means "all fields"
        selectedFields.Should().NotBeEmpty();
        selectedFields.Should().ContainKey("objectid");
        selectedFields.Should().ContainKey("name");
        selectedFields.Should().ContainKey("population");
        selectedFields.Should().ContainKey("area");
        selectedFields.Should().ContainKey("created_date");
        selectedFields.Should().NotContainKey("shape"); // CRITICAL: Geometry field excluded
    }

    [Fact]
    public void ResolveOutFields_SpecificFields_ReturnsOnlyRequestedFields()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "name,population"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        outFields.Should().Be("name,population");
        propertyNames.Should().NotBeNull();
        propertyNames.Should().Contain("name");
        propertyNames.Should().Contain("population");
        propertyNames.Should().Contain("objectid"); // ID field always included
        selectedFields.Should().ContainKey("name");
        selectedFields.Should().ContainKey("population");
        selectedFields.Should().ContainKey("objectid");
        selectedFields.Should().NotContainKey("area");
    }

    [Fact]
    public void ResolveOutFields_EmptyString_ReturnsAllFields()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = ""
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        outFields.Should().BeEmpty();
        propertyNames.Should().BeNull();
        selectedFields.Should().ContainKey("objectid");
        selectedFields.Should().ContainKey("name");
        selectedFields.Should().ContainKey("population");
        selectedFields.Should().ContainKey("area");
    }

    [Fact]
    public void ResolveOutFields_ReturnIdsOnly_ReturnsOnlyIdField()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "name,population,area"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: true);

        // Assert
        outFields.Should().Be("objectid");
        propertyNames.Should().NotBeNull();
        propertyNames.Should().Contain("objectid");
        propertyNames.Should().HaveCount(1);
        selectedFields.Should().ContainKey("objectid");
        selectedFields.Should().HaveCount(1);
    }

    [Fact]
    public void ResolveOutFields_DuplicateFields_DeduplicatesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "name,name,population,name"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        outFields.Should().Be("name,name,population,name"); // Original preserved
        propertyNames.Should().NotBeNull();
        propertyNames.Should().Contain("name");
        propertyNames.Should().Contain("population");
        propertyNames.Should().Contain("objectid");
        selectedFields.Keys.Where(k => k.Equals("name", StringComparison.OrdinalIgnoreCase)).Should().HaveCount(1);
    }

    [Fact]
    public void ResolveOutFields_CaseInsensitive_NormalizesFieldNames()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "NAME,POPULATION"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        propertyNames.Should().NotBeNull();
        propertyNames!.Any(f => f.Equals("name", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        propertyNames.Any(f => f.Equals("population", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        selectedFields.Keys.Any(k => k.Equals("name", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        selectedFields.Keys.Any(k => k.Equals("population", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public void ResolveOutFields_GeometryFieldRequested_ExcludesGeometry()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "name,shape,population"
        });
        var layer = CreateTestLayer();

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false));
    }

    [Fact]
    public void ResolveOutFields_NoOutFieldsParameter_DefaultsToAsterisk()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        outFields.Should().Be("*");
        propertyNames.Should().BeNull(); // null means "all fields"
        selectedFields.Should().NotBeEmpty();
    }

    [Fact]
    public void ResolveOutFields_RequestedFields_MaintainsOrder()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "population,name,area"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        requestedFields.Should().NotBeNull();
        var orderedList = requestedFields!.ToList();
        orderedList[0].Should().Be("population");
        orderedList[1].Should().Be("name");
        orderedList[2].Should().Be("area");
    }

    #endregion

    #region ResolveOrderByFields Tests

    [Fact]
    public void ResolveOrderByFields_SingleFieldAsc_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "name ASC"
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().NotBeNull();
        sortOrders.Should().HaveCount(1);
        sortOrders![0].Field.ToLowerInvariant().Should().Be("name");
        sortOrders[0].Direction.Should().Be(FeatureSortDirection.Ascending);
    }

    [Fact]
    public void ResolveOrderByFields_SingleFieldDesc_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "population DESC"
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().NotBeNull();
        sortOrders.Should().HaveCount(1);
        sortOrders![0].Field.ToLowerInvariant().Should().Be("population");
        sortOrders[0].Direction.Should().Be(FeatureSortDirection.Descending);
    }

    [Fact]
    public void ResolveOrderByFields_MultipleFields_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "name ASC,population DESC,area ASC"
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().NotBeNull();
        sortOrders.Should().HaveCount(3);
        sortOrders![0].Field.ToLowerInvariant().Should().Be("name");
        sortOrders[0].Direction.Should().Be(FeatureSortDirection.Ascending);
        sortOrders[1].Field.ToLowerInvariant().Should().Be("population");
        sortOrders[1].Direction.Should().Be(FeatureSortDirection.Descending);
        sortOrders[2].Field.ToLowerInvariant().Should().Be("area");
        sortOrders[2].Direction.Should().Be(FeatureSortDirection.Ascending);
    }

    [Fact]
    public void ResolveOrderByFields_NoParameter_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().BeNull();
    }

    [Fact]
    public void ResolveOrderByFields_EmptyString_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = ""
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().BeNull();
    }

    [Fact]
    public void ResolveOrderByFields_NoDirectionSpecified_DefaultsToAsc()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "name"
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().NotBeNull();
        sortOrders.Should().HaveCount(1);
        sortOrders![0].Field.ToLowerInvariant().Should().Be("name");
        sortOrders[0].Direction.Should().Be(FeatureSortDirection.Ascending);
    }

    [Fact]
    public void ResolveOrderByFields_CaseInsensitive_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "NAME asc,POPULATION desc"
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().NotBeNull();
        sortOrders.Should().HaveCount(2);
        sortOrders![0].Field.ToLowerInvariant().Should().Be("name");
        sortOrders[1].Field.ToLowerInvariant().Should().Be("population");
    }

    #endregion

    #region ResolveGroupByFields Tests

    [Fact]
    public void ResolveGroupByFields_SingleField_ReturnsCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = "name"
        });
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().HaveCount(1);
        groupByFields.Should().Contain("name");
    }

    [Fact]
    public void ResolveGroupByFields_MultipleFields_ReturnsCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = "name,population"
        });
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().HaveCount(2);
        groupByFields.Should().Contain("name");
        groupByFields.Should().Contain("population");
    }

    [Fact]
    public void ResolveGroupByFields_NoParameter_ReturnsEmpty()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().BeEmpty();
    }

    [Fact]
    public void ResolveGroupByFields_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = ""
        });
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().BeEmpty();
    }

    [Fact]
    public void ResolveGroupByFields_DuplicateFields_Deduplicates()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = "name,population,name"
        });
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().HaveCount(2);
        groupByFields.Should().Contain("name");
        groupByFields.Should().Contain("population");
    }

    [Fact]
    public void ResolveGroupByFields_CaseInsensitive_NormalizesFieldNames()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = "NAME,POPULATION"
        });
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().HaveCount(2);
        // Field names should be normalized to match layer field definitions
    }

    [Fact]
    public void ResolveGroupByFields_WhitespaceFields_IgnoresEmpty()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = "name,  ,population,   "
        });
        var layer = CreateTestLayer();

        // Act
        var groupByFields = GeoservicesFieldResolver.ResolveGroupByFields(query, layer);

        // Assert
        groupByFields.Should().NotBeNull();
        groupByFields.Should().HaveCount(2);
        groupByFields.Should().Contain("name");
        groupByFields.Should().Contain("population");
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void ResolveOutFields_IdFieldAlwaysIncluded_EvenWhenNotRequested()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "name,population"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        propertyNames.Should().Contain("objectid"); // CRITICAL: ID field always included
        selectedFields.Should().ContainKey("objectid");
    }

    [Fact]
    public void ResolveOutFields_GeometryFieldInAsterisk_StillExcluded()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "*"
        });
        var layer = CreateTestLayer();

        // Act
        var (outFields, propertyNames, selectedFields, requestedFields) =
            GeoservicesFieldResolver.ResolveOutFields(query, layer, returnIdsOnly: false);

        // Assert
        selectedFields.Should().NotContainKey("shape"); // CRITICAL: Geometry excluded even with *
    }

    [Fact]
    public void ResolveOrderByFields_IdField_IsValid()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "objectid DESC"
        });
        var layer = CreateTestLayer();

        // Act
        var sortOrders = GeoservicesFieldResolver.ResolveOrderByFields(query, layer);

        // Assert
        sortOrders.Should().NotBeNull();
        sortOrders.Should().HaveCount(1);
        sortOrders![0].Field.ToLowerInvariant().Should().Be("objectid");
    }

    #endregion
}
