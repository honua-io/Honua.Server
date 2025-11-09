// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Tests for SQL View schema detection functionality.
/// </summary>
public class SqlViewSchemaDetectorTests
{
    [Fact]
    public void DetectSchemaAsync_NullConnection_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id, name FROM cities"
        };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await detector.DetectSchemaAsync(null!, sqlView, "postgres", CancellationToken.None));
    }

    [Fact]
    public void DetectSchemaAsync_NullSqlView_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var mockConnection = new Mock<IDbConnection>();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await detector.DetectSchemaAsync(mockConnection.Object, null!, "postgres", CancellationToken.None));
    }

    [Fact]
    public void DetectSchemaAsync_NullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var mockConnection = new Mock<IDbConnection>();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id, name FROM cities"
        };

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await detector.DetectSchemaAsync(mockConnection.Object, sqlView, null!, CancellationToken.None));
    }

    [Fact]
    public void DetectSchemaAsync_UnsupportedProvider_ThrowsNotSupportedException()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var mockConnection = new Mock<IDbConnection>();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id, name FROM cities"
        };

        // Act & Assert
        Assert.ThrowsAsync<NotSupportedException>(async () =>
            await detector.DetectSchemaAsync(mockConnection.Object, sqlView, "unsupported", CancellationToken.None));
    }

    [Theory]
    [InlineData("postgres", "SELECT * FROM (SELECT id, name FROM cities) AS schema_detect LIMIT 0")]
    [InlineData("sqlserver", "SELECT TOP 0 * FROM (SELECT id, name FROM cities) AS schema_detect")]
    [InlineData("mysql", "SELECT * FROM (SELECT id, name FROM cities) AS schema_detect LIMIT 0")]
    [InlineData("sqlite", "SELECT * FROM (SELECT id, name FROM cities) AS schema_detect LIMIT 0")]
    public async Task DetectSchemaAsync_BuildsCorrectSchemaQuery(string provider, string expectedQuery)
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id, name FROM cities"
        };

        var mockCommand = new Mock<IDbCommand>();
        var mockConnection = new Mock<IDbConnection>();
        var mockReader = new Mock<IDataReader>();
        var mockSchemaTable = new System.Data.DataTable();

        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.ExecuteReaderAsync(It.IsAny<CommandBehavior>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);
        mockReader.Setup(r => r.GetSchemaTable()).Returns(mockSchemaTable);

        // Act
        try
        {
            await detector.DetectSchemaAsync(mockConnection.Object, sqlView, provider, CancellationToken.None);
        }
        catch
        {
            // Ignore exceptions - we're just testing the SQL generation
        }

        // Assert
        mockCommand.VerifySet(c => c.CommandText = expectedQuery, Times.Once);
    }

    [Fact]
    public async Task DetectSchemaAsync_ReturnsCorrectFieldTypes()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id, name, population FROM cities"
        };

        var mockCommand = new Mock<IDbCommand>();
        var mockConnection = new Mock<IDbConnection>();
        var mockReader = new Mock<IDataReader>();
        var mockParameters = new Mock<IDataParameterCollection>();

        // Create schema table
        var schemaTable = new System.Data.DataTable();
        schemaTable.Columns.Add("ColumnName", typeof(string));
        schemaTable.Columns.Add("DataType", typeof(Type));
        schemaTable.Columns.Add("AllowDBNull", typeof(bool));
        schemaTable.Columns.Add("ColumnSize", typeof(int));

        schemaTable.Rows.Add("id", typeof(int), false, 4);
        schemaTable.Rows.Add("name", typeof(string), true, 255);
        schemaTable.Rows.Add("population", typeof(long), true, 8);

        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.Parameters).Returns(mockParameters.Object);
        mockCommand.Setup(c => c.ExecuteReaderAsync(It.IsAny<CommandBehavior>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);
        mockReader.Setup(r => r.GetSchemaTable()).Returns(schemaTable);
        mockReader.Setup(r => r.Dispose());

        // Act
        var result = await detector.DetectSchemaAsync(mockConnection.Object, sqlView, "postgres", CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);

        var idField = result.First(f => f.Name == "id");
        Assert.Equal("esriFieldTypeInteger", idField.DataType);
        Assert.False(idField.Nullable);
        Assert.False(idField.Editable); // SQL views are read-only

        var nameField = result.First(f => f.Name == "name");
        Assert.Equal("esriFieldTypeString", nameField.DataType);
        Assert.True(nameField.Nullable);

        var populationField = result.First(f => f.Name == "population");
        Assert.Equal("esriFieldTypeBigInteger", populationField.DataType);
        Assert.True(populationField.Nullable);
    }

    [Fact]
    public async Task DetectSchemaAsync_WithParameters_AddsParametersToCommand()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id, name FROM cities WHERE region = :region AND population > :min_pop",
            Parameters = new[]
            {
                new SqlViewParameterDefinition
                {
                    Name = "region",
                    Type = "string",
                    DefaultValue = "west"
                },
                new SqlViewParameterDefinition
                {
                    Name = "min_pop",
                    Type = "integer",
                    DefaultValue = "100000"
                }
            }
        };

        var mockCommand = new Mock<IDbCommand>();
        var mockConnection = new Mock<IDbConnection>();
        var mockReader = new Mock<IDataReader>();
        var mockParameters = new Mock<IDataParameterCollection>();
        var addedParameters = new List<IDbDataParameter>();

        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.Parameters).Returns(mockParameters.Object);
        mockCommand.Setup(c => c.CreateParameter()).Returns(() =>
        {
            var param = new Mock<IDbDataParameter>();
            addedParameters.Add(param.Object);
            return param.Object;
        });
        mockParameters.Setup(p => p.Add(It.IsAny<object>()));
        mockCommand.Setup(c => c.ExecuteReaderAsync(It.IsAny<CommandBehavior>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);
        mockReader.Setup(r => r.GetSchemaTable()).Returns(new System.Data.DataTable());
        mockReader.Setup(r => r.Dispose());

        // Act
        await detector.DetectSchemaAsync(mockConnection.Object, sqlView, "postgres", CancellationToken.None);

        // Assert
        Assert.Equal(2, addedParameters.Count);
        mockParameters.Verify(p => p.Add(It.IsAny<object>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(typeof(int), "esriFieldTypeInteger")]
    [InlineData(typeof(long), "esriFieldTypeBigInteger")]
    [InlineData(typeof(double), "esriFieldTypeDouble")]
    [InlineData(typeof(decimal), "esriFieldTypeDouble")]
    [InlineData(typeof(string), "esriFieldTypeString")]
    [InlineData(typeof(DateTime), "esriFieldTypeDate")]
    [InlineData(typeof(bool), "esriFieldTypeSmallInteger")]
    [InlineData(typeof(Guid), "esriFieldTypeGUID")]
    public async Task DetectSchemaAsync_MapsTypesCorrectly(Type dotNetType, string expectedEsriType)
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT test_column FROM test_table"
        };

        var mockCommand = new Mock<IDbCommand>();
        var mockConnection = new Mock<IDbConnection>();
        var mockReader = new Mock<IDataReader>();
        var mockParameters = new Mock<IDataParameterCollection>();

        var schemaTable = new System.Data.DataTable();
        schemaTable.Columns.Add("ColumnName", typeof(string));
        schemaTable.Columns.Add("DataType", typeof(Type));
        schemaTable.Columns.Add("AllowDBNull", typeof(bool));
        schemaTable.Columns.Add("ColumnSize", typeof(int));
        schemaTable.Rows.Add("test_column", dotNetType, true, null);

        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.Parameters).Returns(mockParameters.Object);
        mockCommand.Setup(c => c.ExecuteReaderAsync(It.IsAny<CommandBehavior>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);
        mockReader.Setup(r => r.GetSchemaTable()).Returns(schemaTable);
        mockReader.Setup(r => r.Dispose());

        // Act
        var result = await detector.DetectSchemaAsync(mockConnection.Object, sqlView, "postgres", CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedEsriType, result[0].DataType);
    }

    [Fact]
    public async Task DetectSchemaAsync_NoSchemaTable_ThrowsInvalidOperationException()
    {
        // Arrange
        var detector = new SqlViewSchemaDetector();
        var sqlView = new SqlViewDefinition
        {
            Sql = "SELECT id FROM cities"
        };

        var mockCommand = new Mock<IDbCommand>();
        var mockConnection = new Mock<IDbConnection>();
        var mockReader = new Mock<IDataReader>();
        var mockParameters = new Mock<IDataParameterCollection>();

        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.Parameters).Returns(mockParameters.Object);
        mockCommand.Setup(c => c.ExecuteReaderAsync(It.IsAny<CommandBehavior>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);
        mockReader.Setup(r => r.GetSchemaTable()).Returns((System.Data.DataTable?)null);
        mockReader.Setup(r => r.Dispose());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await detector.DetectSchemaAsync(mockConnection.Object, sqlView, "postgres", CancellationToken.None));
    }
}
