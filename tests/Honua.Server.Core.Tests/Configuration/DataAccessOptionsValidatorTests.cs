// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Data;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration;

public class DataAccessOptionsValidatorTests
{
    private readonly DataAccessOptionsValidator _validator;

    public DataAccessOptionsValidatorTests()
    {
        _validator = new DataAccessOptionsValidator();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            DefaultCommandTimeoutSeconds = 30,
            LongRunningQueryTimeoutSeconds = 300,
            BulkOperationTimeoutSeconds = 600,
            TransactionTimeoutSeconds = 120,
            HealthCheckTimeoutSeconds = 5
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidDefaultCommandTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            DefaultCommandTimeoutSeconds = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultCommandTimeoutSeconds").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveDefaultCommandTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            DefaultCommandTimeoutSeconds = 301
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultCommandTimeoutSeconds").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithLongRunningTimeoutLessThanDefault_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            DefaultCommandTimeoutSeconds = 100,
            LongRunningQueryTimeoutSeconds = 50
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("LongRunningQueryTimeoutSeconds").And.Contain("greater than");
    }

    [Fact]
    public void Validate_WithBulkTimeoutLessThanLongRunning_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            LongRunningQueryTimeoutSeconds = 300,
            BulkOperationTimeoutSeconds = 200
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("BulkOperationTimeoutSeconds").And.Contain("greater than");
    }

    [Fact]
    public void Validate_WithExcessiveHealthCheckTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            HealthCheckTimeoutSeconds = 31
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("HealthCheckTimeoutSeconds").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidSqlServerPoolOptions_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            SqlServer = new SqlServerPoolOptions
            {
                MinPoolSize = 10,
                MaxPoolSize = 5 // Less than MinPoolSize
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SqlServer").And.Contain("MaxPoolSize");
    }

    [Fact]
    public void Validate_WithInvalidPostgresPoolOptions_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            Postgres = new PostgresPoolOptions
            {
                MinPoolSize = 10,
                MaxPoolSize = 5 // Less than MinPoolSize
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Postgres").And.Contain("MaxPoolSize");
    }

    [Fact]
    public void Validate_WithInvalidMySqlPoolOptions_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            MySql = new MySqlPoolOptions
            {
                MinimumPoolSize = 10,
                MaximumPoolSize = 5 // Less than MinimumPoolSize
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MySql").And.Contain("MaximumPoolSize");
    }

    [Fact]
    public void Validate_WithInvalidSqliteCacheMode_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            Sqlite = new SqlitePoolOptions
            {
                CacheMode = "Invalid"
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Sqlite").And.Contain("CacheMode");
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Private")]
    [InlineData("Shared")]
    public void Validate_WithValidSqliteCacheModes_ReturnsSuccess(string cacheMode)
    {
        // Arrange
        var options = new DataAccessOptions
        {
            Sqlite = new SqlitePoolOptions
            {
                CacheMode = cacheMode
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyVersionColumnName_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            OptimisticLocking = new OptimisticLockingOptions
            {
                VersionColumnName = ""
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("VersionColumnName").And.Contain("required");
    }

    [Fact]
    public void Validate_WithNegativeOptimisticLockingRetries_ReturnsFail()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            OptimisticLocking = new OptimisticLockingOptions
            {
                MaxRetryAttempts = -1
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("OptimisticLocking").And.Contain("MaxRetryAttempts");
    }

    [Fact]
    public void Validate_WithValidPoolOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new DataAccessOptions
        {
            SqlServer = new SqlServerPoolOptions
            {
                MinPoolSize = 2,
                MaxPoolSize = 50,
                ConnectionLifetime = 600,
                ConnectTimeout = 15
            },
            Postgres = new PostgresPoolOptions
            {
                MinPoolSize = 2,
                MaxPoolSize = 50,
                ConnectionLifetime = 600,
                Timeout = 15
            },
            MySql = new MySqlPoolOptions
            {
                MinimumPoolSize = 2,
                MaximumPoolSize = 50,
                ConnectionLifeTime = 600,
                ConnectionTimeout = 15
            }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
