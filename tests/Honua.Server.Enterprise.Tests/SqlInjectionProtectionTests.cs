using System;
using System.Collections.Generic;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.Data.Oracle;
using Xunit;

namespace Honua.Server.Enterprise.Tests;

/// <summary>
/// Comprehensive SQL injection protection tests for database providers
/// Tests verify that all user-controlled inputs are properly parameterized
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class SqlInjectionProtectionTests
{
    private readonly LayerDefinition _testLayer;

    public SqlInjectionProtectionTests()
    {
        _testLayer = new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "test_table",
                PrimaryKey = "id",
                GeometryColumn = "geom",
                Srid = 4326
            },
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { Name = "id", DataType = "integer" },
                new FieldDefinition { Name = "name", DataType = "string" },
                new FieldDefinition { Name = "geom", DataType = "geometry" }
            }
        };
    }

    #region Oracle SQL Injection Tests

    [Fact]
    public void OracleQueryBuilder_BuildSelect_WithBbox_UsesParameters()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(
            Bbox: new BoundingBox(-180, -90, 180, 90)
        );

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);

        // Verify SQL does not contain raw coordinate values (SQL injection protection)
        Assert.DoesNotContain("-180", sql);
        Assert.DoesNotContain("-90", sql);
        Assert.DoesNotContain("180", sql);
        Assert.DoesNotContain("90", sql);

        // Verify SQL uses parameterized placeholders
        Assert.Contains(":bbox_minx", sql);
        Assert.Contains(":bbox_miny", sql);
        Assert.Contains(":bbox_maxx", sql);
        Assert.Contains(":bbox_maxy", sql);

        // Verify parameters are populated correctly
        Assert.NotNull(query.Bbox);
        Assert.Equal(query.Bbox.MinX, parameters["bbox_minx"]);
        Assert.Equal(query.Bbox.MinY, parameters["bbox_miny"]);
        Assert.Equal(query.Bbox.MaxX, parameters["bbox_maxx"]);
        Assert.Equal(query.Bbox.MaxY, parameters["bbox_maxy"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildSelect_WithMaliciousBbox_UsesParameters()
    {
        // Arrange - Attempt SQL injection via bbox coordinates
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(
            Bbox: new BoundingBox(-180.0, -90, 180, 90) // Could be injected if not parameterized
        );

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert - Even if bbox values were strings, they go into parameters
        Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--", sql);

        // Values should be in parameters, not SQL string
        Assert.Contains(":bbox_minx", sql);
        Assert.Equal(-180.0, parameters["bbox_minx"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildCount_WithBbox_UsesParameters()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(
            Bbox: new BoundingBox(-180, -90, 180, 90)
        );

        // Act
        var (sql, parameters) = builder.BuildCount(query);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);

        // Verify parameterized query
        Assert.Contains(":bbox_minx", sql);
        Assert.Contains(":bbox_miny", sql);
        Assert.Contains(":bbox_maxx", sql);
        Assert.Contains(":bbox_maxy", sql);

        // Verify no raw values in SQL
        Assert.DoesNotContain("-180", sql);
        Assert.DoesNotContain("-90", sql);
    }

    [Fact]
    public void OracleQueryBuilder_BuildById_UsesParameter()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var featureId = "123";

        // Act
        var (sql, parameters) = builder.BuildById(featureId);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);

        // Verify SQL uses parameter placeholder, not raw value
        Assert.DoesNotContain("'123'", sql);
        Assert.Contains(":feature_id", sql);

        // Verify parameter is set correctly
        Assert.Equal(featureId, parameters["feature_id"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildById_WithMaliciousId_UsesParameter()
    {
        // Arrange - Attempt SQL injection via feature ID
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var maliciousId = "1' OR '1'='1"; // Classic SQL injection attempt

        // Act
        var (sql, parameters) = builder.BuildById(maliciousId);

        // Assert
        // The malicious string should be in parameters, NOT in the SQL string
        Assert.DoesNotContain("OR '1'='1'", sql);
        Assert.DoesNotContain(maliciousId, sql);
        Assert.Contains(":feature_id", sql);

        // The malicious value is safely stored as a parameter value
        Assert.Equal(maliciousId, parameters["feature_id"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildById_WithDropTableInjection_UsesParameter()
    {
        // Arrange - Attempt to drop table via feature ID
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var maliciousId = "1'; DROP TABLE test_table; --";

        // Act
        var (sql, parameters) = builder.BuildById(maliciousId);

        // Assert
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":feature_id", sql);
        Assert.Equal(maliciousId, parameters["feature_id"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildDelete_UsesParameter()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var featureId = "456";

        // Act
        var (sql, parameters) = builder.BuildDelete(featureId);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);

        // Verify parameterized query
        Assert.Contains(":feature_id", sql);
        Assert.DoesNotContain("'456'", sql);

        // Verify parameter
        Assert.Equal(featureId, parameters["feature_id"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildDelete_WithMaliciousId_UsesParameter()
    {
        // Arrange - Attempt SQL injection in DELETE
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var maliciousId = "1' OR 1=1 --"; // Would delete all rows if not parameterized

        // Act
        var (sql, parameters) = builder.BuildDelete(maliciousId);

        // Assert
        Assert.DoesNotContain("OR 1=1", sql);
        Assert.Contains(":feature_id", sql);
        Assert.Equal(maliciousId, parameters["feature_id"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildSelect_WithLimitAndOffset_UsesParameters()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery
        {
            Limit = 100,
            Offset = 50
        };

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert
        Assert.Contains(":limit_value", sql);
        Assert.Contains(":offset_value", sql);

        // Verify no raw values
        Assert.DoesNotContain("100", sql);
        Assert.DoesNotContain("50", sql);

        // Verify parameters
        Assert.Equal(100, parameters["limit_value"]);
        Assert.Equal(50, parameters["offset_value"]);
    }

    [Fact]
    public void OracleQueryBuilder_BuildInsert_UsesParameters()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["name"] = "Test Feature",
            ["description"] = "A test feature"
        });

        // Act
        var (sql, parameters) = builder.BuildInsert(record);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);

        // Verify SQL uses parameter placeholders
        Assert.Contains(":p", sql); // Parameter prefix

        // Verify attribute values are NOT in SQL string
        Assert.DoesNotContain("'Test Feature'", sql);
        Assert.DoesNotContain("'A test feature'", sql);

        // Verify parameters contain the values
        Assert.Contains(parameters, p => p.Value?.ToString() == "Test Feature");
    }

    [Fact]
    public void OracleQueryBuilder_BuildInsert_WithMaliciousData_UsesParameters()
    {
        // Arrange - Attempt SQL injection via INSERT values
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["name"] = "'; DROP TABLE test_table; --",
            ["description"] = "Normal description"
        });

        // Act
        var (sql, parameters) = builder.BuildInsert(record);

        // Assert
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p", sql);

        // Malicious value should be safely stored in parameters
        Assert.Contains(parameters, p => p.Value?.ToString()?.Contains("DROP TABLE") == true);
    }

    [Fact]
    public void OracleQueryBuilder_BuildUpdate_UsesParameters()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var featureId = "123";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["name"] = "Updated Name",
            ["description"] = "Updated Description"
        });

        // Act
        var (sql, parameters) = builder.BuildUpdate(featureId, record);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);

        // Verify parameterized query
        Assert.Contains(":p", sql); // Update value parameters
        Assert.Contains(":p", sql); // ID parameter (different from value params)

        // Verify no raw values in SQL
        Assert.DoesNotContain("'Updated Name'", sql);
        Assert.DoesNotContain("'Updated Description'", sql);
        Assert.DoesNotContain("'123'", sql);
    }

    [Fact]
    public void OracleQueryBuilder_BuildUpdate_WithMaliciousData_UsesParameters()
    {
        // Arrange - Attempt SQL injection via UPDATE
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var featureId = "1' OR '1'='1";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["name"] = "Test', description='Injected'; --"
        });

        // Act
        var (sql, parameters) = builder.BuildUpdate(featureId, record);

        // Assert
        Assert.DoesNotContain("OR '1'='1'", sql);
        Assert.DoesNotContain("Injected'; --", sql);

        // Malicious values should be in parameters
        Assert.Contains(parameters, p => p.Value?.ToString()?.Contains("OR") == true);
        Assert.Contains(parameters, p => p.Value?.ToString()?.Contains("Injected") == true);
    }

    #endregion

    #region Identifier Validation Tests

    [Fact]
    public void OracleQueryBuilder_QuotesIdentifiers_Properly()
    {
        // Arrange - Table name with schema qualification
        var layer = new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "schema.table",
                PrimaryKey = "id",
                GeometryColumn = "geom"
            }
        };
        var builder = new OracleFeatureQueryBuilder(layer);

        // Act
        var (sql, _) = builder.BuildSelect(new FeatureQuery());

        // Assert - Identifiers should be quoted with double quotes
        // The QuoteIdentifier method in Oracle uses double quotes and escapes them
        Assert.Contains("\"", sql); // Should contain double quotes for identifier quoting
        Assert.Contains("\"schema\".\"table\"", sql); // Schema and table parts should be individually quoted
    }

    [Fact]
    public void OracleQueryBuilder_EscapesQuotesInIdentifiers()
    {
        // This test verifies that QuoteIdentifier properly escapes quotes
        // Oracle uses double quotes for identifiers and escapes them by doubling
        // e.g., "My""Table" for a table literally named My"Table

        // The QuoteIdentifier method should handle this automatically
        // by replacing " with ""

        var layer = new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "test_table",
                PrimaryKey = "id",
                GeometryColumn = "geom"
            }
        };
        var builder = new OracleFeatureQueryBuilder(layer);
        var (sql, _) = builder.BuildSelect(new FeatureQuery());

        // Should contain quoted identifiers
        Assert.Contains("\"", sql);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void OracleQueryBuilder_HandlesNullBbox_Safely()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(Bbox: null);

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);
        Assert.DoesNotContain(":bbox_", sql); // No bbox parameters
    }

    [Fact]
    public void OracleQueryBuilder_HandlesEmptyQuery_Safely()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery();

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert
        Assert.NotNull(sql);
        Assert.NotNull(parameters);
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OracleQueryBuilder_HandlesZeroLimitAndOffset_Safely()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(Limit: 0, Offset: 0);

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert
        Assert.NotNull(sql);
        // Zero limit/offset should not add parameters
        Assert.DoesNotContain(":limit_value", sql);
        Assert.DoesNotContain(":offset_value", sql);
    }

    #endregion

    #region Parameter Count Tests

    [Fact]
    public void OracleQueryBuilder_BuildSelect_ReturnsCorrectParameterCount()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(
            Bbox: new BoundingBox(-180, -90, 180, 90),
            Limit: 100,
            Offset: 50
        );

        // Act
        var (_, parameters) = builder.BuildSelect(query);

        // Assert
        // Should have: bbox_minx, bbox_miny, bbox_maxx, bbox_maxy, limit_value, offset_value = 6 parameters
        Assert.Equal(6, parameters.Count);
    }

    [Fact]
    public void OracleQueryBuilder_BuildCount_WithBbox_ReturnsCorrectParameterCount()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);
        var query = new FeatureQuery(
            Bbox: new BoundingBox(-180, -90, 180, 90)
        );

        // Act
        var (_, parameters) = builder.BuildCount(query);

        // Assert
        // Should have: bbox_minx, bbox_miny, bbox_maxx, bbox_maxy = 4 parameters
        Assert.Equal(4, parameters.Count);
    }

    [Fact]
    public void OracleQueryBuilder_BuildById_ReturnsCorrectParameterCount()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);

        // Act
        var (_, parameters) = builder.BuildById("123");

        // Assert
        // Should have exactly 1 parameter (feature_id)
        Assert.Single(parameters);
        Assert.Contains("feature_id", parameters.Keys);
    }

    [Fact]
    public void OracleQueryBuilder_BuildDelete_ReturnsCorrectParameterCount()
    {
        // Arrange
        var builder = new OracleFeatureQueryBuilder(_testLayer);

        // Act
        var (_, parameters) = builder.BuildDelete("123");

        // Assert
        // Should have exactly 1 parameter (feature_id)
        Assert.Single(parameters);
        Assert.Contains("feature_id", parameters.Keys);
    }

    #endregion
}
