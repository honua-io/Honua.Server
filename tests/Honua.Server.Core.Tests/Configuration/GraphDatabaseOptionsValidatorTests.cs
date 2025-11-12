// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration;

public class GraphDatabaseOptionsValidatorTests
{
    private readonly GraphDatabaseOptionsValidator _validator;

    public GraphDatabaseOptionsValidatorTests()
    {
        _validator = new GraphDatabaseOptionsValidator();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            CommandTimeoutSeconds = 30,
            MaxRetryAttempts = 3,
            QueryCacheExpirationMinutes = 5,
            MaxTraversalDepth = 10
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDisabledGraphDatabase_ReturnsSuccess()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = false,
            DefaultGraphName = "" // Invalid, but should be ignored when disabled
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyGraphName_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = ""
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultGraphName is required");
    }

    [Fact]
    public void Validate_WithInvalidGraphName_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "Invalid-Name" // Contains hyphen, starts with uppercase
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultGraphName").And.Contain("invalid");
    }

    [Fact]
    public void Validate_WithGraphNameTooLong_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = new string('a', 64) // Exceeds 63 character limit
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("exceeds maximum length");
    }

    [Fact]
    public void Validate_WithNegativeCommandTimeout_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            CommandTimeoutSeconds = -1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CommandTimeoutSeconds").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveCommandTimeout_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            CommandTimeoutSeconds = 3601 // Exceeds 3600 seconds
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CommandTimeoutSeconds").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithNegativeRetryAttempts_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            MaxRetryAttempts = -1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxRetryAttempts").And.Contain("negative");
    }

    [Fact]
    public void Validate_WithExcessiveRetryAttempts_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            MaxRetryAttempts = 11 // Exceeds 10
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxRetryAttempts").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidCacheExpiration_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            EnableQueryCache = true,
            QueryCacheExpirationMinutes = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("QueryCacheExpirationMinutes").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveCacheExpiration_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            EnableQueryCache = true,
            QueryCacheExpirationMinutes = 1441 // Exceeds 1440 minutes
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("QueryCacheExpirationMinutes").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidTraversalDepth_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            MaxTraversalDepth = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTraversalDepth").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveTraversalDepth_ReturnsFail()
    {
        // Arrange
        var options = new GraphDatabaseOptions
        {
            Enabled = true,
            DefaultGraphName = "honua_graph",
            MaxTraversalDepth = 101 // Exceeds 100
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTraversalDepth").And.Contain("exceeds");
    }
}
