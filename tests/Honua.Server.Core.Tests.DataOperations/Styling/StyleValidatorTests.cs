using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Styling;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Styling;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class StyleValidatorTests
{
    [Fact]
    public void ValidateStyleDefinition_WithValidSimpleStyle_ReturnsValid()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Title = "Test Style",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#4A90E2",
                StrokeColor = "#1F364D",
                StrokeWidth = 1.5
            }
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateStyleDefinition_WithMissingId_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition()
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ID is required"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithMissingFormat_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition()
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("format is required"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithInvalidFormat_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "invalid-format",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition()
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unsupported style format"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithMissingGeometryType_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "legacy",
            GeometryType = "",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition()
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Geometry type is required"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithSimpleRendererAndMissingSimpleConfig_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = null
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Simple renderer requires"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithInvalidColor_ReturnsWarning()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "not-a-color",
                StrokeColor = "#1F364D"
            }
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.True(result.IsValid); // Valid but with warnings
        Assert.Contains(result.Warnings, w => w.Contains("Fill color"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithInvalidOpacity_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#4A90E2",
                Opacity = 1.5 // Invalid: must be 0-1
            }
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Opacity"));
    }

    [Fact]
    public void ValidateStyleDefinition_WithUniqueValueRenderer_ValidatesCorrectly()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "uniqueValue",
            UniqueValue = new UniqueValueStyleDefinition
            {
                Field = "category",
                Classes = new List<UniqueValueStyleClassDefinition>
                {
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "residential",
                        Symbol = new SimpleStyleDefinition { FillColor = "#FF0000" }
                    },
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "commercial",
                        Symbol = new SimpleStyleDefinition { FillColor = "#00FF00" }
                    }
                }
            }
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateStyleDefinition_WithUniqueValueAndMissingField_ReturnsError()
    {
        // Arrange
        var style = new StyleDefinition
        {
            Id = "test-style",
            Format = "legacy",
            GeometryType = "polygon",
            Renderer = "uniqueValue",
            UniqueValue = new UniqueValueStyleDefinition
            {
                Field = "",
                Classes = new List<UniqueValueStyleClassDefinition>
                {
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "test",
                        Symbol = new SimpleStyleDefinition()
                    }
                }
            }
        };

        // Act
        var result = StyleValidator.ValidateStyleDefinition(style);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must specify a field"));
    }

    [Fact]
    public void ValidateSldXml_WithValidSld_ReturnsValid()
    {
        // Arrange
        var sld = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<StyledLayerDescriptor version=""1.0.0""
    xmlns=""http://www.opengis.net/sld""
    xmlns:ogc=""http://www.opengis.net/ogc"">
  <NamedLayer>
    <Name>TestLayer</Name>
    <UserStyle>
      <Name>TestStyle</Name>
      <FeatureTypeStyle>
        <Rule>
          <Name>DefaultRule</Name>
          <PolygonSymbolizer>
            <Fill>
              <CssParameter name=""fill"">#4A90E2</CssParameter>
            </Fill>
          </PolygonSymbolizer>
        </Rule>
      </FeatureTypeStyle>
    </UserStyle>
  </NamedLayer>
</StyledLayerDescriptor>";

        // Act
        var result = StyleValidator.ValidateSldXml(sld);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateSldXml_WithEmptyString_ReturnsError()
    {
        // Act
        var result = StyleValidator.ValidateSldXml("");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void ValidateSldXml_WithInvalidXml_ReturnsError()
    {
        // Arrange
        var invalidXml = "<StyledLayerDescriptor><NotClosed>";

        // Act
        var result = StyleValidator.ValidateSldXml(invalidXml);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Failed to parse"));
    }

    [Fact]
    public void ValidateMapboxStyle_WithValidStyle_ReturnsValid()
    {
        // Arrange
        var mapboxStyle = @"{
            ""version"": 8,
            ""name"": ""Test Style"",
            ""sources"": {
                ""test-source"": {
                    ""type"": ""vector"",
                    ""url"": ""mapbox://test.source""
                }
            },
            ""layers"": [
                {
                    ""id"": ""test-layer"",
                    ""type"": ""fill"",
                    ""source"": ""test-source"",
                    ""paint"": {
                        ""fill-color"": ""#4A90E2""
                    }
                }
            ]
        }";

        // Act
        var result = StyleValidator.ValidateMapboxStyle(mapboxStyle);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateMapboxStyle_WithMissingVersion_ReturnsError()
    {
        // Arrange
        var mapboxStyle = @"{
            ""name"": ""Test Style"",
            ""layers"": []
        }";

        // Act
        var result = StyleValidator.ValidateMapboxStyle(mapboxStyle);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("version"));
    }

    [Fact]
    public void ValidateMapboxStyle_WithMissingLayers_ReturnsError()
    {
        // Arrange
        var mapboxStyle = @"{
            ""version"": 8,
            ""name"": ""Test Style""
        }";

        // Act
        var result = StyleValidator.ValidateMapboxStyle(mapboxStyle);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("layers"));
    }

    [Fact]
    public void ValidateCartoCSS_WithValidCSS_ReturnsValid()
    {
        // Arrange
        var cartoCSS = @"
#layer {
  polygon-fill: #4A90E2;
  polygon-opacity: 0.8;
  line-color: #1F364D;
  line-width: 1.5;
}";

        // Act
        var result = StyleValidator.ValidateCartoCSS(cartoCSS);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateCartoCSS_WithEmptyString_ReturnsError()
    {
        // Act
        var result = StyleValidator.ValidateCartoCSS("");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void ValidateCartoCSS_WithNoBraces_ReturnsError()
    {
        // Arrange
        var cartoCSS = "polygon-fill: #4A90E2;";

        // Act
        var result = StyleValidator.ValidateCartoCSS(cartoCSS);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("rule block"));
    }

    [Fact]
    public void ValidationResult_GetSummary_WithNoIssues_ReturnsCorrectMessage()
    {
        // Arrange
        var result = new ValidationResult(Array.Empty<string>(), Array.Empty<string>());

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("valid", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no issues", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationResult_GetSummary_WithErrorsAndWarnings_ReturnsCorrectMessage()
    {
        // Arrange
        var result = new ValidationResult(
            new[] { "Error 1", "Error 2" },
            new[] { "Warning 1" });

        // Act
        var summary = result.GetSummary();

        // Assert
        Assert.Contains("2 error(s)", summary);
        Assert.Contains("1 warning(s)", summary);
    }
}
