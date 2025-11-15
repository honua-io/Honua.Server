// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration;

public class DataIngestionOptionsValidatorTests
{
    private readonly DataIngestionOptionsValidator _validator;

    public DataIngestionOptionsValidatorTests()
    {
        _validator = new DataIngestionOptionsValidator();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchSize = 1000,
            ProgressReportInterval = 100,
            MaxRetries = 3,
            BatchTimeout = TimeSpan.FromMinutes(5),
            TransactionTimeout = TimeSpan.FromMinutes(30),
            UseTransactionalIngestion = true,
            TransactionIsolationLevel = IsolationLevel.RepeatableRead,
            MaxGeometryCoordinates = 1_000_000,
            MaxValidationErrors = 100
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidBatchSize_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchSize = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("BatchSize").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveBatchSize_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchSize = 100001
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("BatchSize").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithProgressIntervalGreaterThanBatchSize_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchSize = 1000,
            ProgressReportInterval = 1001
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ProgressReportInterval").And.Contain("BatchSize");
    }

    [Fact]
    public void Validate_WithNegativeMaxRetries_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            MaxRetries = -1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxRetries").And.Contain("negative");
    }

    [Fact]
    public void Validate_WithNegativeBatchTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchTimeout = TimeSpan.FromSeconds(-1)
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("BatchTimeout").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithTransactionTimeoutLessThanBatchTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchTimeout = TimeSpan.FromMinutes(10),
            TransactionTimeout = TimeSpan.FromMinutes(5),
            UseTransactionalIngestion = true
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TransactionTimeout").And.Contain("BatchTimeout");
    }

    [Fact]
    public void Validate_WithInvalidMaxGeometryCoordinates_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            MaxGeometryCoordinates = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxGeometryCoordinates").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveMaxGeometryCoordinates_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            MaxGeometryCoordinates = 10_000_001
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxGeometryCoordinates").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidMaxValidationErrors_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            MaxValidationErrors = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxValidationErrors").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithRejectInvalidGeometriesButNoValidation_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            ValidateGeometry = false,
            RejectInvalidGeometries = true
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RejectInvalidGeometries").And.Contain("ValidateGeometry");
    }

    [Fact]
    public void Validate_WithAutoRepairButNoValidation_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            ValidateGeometry = false,
            AutoRepairGeometries = true
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("AutoRepairGeometries").And.Contain("ValidateGeometry");
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    public void Validate_WithValidIsolationLevels_ReturnsSuccess(IsolationLevel isolationLevel)
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            TransactionIsolationLevel = isolationLevel
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullOptions_ReturnsFail()
    {
        // Act
        var result = _validator.Validate(null, null!);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("cannot be null");
    }

    [Fact]
    public void Validate_WithExcessiveMaxRetries_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            MaxRetries = 11 // Exceeds 10
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxRetries").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithExcessiveBatchTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            BatchTimeout = TimeSpan.FromHours(2) // Exceeds 1 hour
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("BatchTimeout").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithNegativeTransactionTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            TransactionTimeout = TimeSpan.FromSeconds(-1),
            UseTransactionalIngestion = true
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TransactionTimeout").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveTransactionTimeout_ReturnsFail()
    {
        // Arrange
        var options = new DataIngestionOptions
        {
            TransactionTimeout = TimeSpan.FromHours(3), // Exceeds 2 hours
            UseTransactionalIngestion = true
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TransactionTimeout").And.Contain("exceeds");
    }
}
