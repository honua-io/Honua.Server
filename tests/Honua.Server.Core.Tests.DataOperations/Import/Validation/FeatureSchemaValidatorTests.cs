using System;
using System.Collections.Generic;
using Honua.Server.Core.Import.Validation;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Import.Validation;

public sealed class FeatureSchemaValidatorTests
{
    private readonly FeatureSchemaValidator _validator;

    public FeatureSchemaValidatorTests()
    {
        _validator = new FeatureSchemaValidator(NullLogger<FeatureSchemaValidator>.Instance);
    }

    [Fact]
    public void ValidateFeature_ValidFeature_ReturnsSuccess()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "text", nullable: false),
            CreateField("age", "integer", nullable: false),
            CreateField("email", "text", nullable: true)
        });

        var properties = new Dictionary<string, object?>
        {
            ["name"] = "John Doe",
            ["age"] = 30L,
            ["email"] = "john@example.com"
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateFeature_MissingRequiredField_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "text", nullable: false),
            CreateField("age", "integer", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["name"] = "John Doe"
            // Missing 'age' field
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("age", result.Errors[0].FieldName);
        Assert.Equal(ValidationErrorCodes.RequiredFieldMissing, result.Errors[0].ErrorCode);
    }

    [Fact]
    public void ValidateFeature_NullRequiredField_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "text", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["name"] = null
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("name", result.Errors[0].FieldName);
        Assert.Equal(ValidationErrorCodes.RequiredFieldMissing, result.Errors[0].ErrorCode);
    }

    [Fact]
    public void ValidateFeature_StringExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "varchar", nullable: false, maxLength: 10)
        });

        var properties = new Dictionary<string, object?>
        {
            ["name"] = "This is a very long name that exceeds the limit"
        };

        var options = new SchemaValidationOptions
        {
            TruncateLongStrings = false
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("name", result.Errors[0].FieldName);
        Assert.Equal(ValidationErrorCodes.StringTooLong, result.Errors[0].ErrorCode);
    }

    [Fact]
    public void ValidateFeature_InvalidType_WithCoercion_Succeeds()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("age", "integer", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["age"] = "30" // String instead of integer
        };

        var options = new SchemaValidationOptions
        {
            CoerceTypes = true
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateFeature_InvalidType_WithoutCoercion_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("age", "integer", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["age"] = "not-a-number"
        };

        var options = new SchemaValidationOptions
        {
            CoerceTypes = true,
            ValidationMode = SchemaValidationMode.Strict
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("age", result.Errors[0].FieldName);
    }

    [Fact]
    public void ValidateFeature_SmallIntOutOfRange_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("value", "smallint", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["value"] = 100000 // Exceeds smallint range
        };

        var options = new SchemaValidationOptions
        {
            CoerceTypes = false
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCodes.NumericOutOfRange);
    }

    [Fact]
    public void ValidateFeature_InvalidEmailFormat_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("email", "text", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["email"] = "not-an-email"
        };

        var options = new SchemaValidationOptions
        {
            ValidateCustomFormats = true
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("email", result.Errors[0].FieldName);
        Assert.Equal(ValidationErrorCodes.InvalidFormat, result.Errors[0].ErrorCode);
    }

    [Fact]
    public void ValidateFeature_ValidEmailFormat_Succeeds()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("email", "text", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            ["email"] = "user@example.com"
        };

        var options = new SchemaValidationOptions
        {
            ValidateCustomFormats = true
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateFeature_InvalidUrlFormat_ReturnsError()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("website_url", "text", nullable: true)
        });

        var properties = new Dictionary<string, object?>
        {
            ["website_url"] = "not-a-url"
        };

        var options = new SchemaValidationOptions
        {
            ValidateCustomFormats = true
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("website_url", result.Errors[0].FieldName);
        Assert.Equal(ValidationErrorCodes.InvalidFormat, result.Errors[0].ErrorCode);
    }

    [Fact]
    public void ValidateFeature_DisabledValidation_AlwaysSucceeds()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "text", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            // Missing required field
        };

        var options = new SchemaValidationOptions
        {
            ValidateSchema = false
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateFeature_NullableFieldWithNull_Succeeds()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("description", "text", nullable: true)
        });

        var properties = new Dictionary<string, object?>
        {
            ["description"] = null
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateFeature_MultipleErrors_ReturnsAll()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "text", nullable: false),
            CreateField("age", "integer", nullable: false),
            CreateField("email", "text", nullable: false)
        });

        var properties = new Dictionary<string, object?>
        {
            // Missing 'name'
            ["age"] = "not-a-number",
            ["email"] = "not-an-email"
        };

        var options = new SchemaValidationOptions
        {
            CoerceTypes = true,
            ValidationMode = SchemaValidationMode.Lenient,
            ValidateCustomFormats = true
        };

        // Act
        var result = _validator.ValidateFeature(properties, layer, options);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
    }

    [Fact]
    public void ValidateFeatures_MultipleFeatures_ReturnsResultForEach()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("name", "text", nullable: false)
        });

        var features = new[]
        {
            new Dictionary<string, object?> { ["name"] = "John" },
            new Dictionary<string, object?> { ["name"] = null }, // Invalid
            new Dictionary<string, object?> { ["name"] = "Jane" }
        };

        // Act
        var results = _validator.ValidateFeatures(features, layer);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(results[0].IsValid);
        Assert.False(results[1].IsValid);
        Assert.True(results[2].IsValid);
    }

    [Fact]
    public void ValidateFeature_BooleanField_AcceptsVariousFormats()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("active", "boolean", nullable: false)
        });

        var testCases = new object[]
        {
            true,
            false,
            "true",
            "false",
            1,
            0,
            "yes",
            "no"
        };

        var options = new SchemaValidationOptions
        {
            CoerceTypes = true
        };

        // Act & Assert
        foreach (var value in testCases)
        {
            var properties = new Dictionary<string, object?> { ["active"] = value };
            var result = _validator.ValidateFeature(properties, layer, options);
            Assert.True(result.IsValid, $"Failed for value: {value}");
        }
    }

    [Fact]
    public void ValidateFeature_DateTimeField_AcceptsVariousFormats()
    {
        // Arrange
        var layer = CreateLayer(new[]
        {
            CreateField("created_at", "datetime", nullable: false)
        });

        var testCases = new object[]
        {
            DateTime.UtcNow,
            DateTimeOffset.UtcNow,
            "2024-01-01T12:00:00Z",
            "2024-01-01"
        };

        var options = new SchemaValidationOptions
        {
            CoerceTypes = true
        };

        // Act & Assert
        foreach (var value in testCases)
        {
            var properties = new Dictionary<string, object?> { ["created_at"] = value };
            var result = _validator.ValidateFeature(properties, layer, options);
            Assert.True(result.IsValid, $"Failed for value: {value}");
        }
    }

    // Helper methods

    private static LayerDefinition CreateLayer(FieldDefinition[] fields)
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = fields
        };
    }

    private static FieldDefinition CreateField(
        string name,
        string storageType,
        bool nullable = true,
        int? maxLength = null,
        int? precision = null,
        int? scale = null)
    {
        return new FieldDefinition
        {
            Name = name,
            StorageType = storageType,
            DataType = storageType,
            Nullable = nullable,
            Editable = true,
            MaxLength = maxLength,
            Precision = precision,
            Scale = scale
        };
    }
}
