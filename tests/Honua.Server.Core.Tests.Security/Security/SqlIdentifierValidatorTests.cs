using System;
using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Security;

public class SqlIdentifierValidatorTests
{
    [Theory]
    [InlineData("valid_identifier")]
    [InlineData("ValidIdentifier")]
    [InlineData("_identifier")]
    [InlineData("identifier123")]
    [InlineData("my_table_name")]
    [InlineData("schema.table")]
    [InlineData("database.schema.table")]
    public void ValidateIdentifier_WithValidIdentifiers_Succeeds(string identifier)
    {
        // Act & Assert - should not throw
        SqlIdentifierValidator.ValidateIdentifier(identifier);
    }

    [Theory]
    [InlineData("\"quoted\"", "quoted")]
    [InlineData("`quoted`", "quoted")]
    [InlineData("[quoted]", "quoted")]
    [InlineData("\"schema\".\"table\"", "schema.table")]
    [InlineData("`schema`.`table`", "schema.table")]
    [InlineData("[schema].[table]", "schema.table")]
    [InlineData("\"feature-id\"", "feature-id")]       // Quoted identifiers can contain hyphens
    [InlineData("`table-name`", "table-name")]         // Quoted identifiers can contain hyphens
    [InlineData("[my-column]", "my-column")]           // Quoted identifiers can contain hyphens
    public void ValidateIdentifier_WithQuotedIdentifiers_Succeeds(string quotedIdentifier, string expectedUnquoted)
    {
        // Act & Assert - should not throw
        SqlIdentifierValidator.ValidateIdentifier(quotedIdentifier);

        var actual = quotedIdentifier
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);

        actual.Should().Be(expectedUnquoted);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidateIdentifier_WithNullOrWhitespace_Throws(string? identifier)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier!));
        Assert.Contains("cannot be null or whitespace", exception.Message);
    }

    [Theory]
    [InlineData("table; DROP TABLE users--")]
    [InlineData("table\"; DROP TABLE users--")]
    [InlineData("table'; DELETE FROM")]
    [InlineData("table OR 1=1")]
    [InlineData("table--comment")]
    [InlineData("table/*comment*/")]
    [InlineData("table;")]
    [InlineData("table\n")]
    [InlineData("table\r\n")]
    [InlineData("table\t")]
    [InlineData("table name")]  // Space not allowed
    [InlineData("table@name")]  // @ not allowed
    [InlineData("table#name")]  // # not allowed
    [InlineData("table$name")]  // $ not allowed
    [InlineData("123table")]    // Cannot start with number
    public void ValidateIdentifier_WithMaliciousOrInvalidInput_Throws(string identifier)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier));
        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData("table]; DROP TABLE users--")]
    [InlineData("table`; DROP TABLE users--")]
    [InlineData("table\"; DELETE FROM")]
    public void ValidateIdentifier_WithSqlInjectionAttempts_Throws(string identifier)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier));
        Assert.NotNull(exception);
    }

    [Fact]
    public void ValidateIdentifier_WithTooLongIdentifier_Throws()
    {
        // Arrange - Create identifier longer than max length
        var longIdentifier = new string('a', SqlIdentifierValidator.MaxIdentifierLength + 1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(longIdentifier));
        Assert.Contains("exceeds maximum length", exception.Message);
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("DROP")]
    [InlineData("DELETE")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("EXEC")]
    [InlineData("EXECUTE")]
    [InlineData("SCHEMA")]  // Added test case for schema-qualified names
    [InlineData("TABLE")]   // Added test case for schema-qualified names
    public void ValidateIdentifier_WithUnquotedReservedKeywords_Succeeds(string keyword)
    {
        // Reserved keywords are allowed because all query builders quote identifiers
        // The validator is permissive and relies on the query builders to quote properly
        // Act & Assert - should not throw
        SqlIdentifierValidator.ValidateIdentifier(keyword);
    }

    [Theory]
    [InlineData("\"SELECT\"")]
    [InlineData("`DROP`")]
    [InlineData("[DELETE]")]
    public void ValidateIdentifier_WithQuotedReservedKeywords_Succeeds(string quotedKeyword)
    {
        // Act & Assert - should not throw
        SqlIdentifierValidator.ValidateIdentifier(quotedKeyword);
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithValidIdentifier_ReturnsQuoted()
    {
        // Arrange
        var identifier = "my_table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        Assert.Equal("\"my_table\"", result);
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithQualifiedName_ReturnsQuoted()
    {
        // Arrange
        var identifier = "schema.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        Assert.Equal("\"schema\".\"table\"", result);
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithEmbeddedQuotes_EscapesCorrectly()
    {
        // Arrange
        // Input is an already-quoted identifier: "my"table"
        // After unquoting: my"table
        // After re-quoting with escaping: "my""table"
        var identifier = "\"my\"table\"";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        // The internal quote should be escaped by doubling
        Assert.Equal("\"my\"\"table\"", result);
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithValidIdentifier_ReturnsQuoted()
    {
        // Arrange
        var identifier = "my_table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        Assert.Equal("`my_table`", result);
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithQualifiedName_ReturnsQuoted()
    {
        // Arrange
        var identifier = "database.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        Assert.Equal("`database`.`table`", result);
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithEmbeddedBackticks_EscapesCorrectly()
    {
        // Arrange
        // Input is an already-quoted identifier: `my`table`
        // After unquoting: my`table
        // After re-quoting with escaping: `my``table`
        var identifier = "`my`table`";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        // The internal backtick should be escaped by doubling
        Assert.Equal("`my``table`", result);
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithValidIdentifier_ReturnsQuoted()
    {
        // Arrange
        var identifier = "my_table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        Assert.Equal("[my_table]", result);
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithQualifiedName_ReturnsQuoted()
    {
        // Arrange
        var identifier = "schema.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        Assert.Equal("[schema].[table]", result);
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithEmbeddedBrackets_EscapesCorrectly()
    {
        // Arrange
        // Input is an already-quoted identifier: [my]table]
        // After unquoting: my]table
        // After re-quoting with escaping: [my]]table]
        var identifier = "[my]table]";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        // The internal bracket should be escaped by doubling
        Assert.Equal("[my]]table]", result);
    }

    [Fact]
    public void ValidateAndQuoteSqlite_WithValidIdentifier_ReturnsQuoted()
    {
        // Arrange
        var identifier = "my_table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlite(identifier);

        // Assert
        Assert.Equal("\"my_table\"", result);
    }

    [Fact]
    public void ValidateAndQuoteSqlite_WithQualifiedName_ReturnsQuoted()
    {
        // Arrange
        var identifier = "main.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlite(identifier);

        // Assert
        Assert.Equal("\"main\".\"table\"", result);
    }

    [Theory]
    [InlineData("table\"; DROP TABLE users--")]
    [InlineData("table]; DELETE FROM users--")]
    [InlineData("table`; DROP DATABASE--")]
    public void ValidateAndQuote_WithMaliciousInput_Throws(string maliciousInput)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuotePostgres(maliciousInput));
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuoteMySql(maliciousInput));
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuoteSqlServer(maliciousInput));
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuoteSqlite(maliciousInput));
    }

    [Fact]
    public void TryValidateIdentifier_WithValidIdentifier_ReturnsTrue()
    {
        // Arrange
        var identifier = "valid_table";

        // Act
        var result = SqlIdentifierValidator.TryValidateIdentifier(identifier, out var errorMessage);

        // Assert
        Assert.True(result);
        Assert.Empty(errorMessage);
    }

    [Fact]
    public void TryValidateIdentifier_WithInvalidIdentifier_ReturnsFalseWithMessage()
    {
        // Arrange
        var identifier = "table; DROP TABLE users--";

        // Act
        var result = SqlIdentifierValidator.TryValidateIdentifier(identifier, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errorMessage);
        Assert.Contains("invalid characters", errorMessage);
    }

    [Theory]
    [InlineData("users")]
    [InlineData("orders")]
    [InlineData("products")]
    [InlineData("customers")]
    public void ValidateAndQuote_WithCommonTableNames_Succeeds(string tableName)
    {
        // Act & Assert - should not throw
        var postgres = SqlIdentifierValidator.ValidateAndQuotePostgres(tableName);
        var mysql = SqlIdentifierValidator.ValidateAndQuoteMySql(tableName);
        var sqlserver = SqlIdentifierValidator.ValidateAndQuoteSqlServer(tableName);
        var sqlite = SqlIdentifierValidator.ValidateAndQuoteSqlite(tableName);

        Assert.NotEmpty(postgres);
        Assert.NotEmpty(mysql);
        Assert.NotEmpty(sqlserver);
        Assert.NotEmpty(sqlite);
    }

    [Theory]
    [InlineData("user_id")]
    [InlineData("created_at")]
    [InlineData("first_name")]
    [InlineData("last_name")]
    public void ValidateAndQuote_WithCommonColumnNames_Succeeds(string columnName)
    {
        // Act & Assert - should not throw
        var postgres = SqlIdentifierValidator.ValidateAndQuotePostgres(columnName);
        var mysql = SqlIdentifierValidator.ValidateAndQuoteMySql(columnName);
        var sqlserver = SqlIdentifierValidator.ValidateAndQuoteSqlServer(columnName);
        var sqlite = SqlIdentifierValidator.ValidateAndQuoteSqlite(columnName);

        Assert.NotEmpty(postgres);
        Assert.NotEmpty(mysql);
        Assert.NotEmpty(sqlserver);
        Assert.NotEmpty(sqlite);
    }
}
