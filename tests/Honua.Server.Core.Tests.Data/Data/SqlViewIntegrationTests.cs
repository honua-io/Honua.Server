// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data;

/// <summary>
/// Integration tests for SQL views demonstrating end-to-end functionality.
/// </summary>
public class SqlViewIntegrationTests
{
    [Fact]
    public void SqlViewQueryBuilder_BuildsCorrectSelectQuery()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>
        {
            ["min_population"] = "500000",
            ["region"] = "east"
        };

        var builder = new SqlViewQueryBuilder(layer, requestParams);
        var query = new FeatureQuery(Limit: 100);

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SELECT", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__sqlview", result.Sql);
        Assert.Contains("LIMIT", result.Sql, StringComparison.OrdinalIgnoreCase);

        // Parameters should be properly substituted
        Assert.Contains("@sqlview_min_population", result.Sql);
        Assert.Contains("@sqlview_region", result.Sql);

        // Parameter values should be typed correctly
        Assert.Equal(500000, result.Parameters["sqlview_min_population"]);
        Assert.Equal("east", result.Parameters["sqlview_region"]);
    }

    [Fact]
    public void SqlViewQueryBuilder_BuildsCorrectCountQuery()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>
        {
            ["min_population"] = "100000",
            ["region"] = "west"
        };

        var builder = new SqlViewQueryBuilder(layer, requestParams);
        var query = new FeatureQuery();

        // Act
        var result = builder.BuildCount(query);

        // Assert
        Assert.Contains("COUNT(*)", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__sqlview", result.Sql);
        Assert.Equal(100000, result.Parameters["sqlview_min_population"]);
        Assert.Equal("west", result.Parameters["sqlview_region"]);
    }

    [Fact]
    public void SqlViewQueryBuilder_BuildsByIdQuery()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>
        {
            ["min_population"] = "100000",
            ["region"] = "west"
        };

        var builder = new SqlViewQueryBuilder(layer, requestParams);

        // Act
        var result = builder.BuildById("123");

        // Assert
        Assert.Contains("WHERE", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("city_id", result.Sql);
        Assert.Contains("@feature_id", result.Sql);
        Assert.Equal("123", result.Parameters["feature_id"]);
    }

    [Fact]
    public void SqlViewQueryBuilder_AppliesDefaultValues()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>(); // No parameters provided

        var builder = new SqlViewQueryBuilder(layer, requestParams);
        var query = new FeatureQuery();

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        // Should use default values from parameter definitions
        Assert.Equal(100000, result.Parameters["sqlview_min_population"]);
        Assert.Equal("west", result.Parameters["sqlview_region"]);
    }

    [Fact]
    public void SqlViewQueryBuilder_IsSqlView_DetectsCorrectly()
    {
        // Arrange
        var sqlViewLayer = CreateSampleSqlViewLayer();
        var tableLayer = new LayerDefinition
        {
            Id = "table_layer",
            ServiceId = "demo",
            Title = "Table Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "cities",
                GeometryColumn = "geom",
                PrimaryKey = "id"
            }
        };

        // Act & Assert
        Assert.True(SqlViewQueryBuilder.IsSqlView(sqlViewLayer));
        Assert.False(SqlViewQueryBuilder.IsSqlView(tableLayer));
    }

    [Fact]
    public void SqlViewQueryBuilder_GetCommandTimeout_ReturnsConfiguredValue()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>();
        var builder = new SqlViewQueryBuilder(layer, requestParams);

        // Act
        var timeout = builder.GetCommandTimeout();

        // Assert
        Assert.NotNull(timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), timeout.Value);
    }

    [Fact]
    public void SqlViewQueryBuilder_IsReadOnly_ReturnsCorrectValue()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>();
        var builder = new SqlViewQueryBuilder(layer, requestParams);

        // Act
        var isReadOnly = builder.IsReadOnly();

        // Assert
        Assert.True(isReadOnly);
    }

    [Fact]
    public void SqlViewExecutor_ExtractParameterNames_FindsAllParameters()
    {
        // Arrange
        var sql = "SELECT * FROM cities WHERE population > :min_pop AND region = :region AND country = :country";

        // Act
        var parameters = SqlViewExecutor.ExtractParameterNames(sql);

        // Assert
        Assert.Equal(3, parameters.Count);
        Assert.Contains("min_pop", parameters, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("region", parameters, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("country", parameters, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlViewExecutor_ExtractParameterNames_HandlesNoParameters()
    {
        // Arrange
        var sql = "SELECT * FROM cities WHERE population > 100000";

        // Act
        var parameters = SqlViewExecutor.ExtractParameterNames(sql);

        // Assert
        Assert.Empty(parameters);
    }

    [Fact]
    public void CompleteWorkflow_WithAllParameterTypes()
    {
        // Arrange - Create a layer with various parameter types
        var layer = new LayerDefinition
        {
            Id = "multi_param_layer",
            ServiceId = "demo",
            Title = "Multi-Parameter Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            SqlView = new SqlViewDefinition
            {
                Sql = "SELECT id, name, value, is_active, created_at, geom FROM test_data WHERE value > :min_value AND is_active = :is_active AND created_at >= :start_date",
                Parameters = new[]
                {
                    new SqlViewParameterDefinition
                    {
                        Name = "min_value",
                        Type = "double",
                        DefaultValue = "0.0"
                    },
                    new SqlViewParameterDefinition
                    {
                        Name = "is_active",
                        Type = "boolean",
                        DefaultValue = "true"
                    },
                    new SqlViewParameterDefinition
                    {
                        Name = "start_date",
                        Type = "date",
                        DefaultValue = "2024-01-01"
                    }
                },
                TimeoutSeconds = 45,
                ReadOnly = true
            }
        };

        var requestParams = new Dictionary<string, string>
        {
            ["min_value"] = "123.45",
            ["is_active"] = "true",
            ["start_date"] = "2024-06-01"
        };

        var builder = new SqlViewQueryBuilder(layer, requestParams);

        // Act
        var result = builder.BuildSelect(new FeatureQuery(Limit: 50));

        // Assert
        Assert.NotNull(result);
        Assert.Contains("@sqlview_min_value", result.Sql);
        Assert.Contains("@sqlview_is_active", result.Sql);
        Assert.Contains("@sqlview_start_date", result.Sql);

        // Verify type conversions
        Assert.IsType<double>(result.Parameters["sqlview_min_value"]);
        Assert.Equal(123.45, result.Parameters["sqlview_min_value"]);

        Assert.IsType<bool>(result.Parameters["sqlview_is_active"]);
        Assert.True((bool)result.Parameters["sqlview_is_active"]!);

        Assert.IsType<DateOnly>(result.Parameters["sqlview_start_date"]);
    }

    [Fact]
    public void CompleteWorkflow_WithSorting()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>
        {
            ["min_population"] = "200000",
            ["region"] = "north"
        };

        var builder = new SqlViewQueryBuilder(layer, requestParams);
        var query = new FeatureQuery(
            Limit: 100,
            SortOrders: new[]
            {
                new FeatureSortOrder("population", FeatureSortDirection.Descending),
                new FeatureSortOrder("name", FeatureSortDirection.Ascending)
            });

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        Assert.Contains("ORDER BY", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ASC", result.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompleteWorkflow_WithPagination()
    {
        // Arrange
        var layer = CreateSampleSqlViewLayer();
        var requestParams = new Dictionary<string, string>
        {
            ["min_population"] = "50000",
            ["region"] = "south"
        };

        var builder = new SqlViewQueryBuilder(layer, requestParams);
        var query = new FeatureQuery(Limit: 25, Offset: 50);

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        Assert.Contains("LIMIT 25", result.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET 50", result.Sql, StringComparison.OrdinalIgnoreCase);
    }

    // Helper method to create a sample SQL view layer
    private LayerDefinition CreateSampleSqlViewLayer()
    {
        return new LayerDefinition
        {
            Id = "high_population_cities",
            ServiceId = "demo",
            Title = "High Population Cities",
            GeometryType = "Point",
            IdField = "city_id",
            GeometryField = "location",
            SqlView = new SqlViewDefinition
            {
                Sql = "SELECT city_id, name, population, region, location FROM cities WHERE population > :min_population AND region = :region",
                Parameters = new[]
                {
                    new SqlViewParameterDefinition
                    {
                        Name = "min_population",
                        Type = "integer",
                        DefaultValue = "100000",
                        Validation = new SqlViewParameterValidation
                        {
                            Min = 0,
                            Max = 100000000
                        }
                    },
                    new SqlViewParameterDefinition
                    {
                        Name = "region",
                        Type = "string",
                        DefaultValue = "west",
                        Validation = new SqlViewParameterValidation
                        {
                            AllowedValues = new[] { "north", "south", "east", "west" }
                        }
                    }
                },
                TimeoutSeconds = 30,
                ReadOnly = true
            }
        };
    }
}
