using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Validation;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Geometry;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class ThreeDimensionalValidatorTests
{
    private readonly GeometryFactory _factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    #region ValidateLayerConfiguration Tests

    [Fact]
    public void ValidateLayerConfiguration_ValidLayer_ReturnsValid()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: true, storageHasZ: true);

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateLayerConfiguration_LayerHasZButStorageDoesNot_ReturnsWarning()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: true, storageHasZ: false);

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("storage hasZ is false");
    }

    [Fact]
    public void ValidateLayerConfiguration_StorageHasZButLayerDoesNot_ReturnsWarning()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: false, storageHasZ: true);

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("layer hasZ is false");
    }

    [Fact]
    public void ValidateLayerConfiguration_3DLayerWithoutCRS84H_ReturnsSuggestion()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: true, storageHasZ: true, includeCrs84H: false);

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Suggestions.Should().ContainSingle()
            .Which.Should().Contain("CRS84H");
    }

    [Fact]
    public void ValidateLayerConfiguration_3DLayerWith2DBbox_ReturnsSuggestion()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: true, storageHasZ: true, bbox: new[] { -180.0, -90.0, 180.0, 90.0 });

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Suggestions.Should().Contain(s => s.Contains("6-value bbox"));
    }

    [Fact]
    public void ValidateLayerConfiguration_3DBboxWithInvalidZRange_ReturnsError()
    {
        // Arrange
        var layer = CreateTestLayer(
            hasZ: true,
            storageHasZ: true,
            bbox: new[] { -180.0, -90.0, 100.0, 180.0, 90.0, -100.0 }); // minZ > maxZ

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("minZ").And.Contain("maxZ");
    }

    [Fact]
    public void ValidateLayerConfiguration_InvalidBboxLength_ReturnsError()
    {
        // Arrange
        var layer = CreateTestLayer(
            hasZ: true,
            storageHasZ: true,
            bbox: new[] { -180.0, -90.0, 180.0 }); // Invalid length

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("4 values").And.Contain("6 values");
    }

    [Fact]
    public void ValidateLayerConfiguration_ZFieldSpecifiedButNoHasZ_ReturnsWarning()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: false, zField: "elevation");

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("zField") && w.Contains("hasZ=false"));
    }

    [Fact]
    public void ValidateLayerConfiguration_ZFieldNotInFields_ReturnsError()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: true, zField: "nonexistent_field");

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("zField") && e.Contains("no matching field"));
    }

    [Fact]
    public void ValidateLayerConfiguration_HasZWith2DGeometryType_ReturnsSuggestion()
    {
        // Arrange
        var layer = CreateTestLayer(hasZ: true, geometryType: "Point");

        // Act
        var result = ThreeDimensionalValidator.ValidateLayerConfiguration(layer);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Suggestions.Should().Contain(s => s.Contains("PointZ"));
    }

    [Fact]
    public void ValidateLayerConfiguration_NullLayer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ThreeDimensionalValidator.ValidateLayerConfiguration(null!));
    }

    #endregion

    #region ValidateBoundingBox Tests

    [Fact]
    public void ValidateBoundingBox_Valid2DBbox_ReturnsValid()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 180.0, 90.0 };

        // Act
        var result = ThreeDimensionalValidator.ValidateBoundingBox(bbox);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateBoundingBox_Valid3DBbox_ReturnsValid()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, -100.0, 180.0, 90.0, 8000.0 };

        // Act
        var result = ThreeDimensionalValidator.ValidateBoundingBox(bbox);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateBoundingBox_2DMinXGreaterThanMaxX_ReturnsError()
    {
        // Arrange
        var bbox = new[] { 180.0, -90.0, -180.0, 90.0 };

        // Act
        var result = ThreeDimensionalValidator.ValidateBoundingBox(bbox);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("minX") && e.Contains("maxX"));
    }

    [Fact]
    public void ValidateBoundingBox_2DMinYGreaterThanMaxY_ReturnsError()
    {
        // Arrange
        var bbox = new[] { -180.0, 90.0, 180.0, -90.0 };

        // Act
        var result = ThreeDimensionalValidator.ValidateBoundingBox(bbox);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("minY") && e.Contains("maxY"));
    }

    [Fact]
    public void ValidateBoundingBox_3DMinZGreaterThanMaxZ_ReturnsError()
    {
        // Arrange
        var bbox = new[] { -180.0, -90.0, 100.0, 180.0, 90.0, -100.0 };

        // Act
        var result = ThreeDimensionalValidator.ValidateBoundingBox(bbox);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("minZ") && e.Contains("maxZ"));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void ValidateBoundingBox_InvalidLength_ReturnsError(int length)
    {
        // Arrange
        var bbox = new double[length];

        // Act
        var result = ThreeDimensionalValidator.ValidateBoundingBox(bbox);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("4 values") && e.Contains("6 values"));
    }

    [Fact]
    public void ValidateBoundingBox_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ThreeDimensionalValidator.ValidateBoundingBox(null!));
    }

    #endregion

    #region ValidateZCoordinate Tests

    [Fact]
    public void ValidateZCoordinate_ValidElevation_ReturnsValid()
    {
        // Arrange
        var z = 100.0;

        // Act
        var result = ThreeDimensionalValidator.ValidateZCoordinate(z);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateZCoordinate_NaN_ReturnsWarning()
    {
        // Arrange
        var z = double.NaN;

        // Act
        var result = ThreeDimensionalValidator.ValidateZCoordinate(z);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("NaN");
    }

    [Fact]
    public void ValidateZCoordinate_Infinity_ReturnsError()
    {
        // Arrange
        var z = double.PositiveInfinity;

        // Act
        var result = ThreeDimensionalValidator.ValidateZCoordinate(z);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("infinite");
    }

    [Fact]
    public void ValidateZCoordinate_BelowMarianaTrench_ReturnsWarning()
    {
        // Arrange
        var z = -12000.0; // Below Mariana Trench

        // Act
        var result = ThreeDimensionalValidator.ValidateZCoordinate(z);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("Mariana Trench");
    }

    [Fact]
    public void ValidateZCoordinate_AboveExosphere_ReturnsWarning()
    {
        // Arrange
        var z = 150000.0; // Above exosphere

        // Act
        var result = ThreeDimensionalValidator.ValidateZCoordinate(z);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("exosphere");
    }

    [Fact]
    public void ValidateZCoordinate_WithContext_IncludesContextInMessage()
    {
        // Arrange
        var z = double.NaN;
        var context = "building height";

        // Act
        var result = ThreeDimensionalValidator.ValidateZCoordinate(z, context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("building height");
    }

    #endregion

    #region HasConsistentZCoordinates Tests

    [Fact]
    public void HasConsistentZCoordinates_AllHaveZ_ReturnsTrue()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 10, 200)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = ThreeDimensionalValidator.HasConsistentZCoordinates(lineString);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasConsistentZCoordinates_NoneHaveZ_ReturnsTrue()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = ThreeDimensionalValidator.HasConsistentZCoordinates(lineString);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasConsistentZCoordinates_MixedZ_ReturnsFalse()
    {
        // Arrange - Create a geometry with mixed Z coordinates
        var coords = new Coordinate[]
        {
            new CoordinateZ(0, 0, 100),
            new Coordinate(10, 10) // No Z
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var result = ThreeDimensionalValidator.HasConsistentZCoordinates(lineString);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasConsistentZCoordinates_NullGeometry_ReturnsTrue()
    {
        // Act
        var result = ThreeDimensionalValidator.HasConsistentZCoordinates(null!);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasConsistentZCoordinates_EmptyGeometry_ReturnsTrue()
    {
        // Arrange
        var emptyPoint = _factory.CreatePoint((Coordinate)null!);

        // Act
        var result = ThreeDimensionalValidator.HasConsistentZCoordinates(emptyPoint);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetZStatistics Tests

    [Fact]
    public void GetZStatistics_GeometryWithZ_ReturnsCorrectStats()
    {
        // Arrange
        var coords = new[]
        {
            new CoordinateZ(0, 0, 100),
            new CoordinateZ(10, 10, 200),
            new CoordinateZ(20, 20, 150)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var stats = ThreeDimensionalValidator.GetZStatistics(lineString);

        // Assert
        stats.HasZ.Should().BeTrue();
        stats.Count.Should().Be(3);
        stats.Min.Should().Be(100);
        stats.Max.Should().Be(200);
        stats.Mean.Should().Be(150);
        stats.Range.Should().Be(100);
    }

    [Fact]
    public void GetZStatistics_GeometryWithoutZ_ReturnsNoZ()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10)
        };
        var lineString = _factory.CreateLineString(coords);

        // Act
        var stats = ThreeDimensionalValidator.GetZStatistics(lineString);

        // Assert
        stats.HasZ.Should().BeFalse();
        stats.Count.Should().Be(0);
        stats.Min.Should().BeNull();
        stats.Max.Should().BeNull();
        stats.Mean.Should().BeNull();
        stats.Range.Should().BeNull();
    }

    [Fact]
    public void GetZStatistics_NullGeometry_ReturnsNoZ()
    {
        // Act
        var stats = ThreeDimensionalValidator.GetZStatistics(null!);

        // Assert
        stats.HasZ.Should().BeFalse();
        stats.Count.Should().Be(0);
    }

    [Fact]
    public void GetZStatistics_EmptyGeometry_ReturnsNoZ()
    {
        // Arrange
        var emptyPoint = _factory.CreatePoint((Coordinate)null!);

        // Act
        var stats = ThreeDimensionalValidator.GetZStatistics(emptyPoint);

        // Assert
        stats.HasZ.Should().BeFalse();
        stats.Count.Should().Be(0);
    }

    [Fact]
    public void GetZStatistics_SingleZValue_ReturnsCorrectStats()
    {
        // Arrange
        var point = _factory.CreatePoint(new CoordinateZ(10, 20, 100));

        // Act
        var stats = ThreeDimensionalValidator.GetZStatistics(point);

        // Assert
        stats.HasZ.Should().BeTrue();
        stats.Count.Should().Be(1);
        stats.Min.Should().Be(100);
        stats.Max.Should().Be(100);
        stats.Mean.Should().Be(100);
        stats.Range.Should().Be(0);
    }

    #endregion

    #region Helper Methods

    private LayerDefinition CreateTestLayer(
        bool hasZ = false,
        bool? storageHasZ = null,
        bool includeCrs84H = true,
        double[]? bbox = null,
        string? zField = null,
        string geometryType = "Point")
    {
        var crs = new List<string> { "EPSG:4326" };
        if (includeCrs84H && (hasZ || storageHasZ == true))
        {
            crs.Add("CRS84H");
        }

        LayerExtentDefinition? extent = null;
        if (bbox != null)
        {
            extent = new LayerExtentDefinition
            {
                Bbox = new[] { bbox },
                Crs = "EPSG:4326"
            };
        }

        LayerStorageDefinition? storage = null;
        if (storageHasZ.HasValue)
        {
            storage = new LayerStorageDefinition
            {
                Table = "test_table",
                GeometryColumn = "geom",
                PrimaryKey = "id",
                Srid = 4326,
                HasZ = storageHasZ.Value,
                HasM = false
            };
        }

        var fields = new List<FieldDefinition>
        {
            new() { Name = "id", DataType = "integer" },
            new() { Name = "name", DataType = "string" }
        };

        if (!string.IsNullOrWhiteSpace(zField))
        {
            if (zField == "elevation") // Add elevation field for valid tests
            {
                fields.Add(new FieldDefinition { Name = "elevation", DataType = "double" });
            }
        }

        return new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = geometryType,
            IdField = "id",
            GeometryField = "geom",
            Crs = crs,
            Extent = extent,
            Storage = storage,
            Fields = fields,
            HasZ = hasZ,
            HasM = false,
            ZField = zField
        };
    }

    #endregion
}
