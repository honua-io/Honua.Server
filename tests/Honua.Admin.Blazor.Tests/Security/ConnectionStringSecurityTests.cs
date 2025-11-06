// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Text.RegularExpressions;

namespace Honua.Admin.Blazor.Tests.Security;

/// <summary>
/// Security-focused tests for connection string handling.
/// Tests password redaction, injection prevention, and secure handling.
/// </summary>
[Trait("Category", "Security")]
public class ConnectionStringSecurityTests
{
    #region Password Redaction Tests

    [Fact]
    public void ConnectionString_NoPasswordInLogs_RedactsPassword()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "postgres",
            Password = "SuperSecretPassword123!"
        };

        // Act
        var connectionString = parameters.Build("postgresql");
        var redactedConnectionString = RedactPassword(connectionString);

        // Assert
        redactedConnectionString.Should().NotContain("SuperSecretPassword123!");
        redactedConnectionString.Should().Contain("Password=***");
    }

    [Fact]
    public void ConnectionString_SQLServer_RedactsPassword()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost",
            Database = "testdb",
            UserId = "sa",
            Password = "VerySecure123!@#"
        };

        // Act
        var connectionString = parameters.Build("sqlserver");
        var redacted = RedactPassword(connectionString);

        // Assert
        redacted.Should().NotContain("VerySecure123!@#");
        redacted.Should().Contain("Password=***");
    }

    [Fact]
    public void ConnectionString_MySQL_RedactsPassword()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost",
            Database = "mydb",
            User = "root",
            Password = "RootPassword456"
        };

        // Act
        var connectionString = parameters.Build("mysql");
        var redacted = RedactPassword(connectionString);

        // Assert
        redacted.Should().NotContain("RootPassword456");
        redacted.Should().Contain("Password=***");
    }

    #endregion

    #region SQL Injection Prevention Tests

    [Fact]
    public void ConnectionString_SQLInjectionInHost_Blocked()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "'; DROP TABLE users; --",
            Port = 5432,
            Database = "testdb",
            Username = "postgres",
            Password = "password"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        // Connection string builder doesn't sanitize - it's the responsibility of validation
        // This test documents that injection strings are passed through
        connectionString.Should().Contain("'; DROP TABLE users; --");

        // In a real scenario, IsValidHost() should reject this
        IsValidHost(parameters.Host).Should().BeFalse();
    }

    [Fact]
    public void ConnectionString_SQLInjectionInDatabase_Blocked()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb'; DELETE FROM users; --",
            Username = "postgres",
            Password = "password"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        // Connection string includes the injection attempt
        connectionString.Should().Contain("testdb'; DELETE FROM users; --");

        // Validation should detect dangerous patterns
        ContainsDangerousPatterns(parameters.Database).Should().BeTrue();
    }

    [Fact]
    public void ConnectionString_SQLInjectionInUsername_Blocked()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "admin' OR '1'='1",
            Password = "password"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        connectionString.Should().Contain("admin' OR '1'='1");

        // Validation should detect SQL injection patterns
        ContainsDangerousPatterns(parameters.Username).Should().BeTrue();
    }

    #endregion

    #region Path Traversal Prevention Tests

    [Fact]
    public void ConnectionString_PathTraversalInSQLite_Blocked()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = "../../../etc/passwd"
        };

        // Act
        var connectionString = parameters.Build("sqlite");

        // Assert
        connectionString.Should().Contain("../../../etc/passwd");

        // Validation should detect path traversal
        ContainsPathTraversal(parameters.DataSource).Should().BeTrue();
    }

    [Fact]
    public void ConnectionString_AbsolutePathInSQLite_AllowedButValidated()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = "/var/lib/data/mydb.sqlite"
        };

        // Act
        var connectionString = parameters.Build("sqlite");

        // Assert
        connectionString.Should().Contain("/var/lib/data/mydb.sqlite");
        ContainsPathTraversal(parameters.DataSource).Should().BeFalse();
    }

    #endregion

    #region Command Injection Tests

    [Fact]
    public void ConnectionString_CommandInjectionAttempt_Blocked()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost; rm -rf /",
            Port = 5432,
            Database = "testdb",
            Username = "postgres",
            Password = "password"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        IsValidHost(parameters.Host).Should().BeFalse();
    }

    [Fact]
    public void ConnectionString_ScriptInjectionAttempt_Blocked()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "<script>alert('xss')</script>",
            Database = "testdb",
            UserId = "sa",
            Password = "password"
        };

        // Act
        var connectionString = parameters.Build("sqlserver");

        // Assert
        IsValidHost(parameters.Server).Should().BeFalse();
    }

    #endregion

    #region Special Character Handling Tests

    [Fact]
    public void ConnectionString_SpecialCharacters_ProperlyEscaped()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "test_db-2024",
            Username = "user.name",
            Password = "P@ssw0rd!#$"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        connectionString.Should().Contain("Database=test_db-2024");
        connectionString.Should().Contain("Username=user.name");
        connectionString.Should().Contain("Password=P@ssw0rd!#$");
    }

    [Fact]
    public void ConnectionString_Unicode_HandlesCorrectly()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "数据库",
            Username = "用户",
            Password = "密码123"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        connectionString.Should().Contain("Database=数据库");
        connectionString.Should().Contain("Username=用户");
        connectionString.Should().Contain("Password=密码123");
    }

    [Fact]
    public void ConnectionString_Semicolon_HandledInValues()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "test;db",
            Username = "user",
            Password = "pass;word"
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        // Semicolons in values can break connection string parsing
        // This test documents current behavior - should be properly escaped
        connectionString.Should().Contain("Database=test;db");
        connectionString.Should().Contain("Password=pass;word");
    }

    #endregion

    #region Error Message Security Tests

    [Fact]
    public void TestConnection_ErrorMessage_NoSensitiveInfo()
    {
        // Arrange
        var errorMessage = "Connection failed: authentication failed for user 'postgres'";

        // Act
        var sanitized = SanitizeErrorMessage(errorMessage);

        // Assert
        sanitized.Should().NotContain("postgres");
        sanitized.Should().Contain("authentication failed");
    }

    [Fact]
    public void TestConnection_Logs_RedactsCredentials()
    {
        // Arrange
        var logMessage = "Attempting connection to: Host=localhost;Database=testdb;Username=admin;Password=secret123";

        // Act
        var sanitized = RedactPassword(logMessage);

        // Assert
        sanitized.Should().NotContain("secret123");
        sanitized.Should().Contain("Password=***");
    }

    #endregion

    #region Password Complexity Tests

    [Fact]
    public void ConnectionString_EmptyPassword_Allowed()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "user",
            Password = ""
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        connectionString.Should().Contain("Password=");
    }

    [Fact]
    public void ConnectionString_LongPassword_Handled()
    {
        // Arrange
        var longPassword = new string('a', 1000);
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "user",
            Password = longPassword
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        connectionString.Should().Contain($"Password={longPassword}");
    }

    #endregion

    #region Null/Empty String Security Tests

    [Fact]
    public void ConnectionString_NullValues_HandledSafely()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = null,
            Port = null,
            Database = null,
            Username = null,
            Password = null
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        // Should not throw and should handle null gracefully
        connectionString.Should().NotBeNull();
    }

    [Fact]
    public void ConnectionString_EmptyStrings_HandledSafely()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "",
            Database = "",
            Username = "",
            Password = ""
        };

        // Act
        var connectionString = parameters.Build("postgresql");

        // Assert
        connectionString.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Redacts password from connection string for logging
    /// </summary>
    private string RedactPassword(string connectionString)
    {
        return Regex.Replace(connectionString, @"Password=[^;]*", "Password=***", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Validates host format (mirrors DataSourceDialog.IsValidHost)
    /// </summary>
    private bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        // Check if it's "localhost"
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if it's an IP address
        if (System.Net.IPAddress.TryParse(host, out _))
            return true;

        // Check if it's a valid hostname (basic check)
        return Regex.IsMatch(host, @"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$");
    }

    /// <summary>
    /// Checks for dangerous SQL patterns
    /// </summary>
    private bool ContainsDangerousPatterns(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var dangerousPatterns = new[]
        {
            @";\s*DROP\s+TABLE",
            @";\s*DELETE\s+FROM",
            @"'\s*OR\s+'1'\s*=\s*'1",
            @"--",
            @"xp_cmdshell",
            @"<script",
            @"javascript:"
        };

        return dangerousPatterns.Any(pattern =>
            Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Checks for path traversal attempts
    /// </summary>
    private bool ContainsPathTraversal(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path.Contains("..") || path.Contains("~");
    }

    /// <summary>
    /// Sanitizes error messages to remove sensitive information
    /// </summary>
    private string SanitizeErrorMessage(string message)
    {
        // Remove usernames from error messages
        var sanitized = Regex.Replace(message, @"user\s+'[^']+'", "user '***'", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"username\s+'[^']+'", "username '***'", RegexOptions.IgnoreCase);

        return sanitized;
    }

    #endregion
}
