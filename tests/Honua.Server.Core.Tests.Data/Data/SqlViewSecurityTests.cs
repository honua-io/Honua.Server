// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data;

/// <summary>
/// Security tests for SQL views to ensure protection against SQL injection attacks.
/// CRITICAL: These tests verify that SQL views cannot be exploited for SQL injection.
/// </summary>
public class SqlViewSecurityTests
{
    [Fact]
    public void ValidateSqlView_RejectsNonSelectStatements()
    {
        // Arrange
        var layer = CreateLayerWithSqlView("DROP TABLE users; --");

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            ValidateMetadata(layer));

        Assert.Contains("must start with SELECT", ex.Message);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE id = 1; DELETE FROM users; --")]
    [InlineData("SELECT * FROM users; DROP TABLE accounts; --")]
    [InlineData("SELECT * FROM users; INSERT INTO logs VALUES ('hacked'); --")]
    [InlineData("SELECT * FROM users; UPDATE users SET admin = 1; --")]
    public void ValidateSqlView_RejectsMultipleStatements(string maliciousSql)
    {
        // Arrange
        var layer = CreateLayerWithSqlView(maliciousSql);

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            ValidateMetadata(layer));

        Assert.True(
            ex.Message.Contains("dangerous keyword") || ex.Message.Contains("comments"),
            $"Expected to reject SQL: {maliciousSql}");
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE name = 'admin' --")]
    [InlineData("SELECT * FROM users /* comment */ WHERE id = 1")]
    [InlineData("SELECT * FROM users --comment\nWHERE id = 1")]
    public void ValidateSqlView_RejectsSqlComments(string sqlWithComments)
    {
        // Arrange
        var layer = CreateLayerWithSqlView(sqlWithComments);

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            ValidateMetadata(layer));

        Assert.Contains("comments which are not allowed", ex.Message);
    }

    [Theory]
    [InlineData("DROP TABLE users")]
    [InlineData("TRUNCATE TABLE sessions")]
    [InlineData("ALTER TABLE users ADD COLUMN is_admin")]
    [InlineData("CREATE TABLE malicious (data TEXT)")]
    [InlineData("EXEC sp_executesql @sql")]
    [InlineData("EXECUTE xp_cmdshell 'dir'")]
    [InlineData("GRANT ALL ON users TO public")]
    [InlineData("REVOKE ALL ON users FROM admin")]
    public void ValidateSqlView_RejectsDangerousKeywords(string dangerousSql)
    {
        // Arrange
        var layer = CreateLayerWithSqlView($"SELECT * FROM ({dangerousSql}) AS t");

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            ValidateMetadata(layer));

        Assert.Contains("dangerous keyword", ex.Message);
    }

    [Fact]
    public void ProcessSqlView_UsesParameterizedQueries()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM cities WHERE population > :min_population AND region = :region",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "min_population",
                    Type = "integer",
                    DefaultValue = "100000"
                },
                new SqlViewParameterDefinition
                {
                    Name = "region",
                    Type = "string",
                    DefaultValue = "west"
                }
            }
        };

        var requestParams = new Dictionary<string, string>
        {
            ["min_population"] = "500000",
            ["region"] = "east"
        };

        // Act
        var (sql, parameters) = SqlViewExecutor.ProcessSqlView(sqlView, requestParams, "test_layer");

        // Assert
        // The SQL should have parameterized placeholders, not direct values
        Assert.DoesNotContain("500000", sql);
        Assert.DoesNotContain("east", sql);
        Assert.Contains("@sqlview_min_population", sql);
        Assert.Contains("@sqlview_region", sql);

        // Parameters should be properly typed
        Assert.Equal(500000, parameters["sqlview_min_population"]);
        Assert.Equal("east", parameters["sqlview_region"]);
    }

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1; DELETE FROM users WHERE id > 0; --")]
    public void ProcessSqlView_PreventsSqlInjectionInParameters(string maliciousValue)
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM users WHERE name = :username",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "username",
                    Type = "string"
                }
            }
        };

        var requestParams = new Dictionary<string, string>
        {
            ["username"] = maliciousValue
        };

        // Act
        var (sql, parameters) = SqlViewExecutor.ProcessSqlView(sqlView, requestParams, "test_layer");

        // Assert
        // The malicious value should be in parameters, not in SQL
        Assert.DoesNotContain(maliciousValue, sql);
        Assert.Equal(maliciousValue, parameters["sqlview_username"]);

        // SQL should only contain the parameterized placeholder
        Assert.Contains("@sqlview_username", sql);
    }

    [Fact]
    public void ValidateSqlView_RejectsUnusedParameters()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            SqlView = new SqlViewDefinition
            {
                Sql = "SELECT id, name, geom FROM cities WHERE population > :min_population",
                Parameters = new[]
                {
                    new SqlViewParameterDefinition
                    {
                        Name = "min_population",
                        Type = "integer"
                    },
                    new SqlViewParameterDefinition
                    {
                        Name = "unused_param",  // This parameter is not used in SQL
                        Type = "string"
                    }
                }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() =>
            ValidateMetadata(layer));

        Assert.Contains("unused_param", ex.Message);
        Assert.Contains("not used in the SQL query", ex.Message);
    }

    [Fact]
    public void ProcessSqlView_EnforcesParameterValidation_AllowedValues()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM cities WHERE region = :region",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "region",
                    Type = "string",
                    Validation = new SqlViewParameterValidation
                    {
                        AllowedValues = new[] { "north", "south", "east", "west" }
                    }
                }
            }
        };

        var invalidParams = new Dictionary<string, string>
        {
            ["region"] = "invalid_region"
        };

        // Act & Assert
        var ex = Assert.Throws<SqlViewParameterValidationException>(() =>
            SqlViewExecutor.ProcessSqlView(sqlView, invalidParams, "test_layer"));

        Assert.Contains("region", ex.ParameterName);
        Assert.Contains("one of", ex.Message);
    }

    [Fact]
    public void ProcessSqlView_EnforcesParameterValidation_NumericRange()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM cities WHERE population > :min_population",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "min_population",
                    Type = "integer",
                    Validation = new SqlViewParameterValidation
                    {
                        Min = 0,
                        Max = 100000000
                    }
                }
            }
        };

        var invalidParams = new Dictionary<string, string>
        {
            ["min_population"] = "200000000"  // Exceeds max
        };

        // Act & Assert
        var ex = Assert.Throws<SqlViewParameterValidationException>(() =>
            SqlViewExecutor.ProcessSqlView(sqlView, invalidParams, "test_layer"));

        Assert.Contains("min_population", ex.ParameterName);
        Assert.Contains("at most", ex.Message);
    }

    [Fact]
    public void ProcessSqlView_EnforcesParameterValidation_RegexPattern()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM cities WHERE code = :city_code",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "city_code",
                    Type = "string",
                    Validation = new SqlViewParameterValidation
                    {
                        Pattern = "^[A-Z]{3}$"  // Only 3 uppercase letters
                    }
                }
            }
        };

        var invalidParams = new Dictionary<string, string>
        {
            ["city_code"] = "invalid123"  // Does not match pattern
        };

        // Act & Assert
        var ex = Assert.Throws<SqlViewParameterValidationException>(() =>
            SqlViewExecutor.ProcessSqlView(sqlView, invalidParams, "test_layer"));

        Assert.Contains("city_code", ex.ParameterName);
        Assert.Contains("pattern", ex.Message);
    }

    [Fact]
    public void ProcessSqlView_TypeConversion_Integer()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM cities WHERE id = :id",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "id",
                    Type = "integer"
                }
            }
        };

        var params1 = new Dictionary<string, string> { ["id"] = "123" };

        // Act
        var (_, parameters) = SqlViewExecutor.ProcessSqlView(sqlView, params1, "test_layer");

        // Assert
        Assert.IsType<int>(parameters["sqlview_id"]);
        Assert.Equal(123, parameters["sqlview_id"]);
    }

    [Fact]
    public void ProcessSqlView_TypeConversion_RejectsInvalidTypes()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM cities WHERE id = :id",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "id",
                    Type = "integer"
                }
            }
        };

        var invalidParams = new Dictionary<string, string>
        {
            ["id"] = "not_a_number"
        };

        // Act & Assert
        var ex = Assert.Throws<SqlViewParameterValidationException>(() =>
            SqlViewExecutor.ProcessSqlView(sqlView, invalidParams, "test_layer"));

        Assert.Contains("id", ex.ParameterName);
    }

    [Fact]
    public void ProcessSqlView_AppliesSecurityFilter()
    {
        // Arrange
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT * FROM users WHERE active = true",
            SecurityFilter = "deleted_at IS NULL",
            Parameters = Array.Empty<SqlViewParameterDefinition>()
        };

        var emptyParams = new Dictionary<string, string>();

        // Act
        var (sql, _) = SqlViewExecutor.ProcessSqlView(sqlView, emptyParams, "test_layer");

        // Assert
        Assert.Contains("deleted_at IS NULL", sql);
        Assert.Contains("__sqlview_secure", sql);
    }

    // Helper methods

    private LayerDefinition CreateLayerWithSqlView(string sql)
    {
        return new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            SqlView = new SqlViewDefinition
            {
                Sql = sql,
                Parameters = Array.Empty<SqlViewParameterDefinition>()
            }
        };
    }

    private void ValidateMetadata(LayerDefinition layer)
    {
        // This simulates the validation that happens in MetadataSnapshot
        // We'll call the same validation logic
        var layers = new[] { layer };
        var services = new[]
        {
            new ServiceDefinition
            {
                Id = "test_service",
                Title = "Test Service",
                FolderId = "test_folder",
                ServiceType = "wfs",
                DataSourceId = "test_datasource"
            }
        };
        var folders = new[] { new FolderDefinition { Id = "test_folder" } };
        var dataSources = new[]
        {
            new DataSourceDefinition
            {
                Id = "test_datasource",
                Provider = "postgis",
                ConnectionString = "Host=localhost;Database=test"
            }
        };
        var catalog = new CatalogDefinition { Id = "test_catalog" };

        // This will call ValidateMetadata which includes ValidateSqlView
        _ = new MetadataSnapshot(catalog, folders, dataSources, services, layers);
    }
}
