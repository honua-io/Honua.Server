using System;
using FluentAssertions;
using Honua.Cli.AI.Services.Execution;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Execution;

[Trait("Category", "Unit")]
public class CommandArgumentValidatorTests
{
    [Theory]
    [InlineData("valid_identifier")]
    [InlineData("valid-identifier")]
    [InlineData("valid.identifier")]
    [InlineData("ValidIdentifier123")]
    public void ValidateIdentifier_WithValidInput_ShouldNotThrow(string validInput)
    {
        // Act & Assert - should not throw
        CommandArgumentValidator.ValidateIdentifier(validInput, "test");
    }

    [Theory]
    [InlineData("; rm -rf /")]
    [InlineData("test && malicious")]
    [InlineData("test | cat /etc/passwd")]
    [InlineData("test `whoami`")]
    [InlineData("test $(whoami)")]
    [InlineData("test\nmalicious")]
    [InlineData("test;malicious")]
    [InlineData("test&malicious")]
    [InlineData("test>file")]
    [InlineData("test<file")]
    [InlineData("test*wildcard")]
    [InlineData("test[a-z]")]
    [InlineData("test{1,2}")]
    [InlineData("test'quote")]
    [InlineData("test\"quote")]
    [InlineData("test\\escape")]
    [InlineData("test~home")]
    [InlineData("test#comment")]
    public void ValidateIdentifier_WithInjectionAttempt_ShouldThrow(string maliciousInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateIdentifier(maliciousInput, "test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*shell metacharacters*");
    }

    [Theory]
    [InlineData("valid/path/to/file")]
    [InlineData("valid-path")]
    [InlineData("valid_path")]
    [InlineData("path.txt")]
    [InlineData("path/to/file.txt")]
    public void ValidatePath_WithValidInput_ShouldNotThrow(string validPath)
    {
        // Act & Assert - should not throw
        CommandArgumentValidator.ValidatePath(validPath, "test");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("path/../../../secret")]
    [InlineData("path; rm -rf /")]
    [InlineData("path && malicious")]
    [InlineData("path | cat /etc/passwd")]
    [InlineData("path`whoami`")]
    [InlineData("path$(whoami)")]
    public void ValidatePath_WithInjectionAttempt_ShouldThrow(string maliciousPath)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidatePath(maliciousPath, "test");

        // Assert
        var exception = act.Should().Throw<ArgumentException>().Which;
        var message = exception.Message.ToLower();
        (message.Contains("path traversal") || message.Contains("shell metacharacters"))
            .Should().BeTrue("because either path traversal or shell metacharacters should be detected");
    }

    [Theory]
    [InlineData("valid_database")]
    [InlineData("database123")]
    [InlineData("my_database_name")]
    public void ValidateDatabaseName_WithValidInput_ShouldNotThrow(string validName)
    {
        // Act & Assert - should not throw
        CommandArgumentValidator.ValidateDatabaseName(validName, "test");
    }

    [Theory]
    [InlineData("database-name")]
    [InlineData("database.name")]
    [InlineData("database; DROP TABLE users--")]
    [InlineData("database' OR '1'='1")]
    [InlineData("database`whoami`")]
    [InlineData("very_long_database_name_that_exceeds_the_maximum_allowed_length_for_database_names_in_postgresql_which_is_63_characters")]
    public void ValidateDatabaseName_WithInvalidInput_ShouldThrow(string invalidName)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateDatabaseName(invalidName, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("CREATE TABLE test (id INT)")]
    [InlineData("INSERT INTO users (name) VALUES ('test')")]
    [InlineData("UPDATE users SET name='test' WHERE id=1")]
    public void ValidateSQL_WithValidInput_ShouldNotThrow(string validSql)
    {
        // Act & Assert - should not throw
        // Note: Semicolons are blocked for security since we execute SQL via command line
        CommandArgumentValidator.ValidateSQL(validSql, "test");
    }

    [Theory]
    [InlineData("SELECT * FROM users; DROP TABLE users--")]
    [InlineData("SELECT * FROM users; DELETE FROM sensitive_data;")]
    [InlineData("SELECT * FROM users; UPDATE admin SET password='hacked';")]
    [InlineData("SELECT * FROM users; INSERT INTO logs VALUES ('hacked');")]
    [InlineData("SELECT * FROM users; EXEC xp_cmdshell('whoami');")]
    [InlineData("SELECT * FROM users; EXECUTE sp_executesql N'DROP TABLE users';")]
    [InlineData("SELECT * FROM users /* comment */ WHERE id=1")]
    [InlineData("SELECT * FROM users -- comment")]
    public void ValidateSQL_WithInjectionAttempt_ShouldThrow(string maliciousSql)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateSQL(maliciousSql, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("postgres://localhost:5432/database")]
    [InlineData("Host=localhost;Database=test;Username=user;Password=pass")]
    [InlineData("Server=localhost;Database=test;User=admin")]
    public void ValidateConnectionString_WithValidInput_ShouldNotThrow(string validConnectionString)
    {
        // Act & Assert - should not throw
        CommandArgumentValidator.ValidateConnectionString(validConnectionString, "test");
    }

    [Theory]
    [InlineData("postgres://localhost; whoami")]
    [InlineData("Host=localhost`whoami`")]
    [InlineData("Host=localhost$(whoami)")]
    [InlineData("Host=localhost|cat /etc/passwd")]
    [InlineData("Host=localhost&malicious")]
    [InlineData("Host=localhost\nmalicious")]
    public void ValidateConnectionString_WithInjectionAttempt_ShouldThrow(string maliciousConnectionString)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateConnectionString(maliciousConnectionString, "test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*dangerous*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateIdentifier_WithNullOrEmpty_ShouldThrow(string? invalidInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateIdentifier(invalidInput!, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePath_WithNullOrEmpty_ShouldThrow(string? invalidInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidatePath(invalidInput!, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDatabaseName_WithNullOrEmpty_ShouldThrow(string? invalidInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateDatabaseName(invalidInput!, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSQL_WithNullOrEmpty_ShouldThrow(string? invalidInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateSQL(invalidInput!, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateConnectionString_WithNullOrEmpty_ShouldThrow(string? invalidInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateConnectionString(invalidInput!, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("CREATE DATABASE testdb")]
    [InlineData("CREATE EXTENSION postgis")]
    [InlineData("CREATE EXTENSION IF NOT EXISTS postgis")]
    [InlineData("CREATE EXTENSION IF NOT EXISTS postgis_topology")]
    [InlineData("CREATE TABLE users (id INT)")]
    [InlineData("CREATE TABLE schema.users (id INT, name TEXT)")]
    [InlineData("CREATE INDEX idx_name ON users(name)")]
    [InlineData("ALTER TABLE users ADD COLUMN email TEXT")]
    [InlineData("ALTER TABLE users DROP COLUMN email")]
    [InlineData("ALTER TABLE users RENAME COLUMN name TO username")]
    [InlineData("DROP TABLE users")]
    [InlineData("DROP TABLE IF EXISTS users")]
    [InlineData("DROP INDEX idx_name")]
    [InlineData("DROP INDEX IF EXISTS idx_name")]
    [InlineData("SELECT PostGIS_version()")]
    [InlineData("SELECT * FROM users")]
    [InlineData("\\c testdb")]
    public void ValidateDDLStatement_WithValidDDL_ShouldNotThrow(string validDdl)
    {
        // Act & Assert - should not throw
        CommandArgumentValidator.ValidateDDLStatement(validDdl, "test");
    }

    [Theory]
    [InlineData("DELETE FROM users")]
    [InlineData("UPDATE users SET name='hacked'")]
    [InlineData("INSERT INTO users VALUES (1, 'test')")]
    [InlineData("TRUNCATE TABLE users")]
    [InlineData("GRANT ALL ON users TO public")]
    [InlineData("REVOKE ALL ON users FROM public")]
    [InlineData("DROP DATABASE production")]
    [InlineData("CREATE USER hacker")]
    [InlineData("ALTER USER postgres WITH PASSWORD 'hacked'")]
    [InlineData("CREATE FUNCTION evil() RETURNS void AS 'rm -rf /' LANGUAGE sql")]
    [InlineData("CREATE TRIGGER evil_trigger BEFORE INSERT ON users FOR EACH ROW EXECUTE PROCEDURE evil()")]
    public void ValidateDDLStatement_WithDMLOrDangerous_ShouldThrow(string dangerousSql)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateDDLStatement(dangerousSql, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("CREATE DATABASE test; DROP TABLE users--")]
    [InlineData("CREATE EXTENSION postgis; DELETE FROM sensitive_data;")]
    [InlineData("SELECT * FROM users WHERE id=1; DROP TABLE users;")]
    public void ValidateDDLStatement_WithMultiStatement_ShouldThrow(string multiStatementSql)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateDDLStatement(multiStatementSql, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDDLStatement_WithNullOrEmpty_ShouldThrow(string? invalidInput)
    {
        // Act
        var act = () => CommandArgumentValidator.ValidateDDLStatement(invalidInput!, "test");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
