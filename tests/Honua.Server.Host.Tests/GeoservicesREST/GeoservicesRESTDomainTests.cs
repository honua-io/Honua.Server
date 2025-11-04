using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Unit tests for domain support in GeoservicesRESTMetadataMapper.
/// Tests cover coded value domains and range domains as per ArcGIS REST API specification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Speed", "Fast")]
public sealed class GeoservicesRESTDomainTests
{
    #region Test Infrastructure

    private static LayerDefinition CreateTestLayerWithDomain(FieldDomainDefinition? domain)
    {
        var fieldWithDomain = new FieldDefinition
        {
            Name = "status",
            Alias = "Status",
            DataType = "string",
            Nullable = false,
            Editable = true,
            Domain = domain
        };

        return new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Polygon",
            IdField = "objectid",
            GeometryField = "shape",
            Fields = new ReadOnlyCollection<FieldDefinition>(new[] { fieldWithDomain })
        };
    }

    #endregion

    #region Coded Value Domain Tests

    [Fact]
    public void CreateFieldDefinitions_CodedValueDomain_MapsCorrectly()
    {
        // Arrange - Create a layer with a coded value domain
        var codedValues = new ReadOnlyCollection<CodedValueDefinition>(new[]
        {
            new CodedValueDefinition { Name = "Active", Code = 1 },
            new CodedValueDefinition { Name = "Inactive", Code = 2 },
            new CodedValueDefinition { Name = "Pending", Code = 3 }
        });

        var domain = new FieldDomainDefinition
        {
            Type = "codedValue",
            Name = "StatusDomain",
            CodedValues = codedValues,
            Range = null
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        fields.Should().HaveCount(1);
        var field = fields[0];
        field.Domain.Should().NotBeNull();
        field.Domain!.Type.Should().Be("codedValue");
        field.Domain.Name.Should().Be("StatusDomain");
        field.Domain.CodedValues.Should().NotBeNull();
        field.Domain.CodedValues.Should().HaveCount(3);
        field.Domain.Range.Should().BeNull();

        // Verify coded values
        field.Domain.CodedValues![0].Name.Should().Be("Active");
        field.Domain.CodedValues[0].Code.Should().Be(1);
        field.Domain.CodedValues[1].Name.Should().Be("Inactive");
        field.Domain.CodedValues[1].Code.Should().Be(2);
        field.Domain.CodedValues[2].Name.Should().Be("Pending");
        field.Domain.CodedValues[2].Code.Should().Be(3);
    }

    [Fact]
    public void CreateFieldDefinitions_CodedValueDomainWithStrings_MapsCorrectly()
    {
        // Arrange - Test string codes
        var codedValues = new ReadOnlyCollection<CodedValueDefinition>(new[]
        {
            new CodedValueDefinition { Name = "North", Code = "N" },
            new CodedValueDefinition { Name = "South", Code = "S" },
            new CodedValueDefinition { Name = "East", Code = "E" },
            new CodedValueDefinition { Name = "West", Code = "W" }
        });

        var domain = new FieldDomainDefinition
        {
            Type = "codedValue",
            Name = "DirectionDomain",
            CodedValues = codedValues,
            Range = null
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().NotBeNull();
        field.Domain!.CodedValues.Should().HaveCount(4);
        field.Domain.CodedValues![0].Code.Should().Be("N");
        field.Domain.CodedValues[1].Code.Should().Be("S");
        field.Domain.CodedValues[2].Code.Should().Be("E");
        field.Domain.CodedValues[3].Code.Should().Be("W");
    }

    [Fact]
    public void CreateFieldDefinitions_CodedValueDomainCaseInsensitive_MapsCorrectly()
    {
        // Arrange - Test case insensitivity of domain type
        var codedValues = new ReadOnlyCollection<CodedValueDefinition>(new[]
        {
            new CodedValueDefinition { Name = "Value1", Code = 1 }
        });

        var domain = new FieldDomainDefinition
        {
            Type = "CODEDVALUE", // uppercase
            Name = "TestDomain",
            CodedValues = codedValues,
            Range = null
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().NotBeNull();
        field.Domain!.Type.Should().Be("codedValue");
    }

    #endregion

    #region Range Domain Tests

    [Fact]
    public void CreateFieldDefinitions_RangeDomain_MapsCorrectly()
    {
        // Arrange - Create a layer with a range domain
        var range = new RangeDomainDefinition
        {
            MinValue = 0,
            MaxValue = 100
        };

        var domain = new FieldDomainDefinition
        {
            Type = "range",
            Name = "PercentageDomain",
            CodedValues = null,
            Range = range
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        fields.Should().HaveCount(1);
        var field = fields[0];
        field.Domain.Should().NotBeNull();
        field.Domain!.Type.Should().Be("range");
        field.Domain.Name.Should().Be("PercentageDomain");
        field.Domain.Range.Should().NotBeNull();
        field.Domain.Range.Should().HaveCount(2);
        field.Domain.Range![0].Should().Be(0);
        field.Domain.Range[1].Should().Be(100);
        field.Domain.CodedValues.Should().BeNull();
    }

    [Fact]
    public void CreateFieldDefinitions_RangeDomainNegative_MapsCorrectly()
    {
        // Arrange - Test negative ranges
        var range = new RangeDomainDefinition
        {
            MinValue = -273.15,
            MaxValue = 1000.0
        };

        var domain = new FieldDomainDefinition
        {
            Type = "range",
            Name = "TemperatureDomain",
            CodedValues = null,
            Range = range
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().NotBeNull();
        field.Domain!.Range.Should().NotBeNull();
        field.Domain.Range![0].Should().Be(-273.15);
        field.Domain.Range[1].Should().Be(1000.0);
    }

    [Fact]
    public void CreateFieldDefinitions_RangeDomainCaseInsensitive_MapsCorrectly()
    {
        // Arrange - Test case insensitivity
        var range = new RangeDomainDefinition
        {
            MinValue = 0,
            MaxValue = 10
        };

        var domain = new FieldDomainDefinition
        {
            Type = "RANGE", // uppercase
            Name = "TestRangeDomain",
            CodedValues = null,
            Range = range
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().NotBeNull();
        field.Domain!.Type.Should().Be("range");
    }

    #endregion

    #region No Domain Tests

    [Fact]
    public void CreateFieldDefinitions_NoDomain_ReturnsNull()
    {
        // Arrange - Field without domain
        var layer = CreateTestLayerWithDomain(null);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        fields.Should().HaveCount(1);
        var field = fields[0];
        field.Domain.Should().BeNull();
    }

    [Fact]
    public void CreateFieldDefinitions_UnknownDomainType_ReturnsNull()
    {
        // Arrange - Unknown domain type should be safely ignored
        var domain = new FieldDomainDefinition
        {
            Type = "unknownType",
            Name = "UnknownDomain",
            CodedValues = null,
            Range = null
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().BeNull();
    }

    [Fact]
    public void CreateFieldDefinitions_CodedValueDomainWithoutValues_ReturnsNull()
    {
        // Arrange - Coded value domain without actual values
        var domain = new FieldDomainDefinition
        {
            Type = "codedValue",
            Name = "EmptyDomain",
            CodedValues = null,
            Range = null
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().BeNull();
    }

    [Fact]
    public void CreateFieldDefinitions_RangeDomainWithoutRange_ReturnsNull()
    {
        // Arrange - Range domain without actual range
        var domain = new FieldDomainDefinition
        {
            Type = "range",
            Name = "EmptyRangeDomain",
            CodedValues = null,
            Range = null
        };

        var layer = CreateTestLayerWithDomain(domain);

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        var field = fields[0];
        field.Domain.Should().BeNull();
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void CreateFieldDefinitions_MultipleFieldsWithMixedDomains_MapsCorrectly()
    {
        // Arrange - Multiple fields with different domain types
        var statusField = new FieldDefinition
        {
            Name = "status",
            Alias = "Status",
            DataType = "integer",
            Nullable = false,
            Editable = true,
            Domain = new FieldDomainDefinition
            {
                Type = "codedValue",
                Name = "StatusDomain",
                CodedValues = new ReadOnlyCollection<CodedValueDefinition>(new[]
                {
                    new CodedValueDefinition { Name = "Active", Code = 1 },
                    new CodedValueDefinition { Name = "Inactive", Code = 2 }
                }),
                Range = null
            }
        };

        var scoreField = new FieldDefinition
        {
            Name = "score",
            Alias = "Score",
            DataType = "double",
            Nullable = true,
            Editable = true,
            Domain = new FieldDomainDefinition
            {
                Type = "range",
                Name = "ScoreDomain",
                CodedValues = null,
                Range = new RangeDomainDefinition { MinValue = 0.0, MaxValue = 100.0 }
            }
        };

        var nameField = new FieldDefinition
        {
            Name = "name",
            Alias = "Name",
            DataType = "string",
            Nullable = false,
            Editable = true,
            Domain = null
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Polygon",
            IdField = "objectid",
            GeometryField = "shape",
            Fields = new ReadOnlyCollection<FieldDefinition>(new[] { statusField, scoreField, nameField })
        };

        // Act
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);

        // Assert
        fields.Should().HaveCount(3);

        // Verify status field (coded value domain)
        fields[0].Name.Should().Be("status");
        fields[0].Domain.Should().NotBeNull();
        fields[0].Domain!.Type.Should().Be("codedValue");
        fields[0].Domain.CodedValues.Should().HaveCount(2);

        // Verify score field (range domain)
        fields[1].Name.Should().Be("score");
        fields[1].Domain.Should().NotBeNull();
        fields[1].Domain!.Type.Should().Be("range");
        fields[1].Domain.Range.Should().NotBeNull();

        // Verify name field (no domain)
        fields[2].Name.Should().Be("name");
        fields[2].Domain.Should().BeNull();
    }

    #endregion
}
