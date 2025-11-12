// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Honua.Server.Core.Configuration.V2.Introspection;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Introspection;

public sealed class ConfigurationGeneratorTests
{
    [Fact]
    public void GenerateConfiguration_EmptySchema_GeneratesHeader()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "testdb",
            Tables = new List<TableSchema>()
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema);

        // Assert
        Assert.Contains("# Honua Configuration 2.0 - Auto-generated from database", result);
        Assert.Contains("# Database: testdb", result);
        Assert.Contains("# Provider: postgresql", result);
        Assert.Contains("# Tables: 0", result);
    }

    [Fact]
    public void GenerateConfiguration_WithDataSourceBlock_GeneratesDataSource()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "testdb",
            Tables = new List<TableSchema>()
        };

        var options = new GenerationOptions
        {
            IncludeDataSourceBlock = true,
            DataSourceId = "my_db",
            IncludeServiceBlocks = GenerationOptions.Default.IncludeServiceBlocks,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("data_source \"my_db\"", result);
        Assert.Contains("provider   = \"postgresql\"", result);
    }

    [Fact]
    public void GenerateConfiguration_WithServiceBlocks_GeneratesServices()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Provider = "sqlite",
            DatabaseName = "test.db",
            Tables = new List<TableSchema>()
        };

        var options = new GenerationOptions
        {
            IncludeServiceBlocks = true,
            EnabledServices = new HashSet<string> { "odata", "ogc_api" },
            DataSourceId = GenerationOptions.Default.DataSourceId,
            IncludeDataSourceBlock = GenerationOptions.Default.IncludeDataSourceBlock,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("service \"odata\"", result);
        Assert.Contains("service \"ogc_api\"", result);
        Assert.Contains("enabled = true", result);
    }

    [Fact]
    public void GenerateConfiguration_WithSimpleTable_GeneratesLayerBlock()
    {
        // Arrange
        var table = new TableSchema
        {
            SchemaName = "public",
            TableName = "roads",
            Columns = new List<ColumnSchema>
            {
                new() { ColumnName = "id", DataType = "integer", IsNullable = false, IsPrimaryKey = true },
                new() { ColumnName = "name", DataType = "text", IsNullable = true }
            },
            PrimaryKeyColumns = new List<string> { "id" },
            RowCount = 1000
        };

        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "gis",
            Tables = new List<TableSchema> { table }
        };

        var options = new GenerationOptions
        {
            IncludeDataSourceBlock = false,
            IncludeServiceBlocks = false,
            DataSourceId = GenerationOptions.Default.DataSourceId,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("layer \"roads\"", result);
        Assert.Contains("title            = \"Roads\"", result);
        Assert.Contains("table            = \"public.roads\"", result);
        Assert.Contains("id_field         = \"id\"", result);
        Assert.Contains("display_field    = \"name\"", result);
        Assert.Contains("introspect_fields = true", result);
    }

    [Fact]
    public void GenerateConfiguration_WithGeometryTable_GeneratesGeometryBlock()
    {
        // Arrange
        var table = new TableSchema
        {
            SchemaName = "public",
            TableName = "parcels",
            Columns = new List<ColumnSchema>
            {
                new() { ColumnName = "id", DataType = "integer", IsNullable = false, IsPrimaryKey = true },
                new() { ColumnName = "parcel_number", DataType = "text", IsNullable = false },
                new() { ColumnName = "geom", DataType = "geometry", IsNullable = false }
            },
            PrimaryKeyColumns = new List<string> { "id" },
            GeometryColumn = new GeometryColumnInfo
            {
                ColumnName = "geom",
                GeometryType = "Polygon",
                Srid = 3857
            }
        };

        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "gis",
            Tables = new List<TableSchema> { table }
        };

        var options = new GenerationOptions
        {
            IncludeDataSourceBlock = false,
            IncludeServiceBlocks = false,
            DataSourceId = GenerationOptions.Default.DataSourceId,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("geometry {", result);
        Assert.Contains("column = \"geom\"", result);
        Assert.Contains("type   = \"Polygon\"", result);
        Assert.Contains("srid   = 3857", result);
    }

    [Fact]
    public void GenerateConfiguration_WithExplicitFields_GeneratesFieldsBlock()
    {
        // Arrange
        var table = new TableSchema
        {
            SchemaName = "public",
            TableName = "sensors",
            Columns = new List<ColumnSchema>
            {
                new() { ColumnName = "sensor_id", DataType = "integer", IsNullable = false, IsPrimaryKey = true },
                new() { ColumnName = "name", DataType = "text", IsNullable = false },
                new() { ColumnName = "value", DataType = "double precision", IsNullable = true }
            },
            PrimaryKeyColumns = new List<string> { "sensor_id" }
        };

        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "iot",
            Tables = new List<TableSchema> { table }
        };

        var options = new GenerationOptions
        {
            GenerateExplicitFields = true,
            IncludeDataSourceBlock = false,
            IncludeServiceBlocks = false,
            DataSourceId = GenerationOptions.Default.DataSourceId,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("introspect_fields = false", result);
        Assert.Contains("fields {", result);
        Assert.Contains("sensor_id", result);
        Assert.Contains("name", result);
        Assert.Contains("value", result);
        Assert.Contains("type = \"int\"", result);
        Assert.Contains("type = \"string\"", result);
        Assert.Contains("type = \"double\"", result);
    }

    [Fact]
    public void GenerateConfiguration_WithEnvironmentVariable_UsesEnvFunction()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "prod",
            Tables = new List<TableSchema>()
        };

        var options = new GenerationOptions
        {
            UseEnvironmentVariable = true,
            ConnectionStringEnvVar = "DATABASE_CONNECTION",
            DataSourceId = GenerationOptions.Default.DataSourceId,
            IncludeDataSourceBlock = GenerationOptions.Default.IncludeDataSourceBlock,
            IncludeServiceBlocks = GenerationOptions.Default.IncludeServiceBlocks,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("connection = env(\"DATABASE_CONNECTION\")", result);
    }

    [Fact]
    public void GenerateConfiguration_WithoutEnvironmentVariable_ShowsPlaceholder()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Provider = "sqlite",
            DatabaseName = "test.db",
            Tables = new List<TableSchema>()
        };

        var options = new GenerationOptions
        {
            UseEnvironmentVariable = false,
            DataSourceId = GenerationOptions.Default.DataSourceId,
            IncludeDataSourceBlock = GenerationOptions.Default.IncludeDataSourceBlock,
            IncludeServiceBlocks = GenerationOptions.Default.IncludeServiceBlocks,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("connection = \"<YOUR_CONNECTION_STRING>\"", result);
    }

    [Fact]
    public void GenerateConfiguration_WithConnectionPool_GeneratesPoolBlock()
    {
        // Arrange
        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "prod",
            Tables = new List<TableSchema>()
        };

        var options = new GenerationOptions
        {
            IncludeConnectionPool = true,
            DataSourceId = GenerationOptions.Default.DataSourceId,
            IncludeDataSourceBlock = GenerationOptions.Default.IncludeDataSourceBlock,
            IncludeServiceBlocks = GenerationOptions.Default.IncludeServiceBlocks,
            EnabledServices = GenerationOptions.Default.EnabledServices,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("pool {", result);
        Assert.Contains("min_size = 5", result);
        Assert.Contains("max_size = 20", result);
    }

    [Fact]
    public void GenerateConfiguration_WithServices_GeneratesServiceReferences()
    {
        // Arrange
        var table = new TableSchema
        {
            SchemaName = "public",
            TableName = "features",
            Columns = new List<ColumnSchema>
            {
                new() { ColumnName = "id", DataType = "integer", IsNullable = false, IsPrimaryKey = true }
            },
            PrimaryKeyColumns = new List<string> { "id" }
        };

        var schema = new DatabaseSchema
        {
            Provider = "postgresql",
            DatabaseName = "gis",
            Tables = new List<TableSchema> { table }
        };

        var options = new GenerationOptions
        {
            EnabledServices = new HashSet<string> { "odata", "ogc_api", "wfs" },
            IncludeDataSourceBlock = false,
            IncludeServiceBlocks = false,
            DataSourceId = GenerationOptions.Default.DataSourceId,
            UseEnvironmentVariable = GenerationOptions.Default.UseEnvironmentVariable,
            ConnectionStringEnvVar = GenerationOptions.Default.ConnectionStringEnvVar,
            IncludeConnectionPool = GenerationOptions.Default.IncludeConnectionPool,
            GenerateExplicitFields = GenerationOptions.Default.GenerateExplicitFields
        };

        // Act
        var result = ConfigurationGenerator.GenerateConfiguration(schema, options);

        // Assert
        Assert.Contains("services = [service.odata, service.ogc_api, service.wfs]", result);
    }
}
