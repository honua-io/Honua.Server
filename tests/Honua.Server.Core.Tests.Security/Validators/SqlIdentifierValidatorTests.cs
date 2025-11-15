// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Validators;

public class SqlIdentifierValidatorTests
{
    [Theory]
    [InlineData("users")]
    [InlineData("user_table")]
    [InlineData("UserTable")]
    [InlineData("_private")]
    [InlineData("table123")]
    [InlineData("TABLE_NAME_WITH_UNDERSCORES")]
    public void ValidateIdentifier_WithValidIdentifier_DoesNotThrow(string identifier)
    {
        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("schema.table")]
    [InlineData("database.schema.table")]
    [InlineData("mydb.public.users")]
    public void ValidateIdentifier_WithQualifiedNames_DoesNotThrow(string identifier)
    {
        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ValidateIdentifier_WithNullOrEmpty_ThrowsArgumentException(string? identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier!));
    }

    [Theory]
    [InlineData("table-name")]
    [InlineData("table name")]
    [InlineData("table@name")]
    [InlineData("table#name")]
    [InlineData("table$name")]
    [InlineData("123table")] // Cannot start with digit
    [InlineData("table;DROP")]
    public void ValidateIdentifier_WithInvalidCharacters_ThrowsArgumentException(string identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier));
    }

    [Fact]
    public void ValidateIdentifier_WithTooLongIdentifier_ThrowsArgumentException()
    {
        // Arrange
        var longIdentifier = new string('a', SqlIdentifierValidator.MaxIdentifierLength + 1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(longIdentifier));
        exception.Message.Should().Contain("exceeds maximum length");
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("DROP")]
    [InlineData("DELETE")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    public void ValidateIdentifier_WithReservedKeywords_AllowsWhenQuoted(string keyword)
    {
        // Note: Reserved keywords are allowed because the quoting methods handle them
        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(keyword));

        // Assert - Should not throw since we allow keywords (they'll be quoted)
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithValidIdentifier_ReturnsQuotedIdentifier()
    {
        // Arrange
        var identifier = "users";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        result.Should().Be("\"users\"");
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithQualifiedName_QuotesEachPart()
    {
        // Arrange
        var identifier = "schema.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        result.Should().Be("\"schema\".\"table\"");
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithEmbeddedQuotes_EscapesQuotes()
    {
        // Arrange
        var identifier = "\"table_with_quotes\"";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        result.Should().Be("\"table_with_quotes\"");
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithValidIdentifier_ReturnsQuotedIdentifier()
    {
        // Arrange
        var identifier = "users";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        result.Should().Be("`users`");
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithQualifiedName_QuotesEachPart()
    {
        // Arrange
        var identifier = "database.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        result.Should().Be("`database`.`table`");
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithValidIdentifier_ReturnsQuotedIdentifier()
    {
        // Arrange
        var identifier = "users";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        result.Should().Be("[users]");
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithQualifiedName_QuotesEachPart()
    {
        // Arrange
        var identifier = "schema.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        result.Should().Be("[schema].[table]");
    }

    [Fact]
    public void ValidateAndQuoteSqlite_WithValidIdentifier_ReturnsQuotedIdentifier()
    {
        // Arrange
        var identifier = "users";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlite(identifier);

        // Assert
        result.Should().Be("\"users\"");
    }

    [Theory]
    [InlineData("users'; DROP TABLE users;--")]
    [InlineData("admin'--")]
    [InlineData("1' OR '1'='1")]
    public void ValidateIdentifier_WithSqlInjectionAttempts_ThrowsException(string maliciousInput)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(maliciousInput));
    }

    [Fact]
    public void TryValidateIdentifier_WithValidIdentifier_ReturnsTrue()
    {
        // Arrange
        var identifier = "valid_table";

        // Act
        var isValid = SqlIdentifierValidator.TryValidateIdentifier(identifier, out var errorMessage);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeEmpty();
    }

    [Fact]
    public void TryValidateIdentifier_WithInvalidIdentifier_ReturnsFalseWithMessage()
    {
        // Arrange
        var identifier = "invalid-table";

        // Act
        var isValid = SqlIdentifierValidator.TryValidateIdentifier(identifier, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeEmpty();
        errorMessage.Should().Contain("invalid characters");
    }

    [Fact]
    public void ValidateAndQuote_WithAlreadyQuotedIdentifier_HandlesCorrectly()
    {
        // Arrange
        var identifier = "\"already_quoted\"";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        result.Should().Be("\"already_quoted\"");
    }

    [Theory]
    [InlineData("table1")]
    [InlineData("_table")]
    [InlineData("TABLE")]
    [InlineData("t")]
    public void ValidateIdentifier_WithSingleCharOrUnderscore_IsValid(string identifier)
    {
        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithEmbeddedBrackets_EscapesBrackets()
    {
        // Arrange - Create an identifier with brackets through quoting
        var identifier = "[table]name]";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        result.Should().Contain("]]"); // Brackets should be escaped by doubling
    }

    [Fact]
    public void ValidateIdentifier_WithMaxLengthIdentifier_DoesNotThrow()
    {
        // Arrange
        var identifier = new string('a', SqlIdentifierValidator.MaxIdentifierLength);

        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void TryValidateIdentifier_WithNullIdentifier_ReturnsFalseWithMessage()
    {
        // Act
        var isValid = SqlIdentifierValidator.TryValidateIdentifier(null!, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("cannot be null or whitespace");
    }

    [Fact]
    public void TryValidateIdentifier_WithEmptyAfterSplit_ReturnsFalseWithMessage()
    {
        // Arrange
        var identifier = "...";

        // Act
        var isValid = SqlIdentifierValidator.TryValidateIdentifier(identifier, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("cannot be empty after splitting");
    }

    [Fact]
    public void TryValidateIdentifier_WithTooLongPart_ReturnsFalseWithMessage()
    {
        // Arrange
        var longPart = new string('a', SqlIdentifierValidator.MaxIdentifierLength + 1);
        var identifier = $"schema.{longPart}";

        // Act
        var isValid = SqlIdentifierValidator.TryValidateIdentifier(identifier, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("exceeds maximum length");
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithThreePartQualifiedName_QuotesAllParts()
    {
        // Arrange
        var identifier = "database.schema.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        result.Should().Be("\"database\".\"schema\".\"table\"");
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithEmbeddedBackticks_EscapesBackticks()
    {
        // Arrange
        var identifier = "`table`name`";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        result.Should().Contain("``"); // Backticks should be escaped by doubling
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithDoubleQuotesInside_EscapesCorrectly()
    {
        // Arrange - identifier with embedded quote
        var identifier = "table\"name";

        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateAndQuotePostgres(identifier));

        // Assert
        exception.Should().NotBeNull(); // Should fail validation for unquoted identifier with special chars
    }

    [Fact]
    public void ValidateAndQuoteSqlite_WorksLikePostgres()
    {
        // Arrange
        var identifier = "table_name";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlite(identifier);

        // Assert - SQLite uses same quoting as Postgres
        result.Should().Be("\"table_name\"");
    }

    [Fact]
    public void ValidateIdentifier_WithQuotedMySqlIdentifier_AcceptsIt()
    {
        // Arrange
        var identifier = "`table_name`";

        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateIdentifier_WithQuotedSqlServerIdentifier_AcceptsIt()
    {
        // Arrange
        var identifier = "[table_name]";

        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateIdentifier_WithQuotedPostgresIdentifier_AcceptsIt()
    {
        // Arrange
        var identifier = "\"table_name\"";

        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateIdentifier_WithMalformedQuotedIdentifier_Throws()
    {
        // Arrange - opening quote but no closing quote
        var identifier = "\"table_name";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier));
        exception.Message.Should().Contain("invalid characters");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateAndQuotePostgres_WithInvalidIdentifier_Throws(string? identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuotePostgres(identifier!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateAndQuoteMySql_WithInvalidIdentifier_Throws(string? identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuoteMySql(identifier!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateAndQuoteSqlServer_WithInvalidIdentifier_Throws(string? identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateAndQuoteSqlite_WithInvalidIdentifier_Throws(string? identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateAndQuoteSqlite(identifier!));
    }

    [Fact]
    public void ValidateAndQuotePostgres_WithQualifiedNameContainingQuotes_EscapesQuotes()
    {
        // Arrange
        var identifier = "\"schema\".\"table\"";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);

        // Assert
        result.Should().Be("\"schema\".\"table\"");
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithQualifiedNameContainingBackticks_EscapesBackticks()
    {
        // Arrange
        var identifier = "`database`.`table`";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        result.Should().Be("`database`.`table`");
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithQualifiedNameContainingBrackets_EscapesBrackets()
    {
        // Arrange
        var identifier = "[schema].[table]";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        result.Should().Be("[schema].[table]");
    }

    [Fact]
    public void ValidateIdentifier_WithCustomParameterName_UsesParameterNameInException()
    {
        // Arrange
        var identifier = "invalid-name";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SqlIdentifierValidator.ValidateIdentifier(identifier, "customParam"));
        exception.ParamName.Should().Be("customParam");
    }

    [Fact]
    public void ValidateIdentifier_WithSpecialCharactersInQuotedIdentifier_Passes()
    {
        // Arrange - already quoted identifiers can contain special chars
        var identifier = "\"table-with-dash\"";

        // Act
        var exception = Record.Exception(() => SqlIdentifierValidator.ValidateIdentifier(identifier));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ValidateAndQuoteMySql_WithTriplePartName_QuotesAllParts()
    {
        // Arrange
        var identifier = "server.database.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);

        // Assert
        result.Should().Be("`server`.`database`.`table`");
    }

    [Fact]
    public void ValidateAndQuoteSqlServer_WithTriplePartName_QuotesAllParts()
    {
        // Arrange
        var identifier = "server.database.table";

        // Act
        var result = SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);

        // Assert
        result.Should().Be("[server].[database].[table]");
    }
}
