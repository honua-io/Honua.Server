// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Tests.Shared.Models;

/// <summary>
/// Tests for ConnectionStringParameters connection string building logic.
/// Verifies correct connection string generation for all supported providers.
/// </summary>
[Trait("Category", "Unit")]
public class ConnectionStringParametersTests
{
    #region PostgreSQL Tests

    [Fact]
    public void Build_PostgreSQL_GeneratesCorrectFormat()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "testuser",
            Password = "testpass",
            SslMode = "Prefer"
        };

        // Act
        var result = parameters.Build("postgresql");

        // Assert
        result.Should().Be("Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=testpass;SSL Mode=Prefer");
    }

    [Fact]
    public void Build_PostgreSQL_WithSSLMode_IncludesSSLParameter()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "db.example.com",
            Port = 5432,
            Database = "mydb",
            Username = "admin",
            Password = "secret",
            SslMode = "Require"
        };

        // Act
        var result = parameters.Build("postgis");

        // Assert
        result.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void Build_PostgreSQL_WithPort_IncludesPort()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5433,
            Database = "testdb",
            Username = "user",
            Password = "pass"
        };

        // Act
        var result = parameters.Build("postgresql");

        // Assert
        result.Should().Contain("Port=5433");
    }

    [Fact]
    public void Build_PostgreSQL_DefaultPort_Uses5432()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "user",
            Password = "pass"
        };

        // Act
        var result = parameters.Build("postgis");

        // Assert
        result.Should().Contain("Port=5432");
    }

    [Fact]
    public void Build_PostgreSQL_EscapesSpecialCharacters()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Port = 5432,
            Database = "test;db",
            Username = "test=user",
            Password = "pass;word"
        };

        // Act
        var result = parameters.Build("postgresql");

        // Assert
        result.Should().Contain("Database=test;db");
        result.Should().Contain("Username=test=user");
        result.Should().Contain("Password=pass;word");
    }

    #endregion

    #region SQL Server Tests

    [Fact]
    public void Build_SQLServer_GeneratesCorrectFormat()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost",
            Database = "testdb",
            UserId = "sa",
            Password = "SecurePass123",
            Encrypt = true,
            TrustServerCertificate = false
        };

        // Act
        var result = parameters.Build("sqlserver");

        // Assert
        result.Should().Be("Server=localhost;Database=testdb;User Id=sa;Password=SecurePass123;Encrypt=True;TrustServerCertificate=False");
    }

    [Fact]
    public void Build_SQLServer_WithEncryption_IncludesEncryptParameter()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "sql.example.com",
            Database = "proddb",
            UserId = "appuser",
            Password = "secret",
            Encrypt = true
        };

        // Act
        var result = parameters.Build("sqlserver");

        // Assert
        result.Should().Contain("Encrypt=True");
    }

    [Fact]
    public void Build_SQLServer_WithTrustCertificate_IncludesParameter()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost",
            Database = "devdb",
            UserId = "dev",
            Password = "devpass",
            TrustServerCertificate = true
        };

        // Act
        var result = parameters.Build("sqlserver");

        // Assert
        result.Should().Contain("TrustServerCertificate=True");
    }

    [Fact]
    public void Build_SQLServer_IntegratedAuth_UsesCorrectFormat()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost\\SQLEXPRESS",
            Database = "testdb",
            UserId = "domain\\user",
            Password = "password"
        };

        // Act
        var result = parameters.Build("sqlserver");

        // Assert
        result.Should().Contain("Server=localhost\\SQLEXPRESS");
        result.Should().Contain("User Id=domain\\user");
    }

    #endregion

    #region MySQL Tests

    [Fact]
    public void Build_MySQL_GeneratesCorrectFormat()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost",
            Port = 3306,
            Database = "mydb",
            User = "root",
            Password = "rootpass"
        };

        // Act
        var result = parameters.Build("mysql");

        // Assert
        result.Should().Be("Server=localhost;Port=3306;Database=mydb;User=root;Password=rootpass");
    }

    [Fact]
    public void Build_MySQL_WithPort_IncludesPort()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "mysql.example.com",
            Port = 3307,
            Database = "appdb",
            User = "appuser",
            Password = "secret"
        };

        // Act
        var result = parameters.Build("mysql");

        // Assert
        result.Should().Contain("Port=3307");
    }

    [Fact]
    public void Build_MySQL_DefaultPort_Uses3306()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Server = "localhost",
            Port = 3306,
            Database = "testdb",
            User = "test",
            Password = "test"
        };

        // Act
        var result = parameters.Build("mysql");

        // Assert
        result.Should().Contain("Port=3306");
    }

    #endregion

    #region SQLite Tests

    [Fact]
    public void Build_SQLite_GeneratesCorrectFormat()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = "/data/mydb.sqlite"
        };

        // Act
        var result = parameters.Build("sqlite");

        // Assert
        result.Should().Be("Data Source=/data/mydb.sqlite");
    }

    [Fact]
    public void Build_SQLite_WithRelativePath_HandlesCorrectly()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = "data/local.db"
        };

        // Act
        var result = parameters.Build("sqlite");

        // Assert
        result.Should().Be("Data Source=data/local.db");
    }

    [Fact]
    public void Build_SQLite_WithAbsolutePath_HandlesCorrectly()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = "C:\\Data\\mydb.db"
        };

        // Act
        var result = parameters.Build("sqlite");

        // Assert
        result.Should().Be("Data Source=C:\\Data\\mydb.db");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Build_UnknownProvider_ReturnsEmpty()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Database = "testdb"
        };

        // Act
        var result = parameters.Build("unknownprovider");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_NullProvider_ReturnsEmpty()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = "localhost",
            Database = "testdb"
        };

        // Act
        var result = parameters.Build(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_PostgreSQL_WithNullHost_OmitsHost()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            Host = null,
            Port = 5432,
            Database = "testdb",
            Username = "user",
            Password = "pass"
        };

        // Act
        var result = parameters.Build("postgresql");

        // Assert
        result.Should().NotContain("Host=");
        result.Should().Contain("Port=5432");
    }

    [Fact]
    public void Build_PostgreSQL_WithEmptyPassword_IncludesEmptyPassword()
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
        var result = parameters.Build("postgresql");

        // Assert
        result.Should().Contain("Password=");
    }

    [Fact]
    public void Build_SQLite_WithEmptyDataSource_ReturnsEmpty()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = ""
        };

        // Act
        var result = parameters.Build("sqlite");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_SQLite_WithNullDataSource_ReturnsEmpty()
    {
        // Arrange
        var parameters = new ConnectionStringParameters
        {
            DataSource = null
        };

        // Act
        var result = parameters.Build("sqlite");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion
}
