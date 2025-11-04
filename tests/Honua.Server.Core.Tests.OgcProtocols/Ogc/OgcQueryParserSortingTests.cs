using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Tests for OGC API Features multi-property sorting functionality.
/// Tests the ParseItemsQuery method in OgcSharedHandlers which handles sortby parameter parsing.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public sealed class OgcQueryParserSortingTests
{
    private static LayerDefinition CreateTestLayer()
    {
        return new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            IdField = "id",
            GeometryField = "geom",
            GeometryType = "Point",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int" },
                new() { Name = "name", DataType = "string" },
                new() { Name = "state", DataType = "string" },
                new() { Name = "city", DataType = "string" },
                new() { Name = "population", DataType = "int" },
                new() { Name = "elevation", DataType = "double" },
                new() { Name = "geom", DataType = "geometry" }
            }
        };
    }

    private static ServiceDefinition CreateTestService()
    {
        return new ServiceDefinition
        {
            Id = "test_service",
            Title = "Test Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "stub",
            Enabled = true,
            Ogc = new OgcServiceDefinition
            {
                CollectionsEnabled = true,
                ItemLimit = 1000,
                DefaultCrs = "EPSG:4326"
            }
        };
    }

    private static (FeatureQuery? Query, string? ContentCrs, bool IncludeCount, IResult? Error) ParseWithSortBy(string sortByValue)
    {
        var service = CreateTestService();
        var layer = CreateTestLayer();

        var queryCollection = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["sortby"] = new StringValues(sortByValue)
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Query = queryCollection;

        return OgcSharedHandlers.ParseItemsQuery(httpContext.Request, service, layer, queryCollection);
    }

    [Fact]
    public void ParseSortBy_SingleProperty_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("name");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Single(query.SortOrders);
        Assert.Equal("name", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Ascending, query.SortOrders[0].Direction);
    }

    [Fact]
    public void ParseSortBy_SinglePropertyDescending_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("-population");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Single(query.SortOrders);
        Assert.Equal("population", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[0].Direction);
    }

    [Fact]
    public void ParseSortBy_SinglePropertyExplicitAscending_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("+city");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Single(query.SortOrders);
        Assert.Equal("city", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Ascending, query.SortOrders[0].Direction);
    }

    [Fact]
    public void ParseSortBy_MultipleProperties_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("state,+city,-population");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(3, query.SortOrders.Count);

        Assert.Equal("state", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Ascending, query.SortOrders[0].Direction);

        Assert.Equal("city", query.SortOrders[1].Field);
        Assert.Equal(FeatureSortDirection.Ascending, query.SortOrders[1].Direction);

        Assert.Equal("population", query.SortOrders[2].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[2].Direction);
    }

    [Fact]
    public void ParseSortBy_TwoPropertiesMixedDirections_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("state,-population");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(2, query.SortOrders.Count);

        Assert.Equal("state", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Ascending, query.SortOrders[0].Direction);

        Assert.Equal("population", query.SortOrders[1].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[1].Direction);
    }

    [Fact]
    public void ParseSortBy_ColonSyntax_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("name:asc,population:desc");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(2, query.SortOrders.Count);

        Assert.Equal("name", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Ascending, query.SortOrders[0].Direction);

        Assert.Equal("population", query.SortOrders[1].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[1].Direction);
    }

    [Fact]
    public void ParseSortBy_WhitespaceHandling_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy(" state , +city , -population ");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(3, query.SortOrders.Count);

        Assert.Equal("state", query.SortOrders[0].Field);
        Assert.Equal("city", query.SortOrders[1].Field);
        Assert.Equal("population", query.SortOrders[2].Field);
    }

    [Fact]
    public void ParseSortBy_EmptyString_ReturnsNull()
    {
        var (query, _, _, error) = ParseWithSortBy("");

        Assert.Null(error);
        Assert.NotNull(query);
        // When sortby is empty, it should use default sorting (by ID field)
        Assert.NotNull(query.SortOrders);
        Assert.Single(query.SortOrders);
        Assert.Equal("id", query.SortOrders[0].Field); // Default to ID field
    }

    [Fact]
    public void ParseSortBy_OnlyCommas_ReturnsError()
    {
        var (query, _, _, error) = ParseWithSortBy(",,,");

        Assert.NotNull(error);
        // Expect validation problem for empty fields
    }

    [Fact]
    public void ParseSortBy_InvalidPropertyName_ReturnsError()
    {
        var (query, _, _, error) = ParseWithSortBy("nonexistent_field");

        Assert.NotNull(error);
        // The ParseItemsQuery should return an error for unknown fields
    }

    [Fact]
    public void ParseSortBy_GeometryField_ReturnsError()
    {
        var (query, _, _, error) = ParseWithSortBy("geom");

        Assert.NotNull(error);
        // Geometry fields cannot be sorted
    }

    [Fact]
    public void ParseSortBy_MixedPrefixAndColonSyntax_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("-state,city:desc");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(2, query.SortOrders.Count);

        Assert.Equal("state", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[0].Direction);

        Assert.Equal("city", query.SortOrders[1].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[1].Direction);
    }

    [Fact]
    public void ParseSortBy_AllDescending_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("-state,-city,-population");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(3, query.SortOrders.Count);

        Assert.All(query.SortOrders, order =>
            Assert.Equal(FeatureSortDirection.Descending, order.Direction));
    }

    [Fact]
    public void ParseSortBy_AllAscending_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("+state,+city,+population");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(3, query.SortOrders.Count);

        Assert.All(query.SortOrders, order =>
            Assert.Equal(FeatureSortDirection.Ascending, order.Direction));
    }

    [Fact]
    public void ParseSortBy_InvalidColonSyntax_ReturnsError()
    {
        var (query, _, _, error) = ParseWithSortBy("name:invalid");

        Assert.NotNull(error);
        // Should return error for invalid direction
    }

    [Fact]
    public void ParseSortBy_EmptyFieldWithColon_ReturnsError()
    {
        var (query, _, _, error) = ParseWithSortBy(":asc");

        Assert.NotNull(error);
        // Should return error for empty field name
    }

    [Fact]
    public void ParseSortBy_ColonWithoutDirection_ReturnsError()
    {
        var (query, _, _, error) = ParseWithSortBy("name:");

        Assert.NotNull(error);
        // Should return error for empty direction
    }

    [Fact]
    public void ParseSortBy_FiveProperties_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("state,city,name,population,elevation");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(5, query.SortOrders.Count);
    }

    [Fact]
    public void ParseSortBy_CaseSensitiveFieldNames_ParsesCorrectly()
    {
        // Field names should be case-insensitive
        var (query, _, _, error) = ParseWithSortBy("NAME,POPULATION");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(2, query.SortOrders.Count);
    }

    [Fact]
    public void ParseSortBy_DuplicateFields_ParsesAllOccurrences()
    {
        // While unusual, duplicate fields should be parsed (DB will handle)
        var (query, _, _, error) = ParseWithSortBy("state,state");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(2, query.SortOrders.Count);
        Assert.Equal("state", query.SortOrders[0].Field);
        Assert.Equal("state", query.SortOrders[1].Field);
    }

    [Fact]
    public void ParseSortBy_NumericFieldDescending_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("-elevation");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Single(query.SortOrders);
        Assert.Equal("elevation", query.SortOrders[0].Field);
        Assert.Equal(FeatureSortDirection.Descending, query.SortOrders[0].Direction);
    }

    [Fact]
    public void ParseSortBy_MixedDataTypes_ParsesCorrectly()
    {
        var (query, _, _, error) = ParseWithSortBy("name,population,elevation");

        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.SortOrders);
        Assert.Equal(3, query.SortOrders.Count);
        // String, int, double - all should parse correctly
    }
}
