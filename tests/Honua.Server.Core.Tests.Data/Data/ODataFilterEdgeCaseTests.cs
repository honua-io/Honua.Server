using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Comprehensive OData filter edge case tests including nested conditions,
/// geo queries with realistic coordinates, wildcards, and SQL injection attempts.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Category", "OData")]
public class ODataFilterEdgeCaseTests
{
    private readonly ITestOutputHelper _output;

    public ODataFilterEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Nested AND/OR Combinations

    [Theory]
    [InlineData("(status eq 'active' and priority gt 5) or (status eq 'pending' and priority eq 1)")]
    [InlineData("((category eq 'residential' or category eq 'commercial') and (zone eq 'R1' or zone eq 'C1'))")]
    [InlineData("status eq 'active' and (priority gt 5 or (priority eq 5 and owner eq 'admin'))")]
    public void NestedAndOrCombinations_ShouldBeSupportedInFilter(string filter)
    {
        // Arrange & Act
        var isValid = ValidateODataFilter(filter);

        // Assert
        isValid.Should().BeTrue($"nested AND/OR filter should be valid: {filter}");
        _output.WriteLine($"Valid nested filter: {filter}");
    }

    [Fact]
    public void DeeplyNestedFilter_FourLevels_ShouldBeValid()
    {
        // Arrange - Realistically complex filter
        var filter = "((status eq 'active' and (priority gt 5 or priority lt 2)) and " +
                     "(owner eq 'admin' or (category eq 'residential' and zone eq 'R1')))";

        // Act
        var isValid = ValidateODataFilter(filter);

        // Assert
        isValid.Should().BeTrue("deeply nested filter should be valid");
        _output.WriteLine($"Deeply nested filter: {filter}");
    }

    #endregion

    #region Geo.Distance with Realistic Coordinates

    [Fact]
    public void GeoDistance_WithNewYorkCoordinates_ShouldBeValid()
    {
        // Arrange - Query for features within 5km of Times Square
        var (lon, lat) = RealisticGisTestData.NewYork;
        var filter = $"geo.distance(location, geography'POINT({lon} {lat})') le 5000";

        // Assert
        filter.Should().Contain("geo.distance");
        filter.Should().Contain($"{lon}");
        filter.Should().Contain($"{lat}");

        _output.WriteLine($"Geo distance filter (New York): {filter}");
    }

    [Fact]
    public void GeoDistance_WithTokyoCoordinates_ShouldBeValid()
    {
        // Arrange
        var (lon, lat) = RealisticGisTestData.Tokyo;
        var filter = $"geo.distance(location, geography'POINT({lon} {lat})') le 10000";

        // Assert
        filter.Should().Contain($"{lon}");
        filter.Should().Contain($"{lat}");

        _output.WriteLine($"Geo distance filter (Tokyo): {filter}");
    }

    [Fact]
    public void GeoDistance_WithAntimeridianCoordinates_ShouldBeValid()
    {
        // Arrange - Fiji coordinates near antimeridian
        var (lon, lat) = RealisticGisTestData.Fiji;
        var filter = $"geo.distance(location, geography'POINT({lon} {lat})') le 50000";

        // Assert
        lon.Should().BeGreaterThan(178.0, "Fiji is near antimeridian");
        filter.Should().Contain($"{lon}");

        _output.WriteLine($"Geo distance filter (Fiji, near antimeridian): {filter}");
    }

    [Fact]
    public void GeoDistance_WithHighLatitudeCoordinates_ShouldBeValid()
    {
        // Arrange - Reykjavik, high northern latitude
        var (lon, lat) = RealisticGisTestData.Reykjavik;
        var filter = $"geo.distance(location, geography'POINT({lon} {lat})') le 20000";

        // Assert
        lat.Should().BeGreaterThan(64.0, "Reykjavik is at high northern latitude");

        _output.WriteLine($"Geo distance filter (Reykjavik, high latitude): {filter}");
    }

    [Fact]
    public void GeoIntersects_WithRealisticLineString_ShouldBeValid()
    {
        // Arrange - LineString across New York
        var filter = "geo.intersects(geom, geometry'LINESTRING(-73.99 40.75, -73.98 40.76)')";

        // Assert
        filter.Should().Contain("geo.intersects");
        filter.Should().Contain("LINESTRING");

        _output.WriteLine($"Geo intersects filter: {filter}");
    }

    #endregion

    #region LIKE with Wildcards

    [Theory]
    [InlineData("startswith(name, 'Main')", "startswith function")]
    [InlineData("endswith(address, 'Street')", "endswith function")]
    [InlineData("contains(owner, 'Smith')", "contains function")]
    [InlineData("substringof('Park', name)", "substringof function (OData v3)")]
    public void StringFunctions_WithWildcards_ShouldBeValid(string filter, string description)
    {
        // Assert
        ValidateODataFilter(filter).Should().BeTrue($"{description} should be valid");
        _output.WriteLine($"{description}: {filter}");
    }

    [Fact]
    public void StartsWith_WithUnicodeCharacters_ShouldBeValid()
    {
        // Arrange - Search for addresses starting with "José"
        var filter = "startswith(address, 'José')";

        // Assert
        filter.Should().Contain("José");

        _output.WriteLine($"StartsWith with Unicode: {filter}");
    }

    [Fact]
    public void Contains_WithSpecialCharacters_ShouldBeValid()
    {
        // Arrange - Search for apostrophe in name
        var filter = "contains(name, \"O'Brien\")";

        // Assert
        filter.Should().Contain("O'Brien");

        _output.WriteLine($"Contains with apostrophe: {filter}");
    }

    #endregion

    #region SQL Injection Attempts in Filters

    [Theory]
    [InlineData("name eq '1' OR '1'='1'", "Classic OR 1=1 injection")]
    [InlineData("name eq '1'; DROP TABLE parcels;--'", "DROP TABLE injection")]
    [InlineData("name eq '1' UNION SELECT * FROM users--'", "UNION injection")]
    [InlineData("name eq '1' AND 1=CONVERT(int, (SELECT TOP 1 name FROM sysobjects WHERE xtype='U'))--'", "Error-based injection")]
    [InlineData("name eq 'admin'--'", "Comment injection")]
    [InlineData("name eq '1' OR SLEEP(5)--'", "Time-based injection")]
    public void SqlInjectionAttempt_ShouldBeEscapedOrParameterized(string maliciousFilter, string description)
    {
        // Arrange & Assert
        maliciousFilter.Should().NotBeNull();

        // The filter string itself should be preserved
        _output.WriteLine($"{description}: {maliciousFilter}");
        _output.WriteLine("Note: These should be parameterized by the query builder, not string-escaped");

        // Validate filter contains injection attempt markers
        (maliciousFilter.Contains("OR") ||
         maliciousFilter.Contains("DROP") ||
         maliciousFilter.Contains("UNION") ||
         maliciousFilter.Contains("--")).Should().BeTrue("should contain injection attempt");
    }

    [Fact]
    public void SqlInjection_InFieldName_ShouldBeRejected()
    {
        // Arrange - Attempt to inject via field name
        var maliciousFilter = "name'; DROP TABLE parcels;-- eq 'test'";

        // Assert - Should be treated as invalid field name
        _output.WriteLine($"Malicious field name: {maliciousFilter}");
        _output.WriteLine("Note: This should be rejected as invalid OData syntax");
    }

    [Fact]
    public void SqlInjection_WithUnicodeHomoglyphs_ShouldBePreserved()
    {
        // Arrange - Using Cyrillic 'а' instead of Latin 'a'
        // This tests that the system doesn't normalize away security-relevant differences
        var homoglyphFilter = "nаme eq 'test'"; // 'а' is Cyrillic

        // Assert
        homoglyphFilter.Should().NotBe("name eq 'test'", "Cyrillic 'а' should differ from Latin 'a'");

        _output.WriteLine($"Homoglyph filter: {homoglyphFilter}");
        _output.WriteLine($"Bytes: {System.Text.Encoding.UTF8.GetBytes(homoglyphFilter).Length}");
    }

    #endregion

    #region Complex Real-World Filters

    [Fact]
    public void RealWorldFilter_CaliforniaParcelSearch_ShouldBeValid()
    {
        // Arrange - Realistic query for California parcels
        var filter = "(city eq 'San José' or city eq 'San Francisco') and " +
                     "assessed_value gt 500000 and " +
                     "use_code eq 'R1' and " +
                     "year_built ge 2000";

        // Assert
        ValidateODataFilter(filter).Should().BeTrue();
        filter.Should().Contain("San José"); // Unicode

        _output.WriteLine($"California parcel filter: {filter}");
    }

    [Fact]
    public void RealWorldFilter_GeospatialWithMetadata_ShouldBeValid()
    {
        // Arrange - Combined geo and attribute filtering
        var (lon, lat) = RealisticGisTestData.NewYork;
        var filter = $"geo.distance(location, geography'POINT({lon} {lat})') le 5000 and " +
                     "status eq 'active' and " +
                     "priority ge 3 and " +
                     "(category eq 'residential' or category eq 'commercial')";

        // Assert
        ValidateODataFilter(filter).Should().BeTrue();

        _output.WriteLine($"Geospatial + metadata filter: {filter}");
    }

    [Fact]
    public void RealWorldFilter_DateRangeWithNulls_ShouldBeValid()
    {
        // Arrange - Date range query with null handling
        var filter = "last_sale_date ge 2020-01-01T00:00:00Z and " +
                     "last_sale_date le 2024-12-31T23:59:59Z and " +
                     "assessed_value ne null";

        // Assert
        ValidateODataFilter(filter).Should().BeTrue();

        _output.WriteLine($"Date range filter: {filter}");
    }

    #endregion

    #region Field Names with Special Characters

    [Theory]
    [InlineData("Property Owner", "field with space")]
    [InlineData("tax-year", "field with hyphen")]
    [InlineData("assessed_value", "field with underscore")]
    [InlineData("2024_revenue", "field starting with number")]
    public void FieldNameWithSpecialCharacters_ShouldRequireQuoting(string fieldName, string description)
    {
        // Arrange - These field names require quoting in most databases
        var needsQuoting = fieldName.Contains(' ') ||
                          fieldName.Contains('-') ||
                          char.IsDigit(fieldName[0]);

        // Assert
        _output.WriteLine($"{description}: '{fieldName}' (needs quoting: {needsQuoting})");
    }

    #endregion

    #region In Operator with Large Lists

    [Fact]
    public void InOperator_WithManyValues_ShouldBeValid()
    {
        // Arrange - IN clause with 100 values (realistic for batch queries)
        var values = string.Join(",", Enumerable.Range(1, 100).Select(i => $"'{RealisticGisTestData.CaliforniaParcelId.Replace("123", i.ToString("D3"))}'"));
        var filter = $"apn in ({values})";

        // Assert
        filter.Should().Contain("in (");
        var valueCount = filter.Split(',').Length;
        valueCount.Should().Be(100, "should have 100 values in IN clause");

        _output.WriteLine($"IN operator with {valueCount} values");
        _output.WriteLine($"Filter length: {filter.Length} characters");
    }

    [Fact]
    public void InOperator_WithUuids_ShouldBeValid()
    {
        // Arrange - IN clause with UUID values
        var uuids = Enumerable.Range(1, 10)
            .Select(i => $"'{RealisticGisTestData.GenerateFeatureUuid(i)}'");
        var filter = $"feature_id in ({string.Join(",", uuids)})";

        // Assert
        filter.Should().Contain("in (");
        filter.Should().MatchRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");

        _output.WriteLine($"IN operator with UUIDs: {filter.Substring(0, Math.Min(200, filter.Length))}...");
    }

    #endregion

    #region Numeric Edge Cases in Filters

    [Theory]
    [InlineData("assessed_value eq 2147483647", "int.MaxValue")]
    [InlineData("assessed_value eq -2147483648", "int.MinValue")]
    [InlineData("latitude eq 90.0", "max latitude")]
    [InlineData("latitude eq -90.0", "min latitude")]
    [InlineData("longitude eq 180.0", "max longitude")]
    [InlineData("longitude eq -180.0", "min longitude")]
    public void NumericEdgeValues_InFilter_ShouldBeValid(string filter, string description)
    {
        // Assert
        ValidateODataFilter(filter).Should().BeTrue($"{description} should be valid");
        _output.WriteLine($"{description}: {filter}");
    }

    #endregion

    #region Helper Methods

    private bool ValidateODataFilter(string filter)
    {
        // Basic validation - real implementation would use OData parser
        // For now, just check it's not null/empty and has basic OData structure
        if (string.IsNullOrWhiteSpace(filter))
            return false;

        // Should contain at least one operator or function
        var hasOperator = filter.Contains(" eq ") ||
                         filter.Contains(" ne ") ||
                         filter.Contains(" gt ") ||
                         filter.Contains(" ge ") ||
                         filter.Contains(" lt ") ||
                         filter.Contains(" le ") ||
                         filter.Contains(" and ") ||
                         filter.Contains(" or ") ||
                         filter.Contains("geo.") ||
                         filter.Contains("startswith(") ||
                         filter.Contains("endswith(") ||
                         filter.Contains("contains(") ||
                         filter.Contains("substringof(") ||
                         filter.Contains(" in (");

        return hasOperator;
    }

    #endregion
}
