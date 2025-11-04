using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Query;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class QueryParameterHelperTests
{
    #region ParseLimit Tests

    [Theory]
    [InlineData(null, 100, null, 50, 50)]  // null raw -> use effective max (min of service/layer)
    [InlineData("", 100, null, 50, 100)]   // empty string -> use effective max (service)
    [InlineData("  ", null, 75, 50, 75)]   // whitespace -> use effective max (layer)
    [InlineData("25", 100, 50, 100, 25)]   // valid value below both maximums
    [InlineData("75", 100, 50, 100, 50)]   // valid value above layer max, clamped
    [InlineData("150", 100, 50, 100, 50)]  // valid value above both, clamped to min(service, layer)
    [InlineData("0", 100, 50, 100, 50)]    // zero -> use effective max (special case for GeoServices)
    public void ParseLimit_ValidInputs_ReturnsCorrectValue(
        string? raw,
        int? serviceMax,
        int? layerMax,
        int fallback,
        int expectedValue)
    {
        var (value, error) = QueryParameterHelper.ParseLimit(raw, serviceMax, layerMax, fallback);

        error.Should().BeNull();
        value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(null, null, null, 1000, 1000)]  // No maximums -> use fallback
    [InlineData("", null, null, 500, 500)]      // Empty string -> use fallback
    [InlineData("200", null, null, 1000, 200)]  // Valid value -> use value (no clamping without max)
    public void ParseLimit_NoMaximums_UsesFallback(string? raw, int? serviceMax, int? layerMax, int fallback, int expectedValue)
    {
        var (value, error) = QueryParameterHelper.ParseLimit(raw, serviceMax, layerMax, fallback);

        error.Should().BeNull();
        value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-100")]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("1e10")]
    [InlineData("999999999999999999999")]
    public void ParseLimit_InvalidInputs_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseLimit(raw, 100, 50, 1000);

        error.Should().NotBeNull();
        error.Should().Be("limit must be a positive integer.");
        value.Should().Be(0);
    }

    [Fact]
    public void ParseLimit_ServiceMaxLowerThanLayerMax_UsesServiceMax()
    {
        var (value, error) = QueryParameterHelper.ParseLimit("200", serviceMax: 50, layerMax: 100, fallback: 1000);

        error.Should().BeNull();
        value.Should().Be(50);  // Should be clamped to service max (50), not layer max (100)
    }

    [Fact]
    public void ParseLimit_LayerMaxLowerThanServiceMax_UsesLayerMax()
    {
        var (value, error) = QueryParameterHelper.ParseLimit("200", serviceMax: 100, layerMax: 50, fallback: 1000);

        error.Should().BeNull();
        value.Should().Be(50);  // Should be clamped to layer max (50), not service max (100)
    }

    #endregion

    #region ParseOffset Tests

    [Theory]
    [InlineData(null, null)]       // null -> returns null
    [InlineData("", null)]         // empty -> returns null
    [InlineData("  ", null)]       // whitespace -> returns null
    [InlineData("0", null)]        // zero -> returns null (normalized)
    [InlineData("1", 1)]           // valid offset
    [InlineData("100", 100)]       // valid offset
    [InlineData("999999", 999999)] // large valid offset
    public void ParseOffset_ValidInputs_ReturnsCorrectValue(string? raw, int? expectedValue)
    {
        var (value, error) = QueryParameterHelper.ParseOffset(raw);

        error.Should().BeNull();
        value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-100")]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("1e10")]
    [InlineData("999999999999999999999")]
    public void ParseOffset_InvalidInputs_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseOffset(raw);

        error.Should().NotBeNull();
        error.Should().Be("offset must be a non-negative integer.");
        value.Should().BeNull();
    }

    #endregion

    #region ParseBoundingBox Tests

    [Fact]
    public void ParseBoundingBox_Null_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox(null, null);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseBoundingBox_EmptyString_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("", null);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseBoundingBox_Valid2D_ReturnsCorrectBoundingBox()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("-122.5,47.5,-122.0,48.0", "EPSG:4326");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.MinX.Should().Be(-122.5);
        value.MinY.Should().Be(47.5);
        value.MaxX.Should().Be(-122.0);
        value.MaxY.Should().Be(48.0);
        value.MinZ.Should().BeNull();
        value.MaxZ.Should().BeNull();
        value.Crs.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ParseBoundingBox_Valid2DWithWhitespace_ReturnsCorrectBoundingBox()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox(" -122.5 , 47.5 , -122.0 , 48.0 ", null);

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.MinX.Should().Be(-122.5);
        value.MinY.Should().Be(47.5);
        value.MaxX.Should().Be(-122.0);
        value.MaxY.Should().Be(48.0);
    }

    [Fact]
    public void ParseBoundingBox_Valid3D_ReturnsCorrectBoundingBox()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("-122.5,47.5,100,-122.0,48.0,200", "EPSG:4326");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.MinX.Should().Be(-122.5);
        value.MinY.Should().Be(47.5);
        value.MinZ.Should().Be(100);
        value.MaxX.Should().Be(-122.0);
        value.MaxY.Should().Be(48.0);
        value.MaxZ.Should().Be(200);
        value.Crs.Should().Be("EPSG:4326");
    }

    [Theory]
    [InlineData("1,2,3")]         // Too few values
    [InlineData("1,2,3,4,5")]     // Invalid count (5)
    [InlineData("1,2,3,4,5,6,7")] // Too many values
    public void ParseBoundingBox_InvalidCount_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox(raw, null);

        error.Should().NotBeNull();
        error.Should().Be("bounding box must contain 4 values (2D) or 6 values (3D).");
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("abc,2,3,4")]
    [InlineData("1,xyz,3,4")]
    [InlineData("1,2,3,def")]
    [InlineData("1,2,NaN,4")]
    public void ParseBoundingBox_InvalidCoordinate_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox(raw, null);

        error.Should().NotBeNull();
        error.Should().Contain("is not a valid number");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseBoundingBox_MinXGreaterThanMaxX_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("-122.0,47.5,-122.5,48.0", null);

        error.Should().NotBeNull();
        error.Should().Be("bounding box minX must be less than maxX.");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseBoundingBox_MinYGreaterThanMaxY_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("-122.5,48.0,-122.0,47.5", null);

        error.Should().NotBeNull();
        error.Should().Be("bounding box minY must be less than maxY.");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseBoundingBox_MinXEqualToMaxX_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("-122.5,47.5,-122.5,48.0", null);

        error.Should().NotBeNull();
        error.Should().Be("bounding box minX must be less than maxX.");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseBoundingBox_MinYEqualToMaxY_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseBoundingBox("-122.5,47.5,-122.0,47.5", null);

        error.Should().NotBeNull();
        error.Should().Be("bounding box minY must be less than maxY.");
        value.Should().BeNull();
    }

    #endregion

    #region ParseCrs Tests

    [Fact]
    public void ParseCrs_Null_ReturnsDefaultCrs()
    {
        var supported = new[] { "EPSG:4326", "EPSG:3857" };
        var (value, error) = QueryParameterHelper.ParseCrs(null, supported, "EPSG:4326");

        error.Should().BeNull();
        value.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ParseCrs_EmptyString_ReturnsDefaultCrs()
    {
        var supported = new[] { "EPSG:4326", "EPSG:3857" };
        var (value, error) = QueryParameterHelper.ParseCrs("", supported, "EPSG:4326");

        error.Should().BeNull();
        value.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ParseCrs_ValidSupportedCrs_ReturnsNormalizedCrs()
    {
        var supported = new[] { "EPSG:4326", "EPSG:3857" };
        var (value, error) = QueryParameterHelper.ParseCrs("EPSG:4326", supported, null);

        error.Should().BeNull();
        value.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ParseCrs_CaseInsensitiveMatching_ReturnsNormalizedCrs()
    {
        var supported = new[] { "EPSG:4326", "EPSG:3857" };
        var (value, error) = QueryParameterHelper.ParseCrs("epsg:4326", supported, null);

        error.Should().BeNull();
        value.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ParseCrs_UnsupportedCrs_ReturnsError()
    {
        var supported = new[] { "EPSG:4326", "EPSG:3857" };
        var (value, error) = QueryParameterHelper.ParseCrs("EPSG:2154", supported, null);

        error.Should().NotBeNull();
        error.Should().Contain("CRS 'EPSG:2154' is not supported");
        error.Should().Contain("EPSG:4326, EPSG:3857");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseCrs_NullDefaultCrs_ReturnsNullWhenNoInput()
    {
        var supported = new[] { "EPSG:4326", "EPSG:3857" };
        var (value, error) = QueryParameterHelper.ParseCrs(null, supported, null);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    #endregion

    #region ParseCrsToSrid Tests

    [Fact]
    public void ParseCrsToSrid_Null_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseCrsToSrid(null);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseCrsToSrid_EmptyString_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseCrsToSrid("");

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("EPSG:4326", 4326)]
    [InlineData("EPSG:3857", 3857)]
    [InlineData("4326", 4326)]
    [InlineData("3857", 3857)]
    public void ParseCrsToSrid_ValidCrs_ReturnsSrid(string raw, int expectedSrid)
    {
        var (value, error) = QueryParameterHelper.ParseCrsToSrid(raw);

        error.Should().BeNull();
        value.Should().Be(expectedSrid);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("EPSG:")]
    [InlineData("EPSG:abc")]
    public void ParseCrsToSrid_InvalidCrs_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseCrsToSrid(raw);

        error.Should().NotBeNull();
        error.Should().Contain($"Invalid CRS '{raw}'");
        value.Should().BeNull();
    }

    #endregion

    #region ParseTemporalRange Tests

    [Fact]
    public void ParseTemporalRange_Null_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange(null);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_EmptyString_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("");

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_DoubleDot_ReturnsNull()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("..");

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_SingleInstant_ReturnsSameStartAndEnd()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("2023-01-15T10:30:00Z");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.Start.Should().Be(new DateTimeOffset(2023, 1, 15, 10, 30, 0, TimeSpan.Zero));
        value.End.Should().Be(new DateTimeOffset(2023, 1, 15, 10, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ParseTemporalRange_ValidRange_ReturnsCorrectInterval()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("2023-01-01T00:00:00Z/2023-12-31T23:59:59Z");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.Start.Should().Be(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        value.End.Should().Be(new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero));
    }

    [Fact]
    public void ParseTemporalRange_OpenEndedStart_ReturnsNullStart()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("../2023-12-31T23:59:59Z");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.Start.Should().BeNull();
        value.End.Should().Be(new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero));
    }

    [Fact]
    public void ParseTemporalRange_OpenEndedEnd_ReturnsNullEnd()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("2023-01-01T00:00:00Z/..");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.Start.Should().Be(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        value.End.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_EpochMilliseconds_ReturnsCorrectInterval()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("1609459200000");  // 2021-01-01 00:00:00 UTC

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.Start.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1609459200000));
        value.End.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1609459200000));
    }

    [Fact]
    public void ParseTemporalRange_EpochMillisecondsRange_ReturnsCorrectInterval()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("1609459200000/1640995199000");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value!.Start.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1609459200000));
        value.End.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1640995199000));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("2023-13-01")]  // Invalid month
    [InlineData("not-a-date")]
    public void ParseTemporalRange_InvalidInstant_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange(raw);

        error.Should().NotBeNull();
        error.Should().Contain("must be ISO 8601 format or epoch milliseconds");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_InvalidRangeStart_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("invalid/2023-12-31T23:59:59Z");

        error.Should().NotBeNull();
        error.Should().Contain("temporal range start");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_InvalidRangeEnd_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("2023-01-01T00:00:00Z/invalid");

        error.Should().NotBeNull();
        error.Should().Contain("temporal range end");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseTemporalRange_TooManySlashes_ReturnsError()
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange("2023-01-01/2023-06-01/2023-12-31");

        error.Should().NotBeNull();
        error.Should().Be("temporal parameter must be a single instant or a range (start/end).");
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("999999999999999999999")]  // Too large for epoch milliseconds
    [InlineData("-999999999999999999999")] // Too small for epoch milliseconds
    public void ParseTemporalRange_EpochOutOfRange_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseTemporalRange(raw);

        error.Should().NotBeNull();
        error.Should().Contain("outside the valid range");
        value.Should().BeNull();
    }

    #endregion

    #region ParsePropertyNames Tests

    [Fact]
    public void ParsePropertyNames_Null_ReturnsNull()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames(null, availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().BeNull();  // null means "all fields"
    }

    [Fact]
    public void ParsePropertyNames_EmptyString_ReturnsNull()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParsePropertyNames_Asterisk_ReturnsNull()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("*", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().BeNull();  // "*" means "all fields"
    }

    [Fact]
    public void ParsePropertyNames_ValidSingleField_ReturnsField()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(1);
        value.Should().Contain("field1");
    }

    [Fact]
    public void ParsePropertyNames_ValidMultipleFields_ReturnsAllFields()
    {
        var availableFields = new HashSet<string> { "field1", "field2", "field3" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,field2,field3", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(3);
        value.Should().Contain("field1");
        value.Should().Contain("field2");
        value.Should().Contain("field3");
    }

    [Fact]
    public void ParsePropertyNames_WithWhitespace_ReturnsFields()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames(" field1 , field2 ", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
        value.Should().Contain("field1");
        value.Should().Contain("field2");
    }

    [Fact]
    public void ParsePropertyNames_IncludesIdField_AcceptsIdField()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("id,field1", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().Contain("id");
        value.Should().Contain("field1");
    }

    [Fact]
    public void ParsePropertyNames_IncludesGeometryField_ExcludesGeometry()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,geom,field2", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
        value.Should().Contain("field1");
        value.Should().Contain("field2");
        value.Should().NotContain("geom");  // Geometry field should be excluded
    }

    [Fact]
    public void ParsePropertyNames_UnknownField_ReturnsError()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,unknown", availableFields, "id", "geom");

        error.Should().NotBeNull();
        error.Should().Contain("Unknown fields: unknown");
        value.Should().BeNull();
    }

    [Fact]
    public void ParsePropertyNames_MultipleUnknownFields_ReturnsError()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,unknown1,unknown2", availableFields, "id", "geom");

        error.Should().NotBeNull();
        error.Should().Contain("Unknown fields:");
        error.Should().Contain("unknown1");
        error.Should().Contain("unknown2");
        value.Should().BeNull();
    }

    [Fact]
    public void ParsePropertyNames_CaseInsensitive_ReturnsFields()
    {
        var availableFields = new HashSet<string> { "Field1", "Field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,FIELD2", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
    }

    [Fact]
    public void ParsePropertyNames_DuplicateFields_Deduplicates()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,field1,field2", availableFields, "id", "geom");

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
        value.Should().Contain("field1");
        value.Should().Contain("field2");
    }

    [Fact]
    public void ParsePropertyNames_OnlyCommas_ReturnsError()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames(",,,", availableFields, "id", "geom");

        error.Should().NotBeNull();
        error.Should().Be("property names must contain at least one field or '*'.");
        value.Should().BeNull();
    }

    [Fact]
    public void ParsePropertyNames_NullGeometryField_DoesNotExcludeAnything()
    {
        var availableFields = new HashSet<string> { "field1", "geom" };
        var (value, error) = QueryParameterHelper.ParsePropertyNames("field1,geom", availableFields, "id", null);

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
        value.Should().Contain("geom");  // Should not be excluded when geometryField is null
    }

    #endregion

    #region ParseSortOrders Tests

    [Fact]
    public void ParseSortOrders_Null_ReturnsNull()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders(null, availableFields);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseSortOrders_EmptyString_ReturnsNull()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("", availableFields);

        error.Should().BeNull();
        value.Should().BeNull();
    }

    [Fact]
    public void ParseSortOrders_SingleFieldDefaultDirection_ReturnsAscending()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("field1", availableFields);

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(1);
        value![0].Field.Should().Be("field1");
        value[0].Direction.Should().Be(FeatureSortDirection.Ascending);
    }

    [Theory]
    [InlineData("field1 ASC", FeatureSortDirection.Ascending)]
    [InlineData("field1 DESC", FeatureSortDirection.Descending)]
    [InlineData("field1 asc", FeatureSortDirection.Ascending)]
    [InlineData("field1 desc", FeatureSortDirection.Descending)]
    [InlineData("field1 A", FeatureSortDirection.Ascending)]
    [InlineData("field1 D", FeatureSortDirection.Descending)]
    [InlineData("field1 +", FeatureSortDirection.Ascending)]
    [InlineData("field1 -", FeatureSortDirection.Descending)]
    public void ParseSortOrders_WithDirection_ReturnsCorrectDirection(string raw, FeatureSortDirection expectedDirection)
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders(raw, availableFields);

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(1);
        value![0].Field.Should().Be("field1");
        value[0].Direction.Should().Be(expectedDirection);
    }

    [Fact]
    public void ParseSortOrders_MultipleFields_ReturnsAllOrders()
    {
        var availableFields = new HashSet<string> { "field1", "field2", "field3" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("field1 ASC,field2 DESC,field3", availableFields);

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(3);
        value![0].Field.Should().Be("field1");
        value[0].Direction.Should().Be(FeatureSortDirection.Ascending);
        value[1].Field.Should().Be("field2");
        value[1].Direction.Should().Be(FeatureSortDirection.Descending);
        value[2].Field.Should().Be("field3");
        value[2].Direction.Should().Be(FeatureSortDirection.Ascending);
    }

    [Fact]
    public void ParseSortOrders_ColonSeparator_ReturnsCorrectOrders()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("field1:asc,field2:desc", availableFields, separator: ':');

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
        value![0].Field.Should().Be("field1");
        value[0].Direction.Should().Be(FeatureSortDirection.Ascending);
        value[1].Field.Should().Be("field2");
        value[1].Direction.Should().Be(FeatureSortDirection.Descending);
    }

    [Fact]
    public void ParseSortOrders_SpaceSeparatorAcceptsColon_ReturnsCorrectOrders()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("field1:asc,field2 desc", availableFields, separator: ' ');

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
    }

    [Fact]
    public void ParseSortOrders_UnknownField_ReturnsError()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("unknown ASC", availableFields);

        error.Should().NotBeNull();
        error.Should().Be("sort field 'unknown' does not exist.");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseSortOrders_InvalidDirection_ReturnsError()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders("field1 INVALID", availableFields);

        error.Should().NotBeNull();
        error.Should().Contain("sort direction 'INVALID' is not supported");
        value.Should().BeNull();
    }

    [Fact]
    public void ParseSortOrders_WithWhitespace_ReturnsCorrectOrders()
    {
        var availableFields = new HashSet<string> { "field1", "field2" };
        var (value, error) = QueryParameterHelper.ParseSortOrders(" field1  ASC , field2  DESC ", availableFields);

        error.Should().BeNull();
        value.Should().NotBeNull();
        value.Should().HaveCount(2);
    }

    #endregion

    #region ParseResultType Tests

    [Fact]
    public void ParseResultType_Null_ReturnsDefault()
    {
        var (value, error) = QueryParameterHelper.ParseResultType(null, FeatureResultType.Results);

        error.Should().BeNull();
        value.Should().Be(FeatureResultType.Results);
    }

    [Fact]
    public void ParseResultType_EmptyString_ReturnsDefault()
    {
        var (value, error) = QueryParameterHelper.ParseResultType("", FeatureResultType.Hits);

        error.Should().BeNull();
        value.Should().Be(FeatureResultType.Hits);
    }

    [Theory]
    [InlineData("results", FeatureResultType.Results)]
    [InlineData("RESULTS", FeatureResultType.Results)]
    [InlineData("Results", FeatureResultType.Results)]
    [InlineData("hits", FeatureResultType.Hits)]
    [InlineData("HITS", FeatureResultType.Hits)]
    [InlineData("Hits", FeatureResultType.Hits)]
    [InlineData("count", FeatureResultType.Hits)]
    [InlineData("COUNT", FeatureResultType.Hits)]
    public void ParseResultType_ValidValues_ReturnsCorrectType(string raw, FeatureResultType expected)
    {
        var (value, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);

        error.Should().BeNull();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("true", FeatureResultType.Hits)]
    [InlineData("false", FeatureResultType.Results)]
    [InlineData("1", FeatureResultType.Hits)]
    [InlineData("0", FeatureResultType.Results)]
    public void ParseResultType_BooleanValues_ReturnsCorrectType(string raw, FeatureResultType expected)
    {
        var (value, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);

        error.Should().BeNull();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("maybe")]
    [InlineData("2")]
    [InlineData("yes")]
    public void ParseResultType_InvalidValue_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);

        error.Should().NotBeNull();
        error.Should().Contain($"result type '{raw}' is not supported");
        value.Should().Be(FeatureResultType.Results);  // Returns default on error
    }

    #endregion

    #region ParseBoolean Tests

    [Fact]
    public void ParseBoolean_Null_ReturnsDefault()
    {
        var (value, error) = QueryParameterHelper.ParseBoolean(null, true);

        error.Should().BeNull();
        value.Should().BeTrue();
    }

    [Fact]
    public void ParseBoolean_EmptyString_ReturnsDefault()
    {
        var (value, error) = QueryParameterHelper.ParseBoolean("", false);

        error.Should().BeNull();
        value.Should().BeFalse();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("t", true)]
    [InlineData("T", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("1", true)]
    public void ParseBoolean_TrueValues_ReturnsTrue(string raw, bool expected)
    {
        var (value, error) = QueryParameterHelper.ParseBoolean(raw, false);

        error.Should().BeNull();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("f", false)]
    [InlineData("F", false)]
    [InlineData("no", false)]
    [InlineData("NO", false)]
    [InlineData("n", false)]
    [InlineData("N", false)]
    [InlineData("0", false)]
    public void ParseBoolean_FalseValues_ReturnsFalse(string raw, bool expected)
    {
        var (value, error) = QueryParameterHelper.ParseBoolean(raw, true);

        error.Should().BeNull();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("maybe")]
    [InlineData("2")]
    [InlineData("-1")]
    public void ParseBoolean_InvalidValue_ReturnsError(string raw)
    {
        var (value, error) = QueryParameterHelper.ParseBoolean(raw, false);

        error.Should().NotBeNull();
        error.Should().Contain($"boolean value '{raw}' is not supported");
        value.Should().BeFalse();  // Returns default on error
    }

    [Fact]
    public void ParseBoolean_Whitespace_ReturnsDefault()
    {
        var (value, error) = QueryParameterHelper.ParseBoolean("   ", true);

        error.Should().BeNull();
        value.Should().BeTrue();
    }

    #endregion
}
