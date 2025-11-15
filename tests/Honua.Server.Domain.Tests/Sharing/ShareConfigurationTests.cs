// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain.Sharing;
using Xunit;

namespace Honua.Server.Domain.Tests.Sharing;

[Trait("Category", "Unit")]
public sealed class ShareConfigurationTests
{
    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_ShouldCreateConfigurationWithDefaultValues()
    {
        // Arrange & Act
        var config = ShareConfiguration.CreateDefault();

        // Assert
        config.Should().NotBeNull();
        config.Width.Should().Be("100%");
        config.Height.Should().Be("600px");
        config.ShowZoomControls.Should().BeTrue();
        config.ShowLayerSwitcher.Should().BeTrue();
        config.ShowSearch.Should().BeFalse();
        config.ShowScaleBar.Should().BeTrue();
        config.ShowAttribution.Should().BeTrue();
        config.AllowFullscreen.Should().BeTrue();
        config.CustomCss.Should().BeNull();
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldCreateConfiguration()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create(
            width: "800px",
            height: "600px",
            showZoomControls: false,
            showLayerSwitcher: false,
            showSearch: true,
            showScaleBar: false,
            showAttribution: false,
            allowFullscreen: false,
            customCss: "body { background: blue; }");

        // Assert
        config.Should().NotBeNull();
        config.Width.Should().Be("800px");
        config.Height.Should().Be("600px");
        config.ShowZoomControls.Should().BeFalse();
        config.ShowLayerSwitcher.Should().BeFalse();
        config.ShowSearch.Should().BeTrue();
        config.ShowScaleBar.Should().BeFalse();
        config.ShowAttribution.Should().BeFalse();
        config.AllowFullscreen.Should().BeFalse();
        config.CustomCss.Should().Be("body { background: blue; }");
    }

    [Fact]
    public void Create_WithPixelDimensions_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("800px", "600px");

        // Assert
        config.Width.Should().Be("800px");
        config.Height.Should().Be("600px");
    }

    [Fact]
    public void Create_WithPercentageDimensions_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("100%", "80%");

        // Assert
        config.Width.Should().Be("100%");
        config.Height.Should().Be("80%");
    }

    [Fact]
    public void Create_WithViewportDimensions_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("100vw", "100vh");

        // Assert
        config.Width.Should().Be("100vw");
        config.Height.Should().Be("100vh");
    }

    [Fact]
    public void Create_WithEmDimensions_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("50em", "30em");

        // Assert
        config.Width.Should().Be("50em");
        config.Height.Should().Be("30em");
    }

    [Fact]
    public void Create_WithRemDimensions_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("40rem", "25rem");

        // Assert
        config.Width.Should().Be("40rem");
        config.Height.Should().Be("25rem");
    }

    [Fact]
    public void Create_WithEmptyWidth_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("", "600px");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Width cannot be empty*")
            .And.ParamName.Should().Be("width");
    }

    [Fact]
    public void Create_WithNullWidth_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create(null!, "600px");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Width cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceWidth_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("   ", "600px");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Width cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyHeight_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("800px", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Height cannot be empty*")
            .And.ParamName.Should().Be("height");
    }

    [Fact]
    public void Create_WithNullHeight_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("800px", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Height cannot be empty*");
    }

    [Fact]
    public void Create_WithInvalidWidthUnit_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("800pt", "600px");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Width '800pt' is not a valid dimension*")
            .And.ParamName.Should().Be("width");
    }

    [Fact]
    public void Create_WithInvalidHeightUnit_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("800px", "600cm");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Height '600cm' is not a valid dimension*")
            .And.ParamName.Should().Be("height");
    }

    [Fact]
    public void Create_WithNoUnit_ShouldThrowArgumentException()
    {
        // Arrange & Act
        Action act = () => ShareConfiguration.Create("800", "600px");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Width '800' is not a valid dimension*");
    }

    [Fact]
    public void Create_WithCustomCssTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var longCss = new string('x', 10001);

        // Act
        Action act = () => ShareConfiguration.Create(
            "800px",
            "600px",
            customCss: longCss);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Custom CSS must not exceed 10000 characters*")
            .And.ParamName.Should().Be("customCss");
    }

    [Fact]
    public void Create_WithMaxLengthCustomCss_ShouldSucceed()
    {
        // Arrange
        var maxCss = new string('x', 10000);

        // Act
        var config = ShareConfiguration.Create(
            "800px",
            "600px",
            customCss: maxCss);

        // Assert
        config.CustomCss.Should().HaveLength(10000);
    }

    [Fact]
    public void Create_WithNullCustomCss_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("800px", "600px", customCss: null);

        // Assert
        config.CustomCss.Should().BeNull();
    }

    [Fact]
    public void Create_CaseInsensitiveUnits_ShouldSucceed()
    {
        // Arrange & Act
        var config1 = ShareConfiguration.Create("800PX", "600PX");
        var config2 = ShareConfiguration.Create("100VH", "100VW");
        var config3 = ShareConfiguration.Create("50EM", "30REM");

        // Assert
        config1.Should().NotBeNull();
        config2.Should().NotBeNull();
        config3.Should().NotBeNull();
    }

    #endregion

    #region Value Object Equality Tests

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px");
        var config2 = ShareConfiguration.Create("800px", "600px");

        // Act & Assert
        config1.Should().Be(config2);
        (config1 == config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentWidth_ShouldNotBeEqual()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px");
        var config2 = ShareConfiguration.Create("1024px", "600px");

        // Act & Assert
        config1.Should().NotBe(config2);
        (config1 != config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentHeight_ShouldNotBeEqual()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px");
        var config2 = ShareConfiguration.Create("800px", "768px");

        // Act & Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void Equality_DifferentFlags_ShouldNotBeEqual()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px", showZoomControls: true);
        var config2 = ShareConfiguration.Create("800px", "600px", showZoomControls: false);

        // Act & Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void Equality_DifferentCustomCss_ShouldNotBeEqual()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px", customCss: "body { color: red; }");
        var config2 = ShareConfiguration.Create("800px", "600px", customCss: "body { color: blue; }");

        // Act & Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void Equality_CompletelyDifferent_ShouldNotBeEqual()
    {
        // Arrange
        var config1 = ShareConfiguration.Create(
            "800px", "600px",
            showZoomControls: true,
            showLayerSwitcher: true,
            showSearch: false);

        var config2 = ShareConfiguration.Create(
            "1024px", "768px",
            showZoomControls: false,
            showLayerSwitcher: false,
            showSearch: true);

        // Act & Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void GetHashCode_SameValues_ShouldProduceSameHashCode()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px");
        var config2 = ShareConfiguration.Create("800px", "600px");

        // Act & Assert
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ShouldProduceDifferentHashCodes()
    {
        // Arrange
        var config1 = ShareConfiguration.Create("800px", "600px");
        var config2 = ShareConfiguration.Create("1024px", "768px");

        // Act & Assert
        config1.GetHashCode().Should().NotBe(config2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldIncludeKeyInformation()
    {
        // Arrange
        var config = ShareConfiguration.Create("800px", "600px");

        // Act
        var stringRepresentation = config.ToString();

        // Assert
        stringRepresentation.Should().Contain("ShareConfiguration");
        stringRepresentation.Should().Contain("800px");
        stringRepresentation.Should().Contain("600px");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithMixedCaseUnits_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create("800Px", "600pX");

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void Create_AllFlagsEnabled_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create(
            "800px", "600px",
            showZoomControls: true,
            showLayerSwitcher: true,
            showSearch: true,
            showScaleBar: true,
            showAttribution: true,
            allowFullscreen: true);

        // Assert
        config.ShowZoomControls.Should().BeTrue();
        config.ShowLayerSwitcher.Should().BeTrue();
        config.ShowSearch.Should().BeTrue();
        config.ShowScaleBar.Should().BeTrue();
        config.ShowAttribution.Should().BeTrue();
        config.AllowFullscreen.Should().BeTrue();
    }

    [Fact]
    public void Create_AllFlagsDisabled_ShouldSucceed()
    {
        // Arrange & Act
        var config = ShareConfiguration.Create(
            "800px", "600px",
            showZoomControls: false,
            showLayerSwitcher: false,
            showSearch: false,
            showScaleBar: false,
            showAttribution: false,
            allowFullscreen: false);

        // Assert
        config.ShowZoomControls.Should().BeFalse();
        config.ShowLayerSwitcher.Should().BeFalse();
        config.ShowSearch.Should().BeFalse();
        config.ShowScaleBar.Should().BeFalse();
        config.ShowAttribution.Should().BeFalse();
        config.AllowFullscreen.Should().BeFalse();
    }

    [Fact]
    public void CreateDefault_MultipleCalls_ShouldReturnEqualConfigurations()
    {
        // Arrange & Act
        var config1 = ShareConfiguration.CreateDefault();
        var config2 = ShareConfiguration.CreateDefault();

        // Assert
        config1.Should().Be(config2);
    }

    #endregion
}
