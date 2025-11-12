// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Honua.Server.Core.Configuration.V2.Introspection;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Introspection;

public sealed class SchemaReaderFactoryTests
{
    [Theory]
    [InlineData("postgresql")]
    [InlineData("postgres")]
    [InlineData("npgsql")]
    public void CreateReader_PostgreSql_ReturnsPostgreSqlReader(string provider)
    {
        // Act
        var reader = SchemaReaderFactory.CreateReader(provider);

        // Assert
        Assert.NotNull(reader);
        Assert.IsType<PostgreSqlSchemaReader>(reader);
        Assert.Equal("postgresql", reader.ProviderName);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlite3")]
    public void CreateReader_Sqlite_ReturnsSqliteReader(string provider)
    {
        // Act
        var reader = SchemaReaderFactory.CreateReader(provider);

        // Assert
        Assert.NotNull(reader);
        Assert.IsType<SqliteSchemaReader>(reader);
        Assert.Equal("sqlite", reader.ProviderName);
    }

    [Fact]
    public void CreateReader_UnsupportedProvider_ThrowsNotSupportedException()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            SchemaReaderFactory.CreateReader("oracle"));
    }

    [Theory]
    [InlineData("Data Source=test.db", "sqlite")]
    [InlineData("Data Source=./data/mydb.sqlite", "sqlite")]
    [InlineData("Data Source=:memory:", "sqlite")]
    [InlineData("Host=localhost;Database=testdb;Username=user", "postgresql")]
    [InlineData("Server=localhost;Port=5432;Database=mydb", "postgresql")]
    public void DetectProvider_ValidConnectionString_DetectsCorrectly(string connectionString, string expected)
    {
        // Act
        var detected = SchemaReaderFactory.DetectProvider(connectionString);

        // Assert
        Assert.Equal(expected, detected);
    }

    [Fact]
    public void DetectProvider_UnknownConnectionString_ReturnsNull()
    {
        // Act
        var detected = SchemaReaderFactory.DetectProvider("some random string");

        // Assert
        Assert.Null(detected);
    }

    [Fact]
    public void GetSupportedProviders_ReturnsAllSupportedProviders()
    {
        // Act
        var providers = SchemaReaderFactory.GetSupportedProviders();

        // Assert
        Assert.NotEmpty(providers);
        Assert.Contains("postgresql", providers);
        Assert.Contains("sqlite", providers);
    }
}
