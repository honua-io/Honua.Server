using System;
using System.Data.Common;
using System.Linq;
using Honua.Server.Core.Stac.Cql2;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

public sealed class Cql2SqlQueryBuilderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public Cql2SqlQueryBuilderTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    [Fact]
    public void BuildWhereClause_SimpleComparison_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""="",
            ""args"": [
                {""property"": ""cloud_cover""},
                10
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("=", whereClause);
        Assert.Single(command.Parameters.Cast<DbParameter>());
        Assert.Equal(10L, command.Parameters[0].Value);
    }

    [Fact]
    public void BuildWhereClause_LogicalAnd_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""<"",
                    ""args"": [
                        {""property"": ""cloud_cover""},
                        10
                    ]
                },
                {
                    ""op"": ""="",
                    ""args"": [
                        {""property"": ""sensor""},
                        ""Landsat""
                    ]
                }
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("AND", whereClause);
        Assert.Contains("<", whereClause);
        Assert.Contains("=", whereClause);
        Assert.Equal(2, command.Parameters.Count);
    }

    [Fact]
    public void BuildWhereClause_NotExpression_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""not"",
            ""args"": [
                {
                    ""op"": "">"",
                    ""args"": [
                        {""property"": ""cloud_cover""},
                        50
                    ]
                }
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("NOT", whereClause);
        Assert.Contains(">", whereClause);
        Assert.Single(command.Parameters.Cast<DbParameter>());
    }

    [Fact]
    public void BuildWhereClause_IsNull_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""isNull"",
            ""args"": [
                {""property"": ""end_datetime""}
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("IS NULL", whereClause);
        Assert.Empty(command.Parameters.Cast<DbParameter>());
    }

    [Fact]
    public void BuildWhereClause_Like_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""like"",
            ""args"": [
                {""property"": ""title""},
                ""%imagery%""
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("LIKE", whereClause);
        Assert.Equal(1, command.Parameters.Count);
    }

    [Fact]
    public void BuildWhereClause_Between_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""cloud_cover""},
                10,
                50
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("BETWEEN", whereClause);
        Assert.Contains("AND", whereClause);
        Assert.Equal(2, command.Parameters.Count);
    }

    [Fact]
    public void BuildWhereClause_In_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""sensor""},
                ""Landsat"",
                ""Sentinel"",
                ""MODIS""
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("IN", whereClause);
        Assert.Equal(3, command.Parameters.Count); // values only; field remains column reference
    }

    [Fact]
    public void BuildWhereClause_StandardProperty_MapsToColumn()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""="",
            ""args"": [
                {""property"": ""id""},
                ""item-123""
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("id", whereClause);
        Assert.DoesNotContain("properties_json", whereClause);
    }

    [Fact]
    public void BuildWhereClause_CustomProperty_UsesJsonExtraction()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""="",
            ""args"": [
                {""property"": ""custom_field""},
                ""value""
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("properties_json", whereClause);
        Assert.Contains("custom_field", whereClause);
    }

    [Fact]
    public void BuildWhereClause_ComplexNested_GeneratesCorrectSql()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""or"",
                    ""args"": [
                        {
                            ""op"": ""="",
                            ""args"": [
                                {""property"": ""sensor""},
                                ""Landsat""
                            ]
                        },
                        {
                            ""op"": ""="",
                            ""args"": [
                                {""property"": ""sensor""},
                                ""Sentinel""
                            ]
                        }
                    ]
                },
                {
                    ""op"": ""<"",
                    ""args"": [
                        {""property"": ""cloud_cover""},
                        10
                    ]
                }
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Contains("AND", whereClause);
        Assert.Contains("OR", whereClause);
        Assert.Equal(3, command.Parameters.Count);
    }

    [Fact]
    public void BuildWhereClause_ParameterizedQuery_PreventsSqlInjection()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""="",
            ""args"": [
                {""property"": ""title""},
                ""'; DROP TABLE stac_items; --""
            ]
        }";

        var expression = Cql2Parser.Parse(filterJson);
        var command = _connection.CreateCommand();
        var builder = new Cql2SqlQueryBuilder(command, Cql2SqlQueryBuilder.DatabaseProvider.SQLite);

        // Act
        var whereClause = builder.BuildWhereClause(expression);

        // Assert
        Assert.NotEmpty(whereClause);
        Assert.Single(command.Parameters.Cast<DbParameter>());

        // The dangerous string should be a parameter value, not part of the SQL
        var param = command.Parameters[0];
        Assert.Equal("'; DROP TABLE stac_items; --", param.Value);

        // The SQL should only contain parameter references, not the dangerous string itself
        Assert.DoesNotContain("DROP TABLE", whereClause);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection?.Dispose();
        _disposed = true;
    }
}
