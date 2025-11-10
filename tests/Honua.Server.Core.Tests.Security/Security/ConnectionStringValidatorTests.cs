// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Security;
using NUnit.Framework;

namespace Honua.Server.Core.Tests.Security.Security;

[TestFixture]
[Category("Unit")]
[Category("Security")]
public class ConnectionStringValidatorTests
{
    [Test]
    public void Validate_ValidPostgresConnectionString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Host=localhost;Port=5432;Database=honua;Username=user;Password=pass";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "postgres");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_ValidMySqlConnectionString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=honua;Uid=user;Pwd=pass;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "mysql");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_ValidSqlServerConnectionString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=honua;User Id=user;Password=pass;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlserver");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_ValidSqliteConnectionString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Data Source=honua.db";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlite");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_SqliteInMemory_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Data Source=:memory:";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlite");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_EmptyConnectionString_ThrowsArgumentException()
    {
        // Act
        var act = () => ConnectionStringValidator.Validate(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Test]
    public void Validate_NullConnectionString_ThrowsArgumentException()
    {
        // Act
        var act = () => ConnectionStringValidator.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Test]
    public void Validate_WhitespaceConnectionString_ThrowsArgumentException()
    {
        // Act
        var act = () => ConnectionStringValidator.Validate("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Test]
    public void Validate_SqlInjectionWithDashComment_ThrowsArgumentException()
    {
        // Arrange
        var maliciousString = "Server=localhost;Database=honua;-- DROP TABLE users";

        // Act
        var act = () => ConnectionStringValidator.Validate(maliciousString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SQL comment*");
    }

    [Test]
    public void Validate_SqlInjectionWithBlockComment_ThrowsArgumentException()
    {
        // Arrange
        var maliciousString = "Server=localhost;Database=honua;/* malicious */";

        // Act
        var act = () => ConnectionStringValidator.Validate(maliciousString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*SQL comment*");
    }

    [Test]
    public void Validate_UnexpectedSingleQuotes_ThrowsArgumentException()
    {
        // Arrange
        var maliciousString = "Server=localhost;Database='honua' OR '1'='1'";

        // Act
        var act = () => ConnectionStringValidator.Validate(maliciousString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*unexpected single quotes*");
    }

    [Test]
    public void Validate_PasswordWithSingleQuotes_DoesNotThrow()
    {
        // Arrange - Single quotes are allowed in password values
        var connectionString = "Server=localhost;Database=honua;Password='my$ecretP@ss'";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_PwdWithSingleQuotes_DoesNotThrow()
    {
        // Arrange - Single quotes are allowed in Pwd values (MySQL shorthand)
        var connectionString = "Server=localhost;Database=honua;Pwd='my$ecretP@ss'";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_NullByteInjection_ThrowsArgumentException()
    {
        // Arrange
        var maliciousString = "Server=localhost\0;Database=honua";

        // Act
        var act = () => ConnectionStringValidator.Validate(maliciousString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*null bytes*");
    }

    [Test]
    public void Validate_NewlineInjection_ThrowsArgumentException()
    {
        // Arrange
        var maliciousString = "Server=localhost\nDatabase=honua";

        // Act
        var act = () => ConnectionStringValidator.Validate(maliciousString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*newline characters*");
    }

    [Test]
    public void Validate_CarriageReturnInjection_ThrowsArgumentException()
    {
        // Arrange
        var maliciousString = "Server=localhost\rDatabase=honua";

        // Act
        var act = () => ConnectionStringValidator.Validate(maliciousString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*newline characters*");
    }

    [Test]
    public void Validate_ExcessivelyLongConnectionString_ThrowsArgumentException()
    {
        // Arrange - Create a connection string longer than 4096 characters
        var longString = "Server=localhost;" + new string('A', 5000);

        // Act
        var act = () => ConnectionStringValidator.Validate(longString);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*exceeds maximum length*");
    }

    [Test]
    public void Validate_MaximumAllowedLength_DoesNotThrow()
    {
        // Arrange - 4096 characters is the maximum allowed
        var maxString = "Server=localhost;" + new string('A', 4079); // 4096 total

        // Act
        var act = () => ConnectionStringValidator.Validate(maxString);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_PostgresWithoutHost_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Database=honua;Username=user;Password=pass";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "postgres");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must specify a host*");
    }

    [Test]
    public void Validate_PostgresWithLocalhostImplied_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Database=honua;Username=user;Password=pass;Host=localhost";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "postgres");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_PostgresUriFormat_DoesNotThrow()
    {
        // Arrange
        var connectionString = "postgresql://user:pass@localhost:5432/honua";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "postgres");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_PostgresAlternateUriFormat_DoesNotThrow()
    {
        // Arrange
        var connectionString = "postgres://user:pass@localhost:5432/honua";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "postgres");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_MySqlWithoutServer_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Database=honua;Uid=user;Pwd=pass;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "mysql");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must specify a server*");
    }

    [Test]
    public void Validate_MySqlWithDataSource_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Data Source=localhost;Database=honua;Uid=user;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "mysql");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_SqlServerWithoutServer_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Database=honua;User Id=user;Password=pass;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlserver");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must specify a server*");
    }

    [Test]
    public void Validate_SqlServerWithLocalInstance_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=(local);Database=honua;Integrated Security=true;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlserver");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_SqliteWithoutDataSource_ThrowsArgumentException()
    {
        // Arrange
        var connectionString = "Version=3;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlite");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must specify a data source*");
    }

    [Test]
    public void Validate_SqliteWithDbExtension_DoesNotThrow()
    {
        // Arrange
        var connectionString = "honua.db";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlite");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_SqliteWithSqliteExtension_DoesNotThrow()
    {
        // Arrange
        var connectionString = "honua.sqlite";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlite");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_SqliteWithSqlite3Extension_DoesNotThrow()
    {
        // Arrange
        var connectionString = "honua.sqlite3";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "sqlite");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_UnknownProvider_DoesNotThrow()
    {
        // Arrange - Unknown providers should be allowed but receive no specific validation
        var connectionString = "some_connection_string";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "unknown_provider");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_NoProvider_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=honua;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, null);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_CaseInsensitiveProviderType_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=honua;";

        // Act & Assert - Should handle various casings
        var act1 = () => ConnectionStringValidator.Validate(connectionString, "POSTGRES");
        var act2 = () => ConnectionStringValidator.Validate(connectionString, "PostgreSQL");
        var act3 = () => ConnectionStringValidator.Validate(connectionString, "postgres");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
    }

    [Test]
    public void Validate_SqlServerMssqlAlias_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=honua;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "mssql");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_PostgresPostgisAlias_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=honua;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString, "postgis");

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_ComplexPasswordWithSpecialCharacters_DoesNotThrow()
    {
        // Arrange - Complex passwords with special characters should be allowed
        var connectionString = "Server=localhost;Database=honua;Password='P@$$w0rd!#$%^&*()_+-=[]{}|;:,.<>?'";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_IntegratedSecurity_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=honua;Integrated Security=true;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_ConnectionTimeout_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=honua;Connection Timeout=30;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString);

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Validate_MultipleParameters_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Server=localhost;Port=5432;Database=honua;Username=user;Password=pass;Pooling=true;MinPoolSize=5;MaxPoolSize=100;";

        // Act
        var act = () => ConnectionStringValidator.Validate(connectionString);

        // Assert
        act.Should().NotThrow();
    }
}
