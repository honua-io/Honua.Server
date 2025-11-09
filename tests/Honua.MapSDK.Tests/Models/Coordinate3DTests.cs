using Honua.MapSDK.Models;
using Xunit;

namespace Honua.MapSDK.Tests.Models;

/// <summary>
/// Unit tests for Coordinate3D model.
/// Tests 2D, 3D, and 4D coordinate handling, parsing, and validation.
/// </summary>
public class Coordinate3DTests
{
    #region ToArray Tests

    [Fact]
    public void ToArray_2DCoordinate_Returns2ElementArray()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749
        };

        // Act
        var array = coord.ToArray();

        // Assert
        Assert.Equal(2, array.Length);
        Assert.Equal(-122.4194, array[0]);
        Assert.Equal(37.7749, array[1]);
    }

    [Fact]
    public void ToArray_3DCoordinate_Returns3ElementArray()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        // Act
        var array = coord.ToArray();

        // Assert
        Assert.Equal(3, array.Length);
        Assert.Equal(-122.4194, array[0]);
        Assert.Equal(37.7749, array[1]);
        Assert.Equal(50.0, array[2]);
    }

    [Fact]
    public void ToArray_4DCoordinate_Returns4ElementArray()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0,
            Measure = 100.0
        };

        // Act
        var array = coord.ToArray();

        // Assert
        Assert.Equal(4, array.Length);
        Assert.Equal(-122.4194, array[0]);
        Assert.Equal(37.7749, array[1]);
        Assert.Equal(50.0, array[2]);
        Assert.Equal(100.0, array[3]);
    }

    [Fact]
    public void ToArray_3DWithMeasureOnly_Returns3ElementArray()
    {
        // Arrange - M without Z (linear referencing)
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Measure = 100.0
        };

        // Act
        var array = coord.ToArray();

        // Assert
        Assert.Equal(3, array.Length);
        Assert.Equal(-122.4194, array[0]);
        Assert.Equal(37.7749, array[1]);
        Assert.Equal(100.0, array[2]);
    }

    #endregion

    #region FromArray Tests

    [Theory]
    [InlineData(new double[] { -122.0, 37.0 }, 2)]
    [InlineData(new double[] { -122.0, 37.0, 50.0 }, 3)]
    [InlineData(new double[] { -122.0, 37.0, 50.0, 100.0 }, 4)]
    public void FromArray_ValidArrays_DetectsCorrectDimension(double[] input, int expectedDim)
    {
        // Act
        var coord = Coordinate3D.FromArray(input);

        // Assert
        Assert.Equal(expectedDim, coord.Dimension);
    }

    [Fact]
    public void FromArray_2DArray_SetsCorrectValues()
    {
        // Arrange
        var array = new double[] { -122.4194, 37.7749 };

        // Act
        var coord = Coordinate3D.FromArray(array);

        // Assert
        Assert.Equal(-122.4194, coord.Longitude);
        Assert.Equal(37.7749, coord.Latitude);
        Assert.Null(coord.Elevation);
        Assert.Null(coord.Measure);
    }

    [Fact]
    public void FromArray_3DArray_SetsCorrectValues()
    {
        // Arrange
        var array = new double[] { -122.4194, 37.7749, 50.0 };

        // Act
        var coord = Coordinate3D.FromArray(array);

        // Assert
        Assert.Equal(-122.4194, coord.Longitude);
        Assert.Equal(37.7749, coord.Latitude);
        Assert.Equal(50.0, coord.Elevation);
        Assert.Null(coord.Measure);
    }

    [Fact]
    public void FromArray_4DArray_SetsCorrectValues()
    {
        // Arrange
        var array = new double[] { -122.4194, 37.7749, 50.0, 100.0 };

        // Act
        var coord = Coordinate3D.FromArray(array);

        // Assert
        Assert.Equal(-122.4194, coord.Longitude);
        Assert.Equal(37.7749, coord.Latitude);
        Assert.Equal(50.0, coord.Elevation);
        Assert.Equal(100.0, coord.Measure);
    }

    [Fact]
    public void FromArray_InvalidArray_ThrowsException()
    {
        // Arrange
        var array = new double[] { -122.4194 }; // Only 1 element

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Coordinate3D.FromArray(array));
    }

    #endregion

    #region Dimension Tests

    [Fact]
    public void Dimension_2DCoordinate_Returns2()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749
        };

        // Act & Assert
        Assert.Equal(2, coord.Dimension);
    }

    [Fact]
    public void Dimension_3DCoordinateWithZ_Returns3()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        // Act & Assert
        Assert.Equal(3, coord.Dimension);
    }

    [Fact]
    public void Dimension_3DCoordinateWithM_Returns3()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Measure = 100.0
        };

        // Act & Assert
        Assert.Equal(3, coord.Dimension);
    }

    [Fact]
    public void Dimension_4DCoordinate_Returns4()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0,
            Measure = 100.0
        };

        // Act & Assert
        Assert.Equal(4, coord.Dimension);
    }

    #endregion

    #region HasZ and HasM Tests

    [Fact]
    public void HasZ_CoordinateWithElevation_ReturnsTrue()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        // Act & Assert
        Assert.True(coord.HasZ);
    }

    [Fact]
    public void HasZ_CoordinateWithoutElevation_ReturnsFalse()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749
        };

        // Act & Assert
        Assert.False(coord.HasZ);
    }

    [Fact]
    public void HasM_CoordinateWithMeasure_ReturnsTrue()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Measure = 100.0
        };

        // Act & Assert
        Assert.True(coord.HasM);
    }

    [Fact]
    public void HasM_CoordinateWithoutMeasure_ReturnsFalse()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        // Act & Assert
        Assert.False(coord.HasM);
    }

    #endregion

    #region OGC Type Suffix Tests

    [Fact]
    public void GetOgcTypeSuffix_2DCoordinate_ReturnsEmptyString()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749
        };

        // Act
        var suffix = coord.GetOgcTypeSuffix();

        // Assert
        Assert.Equal("", suffix);
    }

    [Fact]
    public void GetOgcTypeSuffix_3DWithZ_ReturnsZ()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        // Act
        var suffix = coord.GetOgcTypeSuffix();

        // Assert
        Assert.Equal("Z", suffix);
    }

    [Fact]
    public void GetOgcTypeSuffix_3DWithM_ReturnsM()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Measure = 100.0
        };

        // Act
        var suffix = coord.GetOgcTypeSuffix();

        // Assert
        Assert.Equal("M", suffix);
    }

    [Fact]
    public void GetOgcTypeSuffix_4DWithZM_ReturnsZM()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0,
            Measure = 100.0
        };

        // Act
        var suffix = coord.GetOgcTypeSuffix();

        // Assert
        Assert.Equal("ZM", suffix);
    }

    #endregion

    #region Factory Methods Tests

    [Fact]
    public void Create2D_CreatesCorrect2DCoordinate()
    {
        // Act
        var coord = Coordinate3D.Create2D(-122.4194, 37.7749);

        // Assert
        Assert.Equal(-122.4194, coord.Longitude);
        Assert.Equal(37.7749, coord.Latitude);
        Assert.Null(coord.Elevation);
        Assert.Null(coord.Measure);
        Assert.Equal(2, coord.Dimension);
    }

    [Fact]
    public void Create3D_CreatesCorrect3DCoordinate()
    {
        // Act
        var coord = Coordinate3D.Create3D(-122.4194, 37.7749, 50.0);

        // Assert
        Assert.Equal(-122.4194, coord.Longitude);
        Assert.Equal(37.7749, coord.Latitude);
        Assert.Equal(50.0, coord.Elevation);
        Assert.Null(coord.Measure);
        Assert.Equal(3, coord.Dimension);
    }

    [Fact]
    public void Create4D_CreatesCorrect4DCoordinate()
    {
        // Act
        var coord = Coordinate3D.Create4D(-122.4194, 37.7749, 50.0, 100.0);

        // Assert
        Assert.Equal(-122.4194, coord.Longitude);
        Assert.Equal(37.7749, coord.Latitude);
        Assert.Equal(50.0, coord.Elevation);
        Assert.Equal(100.0, coord.Measure);
        Assert.Equal(4, coord.Dimension);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(-122.4194, 37.7749, true)]  // Valid WGS84
    [InlineData(-180.0, -90.0, true)]        // Min bounds
    [InlineData(180.0, 90.0, true)]          // Max bounds
    [InlineData(-181.0, 37.0, false)]        // Longitude too low
    [InlineData(181.0, 37.0, false)]         // Longitude too high
    [InlineData(-122.0, -91.0, false)]       // Latitude too low
    [InlineData(-122.0, 91.0, false)]        // Latitude too high
    public void IsValid_VariousCoordinates_ValidatesCorrectly(double lon, double lat, bool expected)
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = lon,
            Latitude = lat
        };

        // Act
        var isValid = coord.IsValid();

        // Assert
        Assert.Equal(expected, isValid);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_2DCoordinate_FormatsCorrectly()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749
        };

        // Act
        var str = coord.ToString();

        // Assert
        Assert.Equal("(-122.4194, 37.7749)", str);
    }

    [Fact]
    public void ToString_3DCoordinate_FormatsCorrectly()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0
        };

        // Act
        var str = coord.ToString();

        // Assert
        Assert.Equal("(-122.4194, 37.7749, 50)", str);
    }

    [Fact]
    public void ToString_4DCoordinate_FormatsCorrectly()
    {
        // Arrange
        var coord = new Coordinate3D
        {
            Longitude = -122.4194,
            Latitude = 37.7749,
            Elevation = 50.0,
            Measure = 100.0
        };

        // Act
        var str = coord.ToString();

        // Assert
        Assert.Equal("(-122.4194, 37.7749, 50, 100)", str);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_2DCoordinate_PreservesData()
    {
        // Arrange
        var original = Coordinate3D.Create2D(-122.4194, 37.7749);

        // Act
        var array = original.ToArray();
        var restored = Coordinate3D.FromArray(array);

        // Assert
        Assert.Equal(original.Longitude, restored.Longitude);
        Assert.Equal(original.Latitude, restored.Latitude);
        Assert.Equal(original.Elevation, restored.Elevation);
        Assert.Equal(original.Measure, restored.Measure);
        Assert.Equal(original.Dimension, restored.Dimension);
    }

    [Fact]
    public void RoundTrip_3DCoordinate_PreservesData()
    {
        // Arrange
        var original = Coordinate3D.Create3D(-122.4194, 37.7749, 50.0);

        // Act
        var array = original.ToArray();
        var restored = Coordinate3D.FromArray(array);

        // Assert
        Assert.Equal(original.Longitude, restored.Longitude);
        Assert.Equal(original.Latitude, restored.Latitude);
        Assert.Equal(original.Elevation, restored.Elevation);
        Assert.Equal(original.Measure, restored.Measure);
        Assert.Equal(original.Dimension, restored.Dimension);
    }

    [Fact]
    public void RoundTrip_4DCoordinate_PreservesData()
    {
        // Arrange
        var original = Coordinate3D.Create4D(-122.4194, 37.7749, 50.0, 100.0);

        // Act
        var array = original.ToArray();
        var restored = Coordinate3D.FromArray(array);

        // Assert
        Assert.Equal(original.Longitude, restored.Longitude);
        Assert.Equal(original.Latitude, restored.Latitude);
        Assert.Equal(original.Elevation, restored.Elevation);
        Assert.Equal(original.Measure, restored.Measure);
        Assert.Equal(original.Dimension, restored.Dimension);
    }

    #endregion
}
