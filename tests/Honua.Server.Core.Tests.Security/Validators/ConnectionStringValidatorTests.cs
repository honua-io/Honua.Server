// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Validators;

public class ConnectionStringValidatorTests
{
    [Theory]
    [InlineData("Host=localhost;Database=mydb;Username=user;Password=pass")]
    [InlineData("Server=localhost;Database=testdb;Uid=root;Pwd=password")]
    [InlineData("Data Source=localhost;Initial Catalog=db;User ID=sa;Password=pwd")]
    [InlineData("Data Source=mydb.db")]
    public void Validate_WithValidConnectionStrings_DoesNotThrow(string connectionString)
    {
        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithNullOrEmpty_ThrowsArgumentException(string? connectionString)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(connectionString!));
        exception.Message.Should().Contain("Connection string cannot be empty");
    }

    [Fact]
    public void Validate_WithSqlCommentDashes_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost;Database=test;--DROP DATABASE test";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("SQL comment");
    }

    [Fact]
    public void Validate_WithSqlCommentBlockStart_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost;Database=test;/*DROP DATABASE*/";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("SQL comment");
    }

    [Fact]
    public void Validate_WithUnexpectedSingleQuotes_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost;Database='test';User='admin'";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("unexpected single quotes");
    }

    [Fact]
    public void Validate_WithPasswordContainingSingleQuote_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=test;Password='p@ss'word'";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_WithPwdContainingSingleQuote_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=test;Pwd='p@ss'word'";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_WithNullByte_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost\0;Database=test";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("null bytes");
    }

    [Fact]
    public void Validate_WithNewline_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost\nDatabase=test";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("newline characters");
    }

    [Fact]
    public void Validate_WithCarriageReturn_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost\rDatabase=test";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("newline characters");
    }

    [Fact]
    public void Validate_WithExcessivelyLongConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var longConnectionString = "Server=localhost;" + new string('x', 5000);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(longConnectionString));
        exception.Message.Should().Contain("exceeds maximum length");
    }

    [Fact]
    public void Validate_WithMaxLengthConnectionString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;" + new string('x', 4000);

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("postgis")]
    [InlineData("postgres")]
    [InlineData("postgresql")]
    public void Validate_PostgresWithHostParameter_DoesNotThrow(string providerType)
    {
        // Arrange
        var connectionString = "Host=localhost;Database=mydb;Username=user;Password=pass";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, providerType));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("postgis")]
    [InlineData("postgres")]
    [InlineData("postgresql")]
    public void Validate_PostgresWithServerParameter_DoesNotThrow(string providerType)
    {
        // Arrange
        var connectionString = "Server=localhost;Database=mydb;Username=user;Password=pass";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, providerType));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("postgres://user:pass@localhost/mydb")]
    [InlineData("postgresql://user:pass@localhost:5432/testdb")]
    public void Validate_PostgresWithUriFormat_DoesNotThrow(string connectionString)
    {
        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "postgres"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_PostgresWithoutHost_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Database=mydb;Username=user;Password=pass";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(connectionString, "postgres"));
        exception.Message.Should().Contain("must specify a host");
    }

    [Fact]
    public void Validate_PostgresWithLocalhost_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Database=mydb;Username=user;Password=pass;localhost";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "postgres"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_MySqlWithServerParameter_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=testdb;Uid=root;Pwd=password";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "mysql"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_MySqlWithHostParameter_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=testdb;Uid=root;Pwd=password";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "mysql"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_MySqlWithDataSourceParameter_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Data Source=localhost;Database=testdb;Uid=root;Pwd=password";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "mysql"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_MySqlWithoutServer_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Database=testdb;Uid=root;Pwd=password";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(connectionString, "mysql"));
        exception.Message.Should().Contain("must specify a server");
    }

    [Fact]
    public void Validate_MySqlWithLocalhost_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Database=testdb;Uid=root;Pwd=password;localhost";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "mysql"));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mssql")]
    public void Validate_SqlServerWithServerParameter_DoesNotThrow(string providerType)
    {
        // Arrange
        var connectionString = "Server=localhost;Database=testdb;User ID=sa;Password=pwd";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, providerType));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mssql")]
    public void Validate_SqlServerWithDataSourceParameter_DoesNotThrow(string providerType)
    {
        // Arrange
        var connectionString = "Data Source=localhost;Initial Catalog=testdb;User ID=sa;Password=pwd";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, providerType));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_SqlServerWithoutServer_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Initial Catalog=testdb;User ID=sa;Password=pwd";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(connectionString, "sqlserver"));
        exception.Message.Should().Contain("must specify a server");
    }

    [Fact]
    public void Validate_SqlServerWithLocalParameter_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=(local);Database=testdb;Integrated Security=true";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "sqlserver"));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("Data Source=mydb.db")]
    [InlineData("Data Source=test.sqlite")]
    [InlineData("Data Source=database.sqlite3")]
    [InlineData("Data Source=:memory:")]
    public void Validate_SqliteWithValidDataSource_DoesNotThrow(string connectionString)
    {
        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "sqlite"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_SqliteWithDbExtension_DoesNotThrow()
    {
        // Arrange
        var connectionString = "/path/to/database.db";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "sqlite"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_SqliteWithoutValidDataSource_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Version=3;Cache Size=10000";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(connectionString, "sqlite"));
        exception.Message.Should().Contain("must specify a data source");
    }

    [Fact]
    public void Validate_WithUnknownProvider_DoesNotThrowForValidConnectionString()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=test";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "unknownProvider"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_CaseInsensitiveProviderType_WorksCorrectly()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=mydb";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "POSTGRES"));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("Server=localhost;Database=test';DROP TABLE users;--")]
    [InlineData("Host=localhost/*malicious*/;Database=test")]
    [InlineData("Server=localhost\0Database=test")]
    public void Validate_WithInjectionAttempts_ThrowsArgumentException(string maliciousConnectionString)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithMultipleSqlComments_ThrowsArgumentException()
    {
        // Arrange
        var maliciousConnectionString = "Server=localhost;--comment1;Database=test;--comment2";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(maliciousConnectionString));
        exception.Message.Should().Contain("SQL comment");
    }

    [Fact]
    public void Validate_PostgresWithLocalhostInString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Database=mydb;localhost=true;Username=user";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString, "postgres"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_CaseInsensitivePasswordKeyword_AllowsSingleQuotes()
    {
        // Arrange
        var connectionString = "Server=localhost;PASSWORD='test'";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_CaseInsensitivePwdKeyword_AllowsSingleQuotes()
    {
        // Arrange
        var connectionString = "Server=localhost;PWD='test'";

        // Act
        var exception = Record.Exception(() => ConnectionStringValidator.Validate(connectionString));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Validate_WithNormalSingleQuoteInNonPasswordField_Throws()
    {
        // Arrange
        var connectionString = "Server=localhost;Database='testdb'";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringValidator.Validate(connectionString));
        exception.Message.Should().Contain("unexpected single quotes");
    }
}
