using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Utilities;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class QueryParsingHelpersTests
{
    [Fact]
    public void ParseBoundingBox_ShouldReturnNumbers()
    {
        var (bbox, error) = QueryParsingHelpers.ParseBoundingBox("-10,20,30,40");

        error.Should().BeNull();
        bbox.Should().NotBeNull();
        bbox.Should().BeEquivalentTo(new[] { -10d, 20d, 30d, 40d });
    }

    [Fact]
    public void ParseBoundingBox_ShouldReturnErrorForInvalidValues()
    {
        var (_, error) = QueryParsingHelpers.ParseBoundingBox("-10,not-a-number,30,40");

        error.Should().NotBeNull();
    }

    [Fact]
    public void ParseTemporalRange_ShouldHandleOpenEndedIntervals()
    {
        var (range, error) = QueryParsingHelpers.ParseTemporalRange("2020-01-01T00:00:00Z/..");

        error.Should().BeNull();
        range.Should().NotBeNull();
        range!.Value.Start.Should().Be(DateTimeOffset.Parse("2020-01-01T00:00:00Z"));
        range.Value.End.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_ShouldReturnErrorForInvalidFormat()
    {
        var (_, error) = QueryParsingHelpers.ParseTemporalRange("2020-01-01/2020-02-01/extra");

        error.Should().NotBeNull();
    }

    [Fact]
    public void ParseCsv_ShouldTrimTokens()
    {
        var tokens = QueryParsingHelpers.ParseCsv(" one ,two ,, three ");

        tokens.Should().Equal("one", "two", "three");
    }

    [Fact]
    public void ParsePagination_ShouldNormalizeValues()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["limit"] = new StringValues("75"),
            ["offset"] = new StringValues("15")
        });

        var pagination = QueryParsingHelpers.ParsePagination(query, defaultLimit: 50, defaultOffset: 0, minLimit: 1, maxLimit: 60);

        pagination.Limit.Should().Be(60); // clamped to max
        pagination.Offset.Should().Be(15);
    }

    [Fact]
    public void ParseBoundingBoxWithCrs_ShouldExtractAltitudeAndCrs()
    {
        var (result, error) = QueryParsingHelpers.ParseBoundingBoxWithCrs("1,2,3,4,5,6,EPSG:3857", allowAltitude: true);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.Value.Coordinates.Should().Equal(new[] { 1d, 2d, 3d, 4d, 5d, 6d });
        result.Value.Crs.Should().Be("EPSG:3857");
    }

    [Fact]
    public void ParsePositiveInt_String_ShouldReturnDefaultWhenMissing()
    {
        var (value, error) = QueryParsingHelpers.ParsePositiveInt((string?)null, "limit", defaultValue: 10);

        error.Should().BeNull();
        value.Should().Be(10);
    }

    [Fact]
    public void ParsePositiveInt_String_ShouldReturnErrorWhenInvalid()
    {
        var (value, error) = QueryParsingHelpers.ParsePositiveInt(raw: "-1", "limit");

        value.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void ResolveCrs_ShouldNormalizeAndValidate()
    {
        var supported = new[]
        {
            CrsHelper.NormalizeIdentifier("EPSG:4326"),
            CrsHelper.NormalizeIdentifier("EPSG:3857")
        };

        var (value, error) = QueryParsingHelpers.ResolveCrs("epsg:3857", supported, "crs");

        error.Should().BeNull();
        value.Should().Be(CrsHelper.NormalizeIdentifier("EPSG:3857"));

        var (_, errorUnsupported) = QueryParsingHelpers.ResolveCrs("EPSG:9999", supported, "crs");

        errorUnsupported.Should().NotBeNull();
    }

    [Fact]
    public void ParseBoolean_ShouldHandleNumericTokens()
    {
        QueryParsingHelpers.ParseBoolean("1", defaultValue: false).Should().BeTrue();
        QueryParsingHelpers.ParseBoolean("off", defaultValue: true).Should().BeFalse();
        QueryParsingHelpers.ParseBoolean("maybe", defaultValue: true).Should().BeTrue();
    }
}
