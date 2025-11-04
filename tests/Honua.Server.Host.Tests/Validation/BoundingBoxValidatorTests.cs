using Honua.Server.Host.Validation;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

public sealed class BoundingBoxValidatorTests
{
    [Fact]
    public void IsValid2D_ValidBoundingBox_ReturnsTrue()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0, 90.0 }; // World bounds in WGS84

        // Act
        var result = BoundingBoxValidator.IsValid2D(bbox, 4326);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid2D_MinGreaterThanMax_ReturnsFalse()
    {
        // Arrange
        var bbox = new[] { 180.0, -90.0, -180.0, 90.0 }; // minX > maxX

        // Act
        var result = BoundingBoxValidator.IsValid2D(bbox, 4326);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid2D_NullBbox_ReturnsFalse()
    {
        // Act
        var result = BoundingBoxValidator.IsValid2D(null!, 4326);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid2D_WrongLength_ReturnsFalse()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0 }; // Only 3 coordinates

        // Act
        var result = BoundingBoxValidator.IsValid2D(bbox, 4326);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(double.NaN, -90.0, 180.0, 90.0)]
    [InlineData(-180.0, double.NaN, 180.0, 90.0)]
    [InlineData(-180.0, -90.0, double.NaN, 90.0)]
    [InlineData(-180.0, -90.0, 180.0, double.NaN)]
    public void IsValid2D_ContainsNaN_ReturnsFalse(double minX, double minY, double maxX, double maxY)
    {
        // Arrange
        var bbox = new[] { minX, minY, maxX, maxY };

        // Act
        var result = BoundingBoxValidator.IsValid2D(bbox, 4326);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(-181.0, -90.0, 180.0, 90.0)] // Longitude out of range
    [InlineData(-180.0, -91.0, 180.0, 90.0)] // Latitude out of range
    [InlineData(-180.0, -90.0, 181.0, 90.0)] // Longitude out of range
    [InlineData(-180.0, -90.0, 180.0, 91.0)] // Latitude out of range
    public void IsValid2D_GeographicCrsOutOfBounds_ReturnsFalse(double minX, double minY, double maxX, double maxY)
    {
        // Arrange
        var bbox = new[] { minX, minY, maxX, maxY };

        // Act
        var result = BoundingBoxValidator.IsValid2D(bbox, 4326); // WGS84

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid3D_ValidBoundingBox_ReturnsTrue()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, -100.0, 180.0, 90.0, 8000.0 }; // With altitude

        // Act
        var result = BoundingBoxValidator.IsValid3D(bbox, 4326);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid3D_MinZGreaterThanMaxZ_ReturnsFalse()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 8000.0, 180.0, 90.0, -100.0 }; // minZ > maxZ

        // Act
        var result = BoundingBoxValidator.IsValid3D(bbox, 4326);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid3D_AltitudeOutOfBounds_ReturnsFalse()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, -20000.0, 180.0, 90.0, 0.0 }; // Too deep

        // Act
        var result = BoundingBoxValidator.IsValid3D(bbox, 4326);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(4)] // 2D
    [InlineData(6)] // 3D
    public void IsValid_ValidLengths_ReturnsTrue(int length)
    {
        // Arrange
        double[] bbox = length == 4
            ? new[] { -180.0, -90.0, 180.0, 90.0 }
            : new[] { -180.0, -90.0, -100.0, 180.0, 90.0, 8000.0 };

        // Act
        var result = BoundingBoxValidator.IsValid(bbox, 4326);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void IsValid_InvalidLength_ReturnsFalse(int length)
    {
        // Arrange
        var bbox = new double[length];

        // Act
        var result = BoundingBoxValidator.IsValid(bbox, 4326);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_ValidBoundingBox_DoesNotThrow()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0, 90.0 };

        // Act & Assert
        var exception = Record.Exception(() => BoundingBoxValidator.Validate(bbox, 4326));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MinXGreaterThanMaxX_ThrowsArgumentException()
    {
        // Arrange
        var bbox = new[] { 180.0, -90.0, -180.0, 90.0 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => BoundingBoxValidator.Validate(bbox, 4326));
        Assert.Contains("minX", exception.Message);
        Assert.Contains("maxX", exception.Message);
    }

    [Fact]
    public void Validate_MinYGreaterThanMaxY_ThrowsArgumentException()
    {
        // Arrange
        var bbox = new[] { -180.0, 90.0, 180.0, -90.0 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => BoundingBoxValidator.Validate(bbox, 4326));
        Assert.Contains("minY", exception.Message);
        Assert.Contains("maxY", exception.Message);
    }

    [Fact]
    public void Validate_NullBbox_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => BoundingBoxValidator.Validate(null!));
    }

    [Fact]
    public void Validate_InvalidLength_ThrowsArgumentException()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0 }; // Only 3 coordinates

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => BoundingBoxValidator.Validate(bbox));
        Assert.Contains("4 coordinates", exception.Message);
        Assert.Contains("6 coordinates", exception.Message);
    }

    [Fact]
    public void Validate_GeographicCrsLongitudeOutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var bbox = new[] { -181.0, -90.0, 180.0, 90.0 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => BoundingBoxValidator.Validate(bbox, 4326));
        Assert.Contains("longitude", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_GeographicCrsLatitudeOutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var bbox = new[] { -180.0, -91.0, 180.0, 90.0 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => BoundingBoxValidator.Validate(bbox, 4326));
        Assert.Contains("latitude", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_ValidBbox_ReturnsTrueWithNoError()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0, 90.0 };

        // Act
        var result = BoundingBoxValidator.TryValidate(bbox, 4326, out var errorMessage);

        // Assert
        Assert.True(result);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidate_InvalidBbox_ReturnsFalseWithError()
    {
        // Arrange
        var bbox = new[] { 180.0, -90.0, -180.0, 90.0 }; // minX > maxX

        // Act
        var result = BoundingBoxValidator.TryValidate(bbox, 4326, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
    }

    [Theory]
    [InlineData(-180.0, -90.0, 180.0, 90.0, 64800.0)] // World bounds: 360 * 180
    [InlineData(-100.0, -50.0, 100.0, 50.0, 20000.0)] // 200 * 100
    [InlineData(0.0, 0.0, 1.0, 1.0, 1.0)] // 1 * 1
    public void GetArea_VariousBoundingBoxes_ReturnsCorrectArea(
        double minX, double minY, double maxX, double maxY, double expectedArea)
    {
        // Arrange
        var bbox = new[] { minX, minY, maxX, maxY };

        // Act
        var area = BoundingBoxValidator.GetArea(bbox);

        // Assert
        Assert.Equal(expectedArea, area, precision: 5);
    }

    [Fact]
    public void IsTooLarge_WorldBounds_ReturnsTrue()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0, 90.0 }; // Entire world

        // Act
        var result = BoundingBoxValidator.IsTooLarge(bbox, 4326, maxAreaSquareDegrees: 10000.0);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTooLarge_SmallArea_ReturnsFalse()
    {
        // Arrange
        var bbox = new[] { -1.0, -1.0, 1.0, 1.0 }; // 4 square degrees

        // Act
        var result = BoundingBoxValidator.IsTooLarge(bbox, 4326, maxAreaSquareDegrees: 10000.0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidBoundingBoxAttribute_ValidBbox_Succeeds()
    {
        // Arrange
        var attribute = new ValidBoundingBoxAttribute(srid: 4326, allow3D: true);
        var bbox = new[] { -180.0, -90.0, 180.0, 90.0 };

        // Act
        var result = attribute.IsValid(bbox);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidBoundingBoxAttribute_3DBbox_Allow3DFalse_Fails()
    {
        // Arrange
        var attribute = new ValidBoundingBoxAttribute(srid: 4326, allow3D: false);
        var bbox = new[] { -180.0, -90.0, -100.0, 180.0, 90.0, 8000.0 };

        // Act
        var result = attribute.IsValid(bbox);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidBoundingBoxAttribute_InvalidBbox_Fails()
    {
        // Arrange
        var attribute = new ValidBoundingBoxAttribute(srid: 4326);
        var bbox = new[] { 180.0, -90.0, -180.0, 90.0 }; // minX > maxX

        // Act
        var result = attribute.IsValid(bbox);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidBoundingBoxAttribute_NullValue_Succeeds()
    {
        // Arrange
        var attribute = new ValidBoundingBoxAttribute();

        // Act
        var result = attribute.IsValid(null);

        // Assert
        Assert.True(result);
    }
}
