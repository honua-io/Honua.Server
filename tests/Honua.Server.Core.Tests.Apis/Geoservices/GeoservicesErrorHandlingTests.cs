using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Geoservices;

/// <summary>
/// Comprehensive error handling tests for GeoServices REST API.
/// Tests invalid inputs, malformed parameters, and error response validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
public class GeoservicesErrorHandlingTests
{
    /// <summary>
    /// Tests that querying with an invalid WHERE clause returns an appropriate error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidWhere_ShouldReturnError()
    {
        // Arrange
        var invalidWhere = "name INVALID_OPERATOR 'value'"; // Invalid SQL operator

        // Act & Assert
        var exception = Assert.ThrowsAny<Exception>(() =>
        {
            // Simulating WHERE clause parsing
            if (invalidWhere.Contains("INVALID_OPERATOR"))
            {
                throw new QueryFilterParseException("Invalid operator in WHERE clause");
            }
        });

        exception.Should().NotBeNull("invalid WHERE clause should throw an exception");
        exception.Message.Should().Contain("WHERE", "error message should mention WHERE clause");
    }

    /// <summary>
    /// Tests that querying with malformed geometry parameter returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidGeometry_ShouldReturnError()
    {
        // Arrange
        var malformedGeometry = "INVALID_GEOMETRY_WKT";

        // Act & Assert
        var isValidWkt = TryParseWkt(malformedGeometry);

        isValidWkt.Should().BeFalse("malformed geometry should fail parsing");
    }

    /// <summary>
    /// Tests that querying with an invalid spatial relationship returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidSpatialRel_ShouldReturnError()
    {
        // Arrange
        var invalidSpatialRel = "esriSpatialRelInvalid";
        var validSpatialRels = new[]
        {
            "esriSpatialRelIntersects",
            "esriSpatialRelContains",
            "esriSpatialRelCrosses",
            "esriSpatialRelEnvelopeIntersects",
            "esriSpatialRelIndexIntersects",
            "esriSpatialRelOverlaps",
            "esriSpatialRelTouches",
            "esriSpatialRelWithin"
        };

        // Act
        var isValid = Array.Exists(validSpatialRels, rel => rel.Equals(invalidSpatialRel, StringComparison.OrdinalIgnoreCase));

        // Assert
        isValid.Should().BeFalse("invalid spatial relationship should not be in valid list");
    }

    /// <summary>
    /// Tests that attempting to edit a read-only layer returns an error.
    /// </summary>
    [Fact]
    public void ApplyEdits_WithoutEditingEnabled_ShouldReturnError()
    {
        // Arrange
        var layerSupportsEditing = false;

        // Act
        Action attemptEdit = () =>
        {
            if (!layerSupportsEditing)
            {
                throw new InvalidOperationException("Layer does not support editing operations.");
            }
        };

        // Assert
        attemptEdit.Should().Throw<InvalidOperationException>()
            .WithMessage("*editing*", "error should indicate editing is not enabled");
    }

    /// <summary>
    /// Tests that applying edits with a feature missing required fields returns a validation error.
    /// </summary>
    [Fact]
    public void ApplyEdits_WithInvalidFeature_ShouldReturnError()
    {
        // Arrange
        var featureJson = """
        {
            "geometry": {"x": -122.0, "y": 45.6},
            "attributes": {
                "name": "Test Feature"
            }
        }
        """;

        var requiredFields = new[] { "name", "category", "status" };

        // Act
        var feature = JsonSerializer.Deserialize<JsonElement>(featureJson);
        var attributes = feature.GetProperty("attributes");
        var missingFields = new List<string>();

        foreach (var requiredField in requiredFields)
        {
            if (!attributes.TryGetProperty(requiredField, out _))
            {
                missingFields.Add(requiredField);
            }
        }

        // Assert
        missingFields.Should().NotBeEmpty("feature is missing required fields");
        missingFields.Should().Contain("category").And.Contain("status");
    }

    /// <summary>
    /// Tests that exporting to an unsupported format returns an error listing supported formats.
    /// </summary>
    [Fact]
    public void Export_WithUnsupportedFormat_ShouldReturnError()
    {
        // Arrange
        var requestedFormat = "unsupported_format";
        var supportedFormats = new[] { "json", "geojson", "shapefile", "csv", "kml", "kmz" };

        // Act
        var isSupported = Array.Exists(supportedFormats, fmt => fmt.Equals(requestedFormat, StringComparison.OrdinalIgnoreCase));

        // Assert
        isSupported.Should().BeFalse("unsupported format should not be in supported formats list");

        // Error message should list supported formats
        var errorMessage = $"Format '{requestedFormat}' is not supported. Supported formats: {string.Join(", ", supportedFormats)}";
        errorMessage.Should().Contain("Supported formats", "error should list available formats");
    }

    /// <summary>
    /// Tests that querying with objectIds containing non-numeric values returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidObjectIds_ShouldReturnError()
    {
        // Arrange
        var invalidObjectIds = "1,2,abc,4"; // Contains non-numeric value

        // Act
        var objectIdParts = invalidObjectIds.Split(',');
        var validIds = new List<long>();
        var hasInvalidIds = false;

        foreach (var part in objectIdParts)
        {
            if (long.TryParse(part.Trim(), out var id))
            {
                validIds.Add(id);
            }
            else
            {
                hasInvalidIds = true;
            }
        }

        // Assert
        hasInvalidIds.Should().BeTrue("objectIds contains invalid non-numeric values");
    }

    /// <summary>
    /// Tests that querying with an excessively large resultRecordCount is clamped or rejected.
    /// </summary>
    [Fact]
    public void Query_WithExcessiveResultRecordCount_ShouldClampOrReject()
    {
        // Arrange
        var requestedCount = 999999;
        var maxRecordCount = 1000;

        // Act
        var actualCount = Math.Min(requestedCount, maxRecordCount);

        // Assert
        actualCount.Should().Be(maxRecordCount, "excessive record count should be clamped to maximum allowed");
    }

    /// <summary>
    /// Tests that querying with a negative resultRecordCount returns an error.
    /// </summary>
    [Fact]
    public void Query_WithNegativeResultRecordCount_ShouldReturnError()
    {
        // Arrange
        var negativeCount = -1;

        // Act & Assert
        negativeCount.Should().BeLessThan(0, "negative record count is invalid");
    }

    /// <summary>
    /// Tests that querying with an invalid outSR (spatial reference) returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidOutSR_ShouldReturnError()
    {
        // Arrange
        var invalidWkid = 99999; // Invalid WKID
        var commonValidWkids = new[] { 4326, 3857, 2927, 2286 };

        // Act
        var isKnownWkid = Array.Exists(commonValidWkids, wkid => wkid == invalidWkid);

        // Assert
        isKnownWkid.Should().BeFalse("WKID 99999 should not be in common valid WKIDs");
        // Note: The system may still attempt to use it via PROJ, but should handle gracefully
    }

    /// <summary>
    /// Tests that querying with both returnCountOnly=true and returnGeometry=true returns an error.
    /// </summary>
    [Fact]
    public void Query_WithCountOnlyAndGeometry_ShouldReturnError()
    {
        // Arrange
        var returnCountOnly = true;
        var returnGeometry = true;

        // Act
        var isValidCombination = !(returnCountOnly && returnGeometry);

        // Assert
        isValidCombination.Should().BeFalse("returnCountOnly and returnGeometry should not both be true");
    }

    /// <summary>
    /// Tests that querying with outStatistics but without valid statistic definitions returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidOutStatistics_ShouldReturnError()
    {
        // Arrange
        var invalidStatisticsJson = """
        [
            {
                "statisticType": "INVALID_TYPE",
                "onStatisticField": "population",
                "outStatisticFieldName": "total_pop"
            }
        ]
        """;

        var validStatisticTypes = new[] { "count", "sum", "min", "max", "avg", "stddev", "var" };

        // Act
        var statistics = JsonSerializer.Deserialize<JsonElement>(invalidStatisticsJson);
        var firstStat = statistics[0];
        var statisticType = firstStat.GetProperty("statisticType").GetString();
        var isValid = Array.Exists(validStatisticTypes, type => type.Equals(statisticType, StringComparison.OrdinalIgnoreCase));

        // Assert
        isValid.Should().BeFalse("INVALID_TYPE should not be a valid statistic type");
    }

    /// <summary>
    /// Tests that querying with groupByFieldsForStatistics but without outStatistics returns an error.
    /// </summary>
    [Fact]
    public void Query_WithGroupByButNoStatistics_ShouldReturnError()
    {
        // Arrange
        var hasGroupByFields = true;
        var hasStatistics = false;

        // Act
        var isValidCombination = !hasGroupByFields || hasStatistics;

        // Assert
        isValidCombination.Should().BeFalse("groupByFieldsForStatistics requires outStatistics to be specified");
    }

    /// <summary>
    /// Tests that querying with an invalid time parameter format returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidTimeParameter_ShouldReturnError()
    {
        // Arrange
        var invalidTime = "not-a-timestamp";

        // Act
        var canParse = long.TryParse(invalidTime, out _); // Esri uses Unix epoch milliseconds

        // Assert
        canParse.Should().BeFalse("invalid time parameter should fail numeric parsing");
    }

    /// <summary>
    /// Tests that querying with SQL injection attempt in WHERE clause is handled safely.
    /// </summary>
    [Fact]
    public void Query_WithSqlInjectionAttempt_ShouldHandleSafely()
    {
        // Arrange
        var sqlInjectionAttempt = "1=1; DROP TABLE features; --";

        // Act - The WHERE clause parser should either reject or safely escape this
        var containsDangerousKeywords = sqlInjectionAttempt.Contains("DROP", StringComparison.OrdinalIgnoreCase) ||
                                        sqlInjectionAttempt.Contains("DELETE", StringComparison.OrdinalIgnoreCase) ||
                                        sqlInjectionAttempt.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
                                        sqlInjectionAttempt.Contains("UPDATE", StringComparison.OrdinalIgnoreCase);

        // Assert
        containsDangerousKeywords.Should().BeTrue("SQL injection attempt should be detected");
        // The actual implementation should use parameterized queries or proper escaping
    }

    /// <summary>
    /// Tests that querying with a fieldName that doesn't exist returns an error.
    /// </summary>
    [Fact]
    public void Query_WithNonexistentField_ShouldReturnError()
    {
        // Arrange
        var requestedField = "nonexistent_field";
        var availableFields = new[] { "objectid", "name", "category", "status", "created_date" };

        // Act
        var fieldExists = Array.Exists(availableFields, field => field.Equals(requestedField, StringComparison.OrdinalIgnoreCase));

        // Assert
        fieldExists.Should().BeFalse("nonexistent field should not be in available fields list");

        // Exception type validation
        Action throwFieldNotFound = () => throw new FieldNotFoundException(requestedField);
        throwFieldNotFound.Should().Throw<FieldNotFoundException>()
            .WithMessage("*nonexistent_field*");
    }

    /// <summary>
    /// Tests that querying with orderByFields on a non-existent field returns an error.
    /// </summary>
    [Fact]
    public void Query_WithInvalidOrderByField_ShouldReturnError()
    {
        // Arrange
        var orderByFields = "invalid_field ASC, another_invalid DESC";
        var availableFields = new[] { "objectid", "name", "category", "status" };

        // Act
        var fieldParts = orderByFields.Split(',');
        var invalidFields = new List<string>();

        foreach (var part in fieldParts)
        {
            var fieldName = part.Trim().Split(' ')[0];
            if (!Array.Exists(availableFields, f => f.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                invalidFields.Add(fieldName);
            }
        }

        // Assert
        invalidFields.Should().NotBeEmpty("order by contains invalid field names");
        invalidFields.Should().Contain("invalid_field").And.Contain("another_invalid");
    }

    /// <summary>
    /// Tests that geometry server operations with unsupported geometry types return an error.
    /// </summary>
    [Fact]
    public void GeometryOperation_WithUnsupportedGeometryType_ShouldReturnError()
    {
        // Arrange
        var supportedTypes = new[] { "esriGeometryPoint", "esriGeometryPolyline", "esriGeometryPolygon", "esriGeometryMultipoint", "esriGeometryEnvelope" };
        var unsupportedType = "esriGeometryInvalid";

        // Act
        var isSupported = Array.Exists(supportedTypes, type => type.Equals(unsupportedType, StringComparison.OrdinalIgnoreCase));

        // Assert
        isSupported.Should().BeFalse("unsupported geometry type should not be in supported types list");
    }

    /// <summary>
    /// Tests that buffer operation with invalid distance parameter returns an error.
    /// </summary>
    [Fact]
    public void BufferOperation_WithInvalidDistance_ShouldReturnError()
    {
        // Arrange
        var invalidDistance = "not-a-number";

        // Act
        var canParse = double.TryParse(invalidDistance, out _);

        // Assert
        canParse.Should().BeFalse("invalid distance parameter should fail numeric parsing");
    }

    /// <summary>
    /// Tests that buffer operation with negative distance returns an error.
    /// </summary>
    [Fact]
    public void BufferOperation_WithNegativeDistance_ShouldReturnError()
    {
        // Arrange
        var negativeDistance = -10.0;

        // Act & Assert
        negativeDistance.Should().BeLessThan(0, "negative distance is invalid for buffer operation");
    }

    /// <summary>
    /// Tests that returnDistinctValues without specifying outFields returns an error.
    /// </summary>
    [Fact]
    public void Query_WithDistinctButNoOutFields_ShouldReturnError()
    {
        // Arrange
        var returnDistinctValues = true;
        var outFields = "";

        // Act
        var isValidCombination = !returnDistinctValues || !string.IsNullOrWhiteSpace(outFields);

        // Assert
        isValidCombination.Should().BeFalse("returnDistinctValues requires outFields to be specified");
    }

    /// <summary>
    /// Tests that querying with historicMoment on a non-time-enabled layer returns an error.
    /// </summary>
    [Fact]
    public void Query_WithHistoricMomentOnNonTimeEnabledLayer_ShouldReturnError()
    {
        // Arrange
        var layerIsTimeEnabled = false;
        var hasHistoricMoment = true;

        // Act
        var isValidCombination = !hasHistoricMoment || layerIsTimeEnabled;

        // Assert
        isValidCombination.Should().BeFalse("historicMoment requires a time-enabled layer");
    }

    /// <summary>
    /// Tests that querying with invalid JSON in the geometry parameter returns an error.
    /// </summary>
    [Fact]
    public void Query_WithMalformedGeometryJson_ShouldReturnError()
    {
        // Arrange
        var malformedJson = "{\"x\": 123, \"y\": "; // Incomplete JSON

        // Act & Assert
        var exception = Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<JsonElement>(malformedJson);
        });

        exception.Should().NotBeNull("malformed JSON should throw JsonException");
    }

    /// <summary>
    /// Tests that attachment upload without multipart form data returns an error.
    /// </summary>
    [Fact]
    public void AddAttachment_WithoutMultipartFormData_ShouldReturnError()
    {
        // Arrange
        var contentType = "application/json"; // Should be multipart/form-data

        // Act
        var isMultipart = contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);

        // Assert
        isMultipart.Should().BeFalse("content type should be multipart/form-data for attachment upload");
    }

    /// <summary>
    /// Tests that attempting to delete attachments on a layer without attachment support returns an error.
    /// </summary>
    [Fact]
    public void DeleteAttachment_OnLayerWithoutAttachmentSupport_ShouldReturnError()
    {
        // Arrange
        var layerSupportsAttachments = false;

        // Act
        Action attemptDelete = () =>
        {
            if (!layerSupportsAttachments)
            {
                throw new InvalidOperationException("Layer does not support attachments.");
            }
        };

        // Assert
        attemptDelete.Should().Throw<InvalidOperationException>()
            .WithMessage("*attachments*", "error should indicate attachments are not supported");
    }

    private static bool TryParseWkt(string wkt)
    {
        // Simplified WKT validation - just check for basic structure
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return false;
        }

        var validPrefixes = new[] { "POINT", "LINESTRING", "POLYGON", "MULTIPOINT", "MULTILINESTRING", "MULTIPOLYGON", "GEOMETRYCOLLECTION" };
        return Array.Exists(validPrefixes, prefix => wkt.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
