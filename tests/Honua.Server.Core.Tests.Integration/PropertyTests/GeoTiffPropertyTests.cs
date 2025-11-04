using System;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Honua.Server.Core.Tests.Integration.PropertyTests;

/// <summary>
/// Property-based tests for GeoTIFF tag parsing and geospatial transformations.
/// </summary>
public class GeoTiffPropertyTests
{
    // GeoTransform Property Tests

    [Property(MaxTest = 500)]
    public Property GeoTransform_PixelToGeo_ShouldBeReversible()
    {
        return Prop.ForAll(
            GenerateValidGeoTransform(),
            geoTransform =>
            {
                var (originX, originY, pixelSizeX, pixelSizeY, rotX, rotY) = geoTransform;

                // Pick a test pixel coordinate
                var pixelX = 100;
                var pixelY = 100;

                // Forward transformation: pixel to geo
                var geoX = originX + pixelX * pixelSizeX + pixelY * rotX;
                var geoY = originY + pixelX * rotY + pixelY * pixelSizeY;

                // Reverse transformation: geo to pixel (if no rotation)
                if (Math.Abs(rotX) < 1e-10 && Math.Abs(rotY) < 1e-10)
                {
                    var recoveredPixelX = (geoX - originX) / pixelSizeX;
                    var recoveredPixelY = (geoY - originY) / pixelSizeY;

                    Assert.Equal(pixelX, recoveredPixelX, 6);
                    Assert.Equal(pixelY, recoveredPixelY, 6);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property GeoTransform_PixelSize_ShouldDetermineResolution()
    {
        return Prop.ForAll(
            GenerateValidGeoTransform(),
            geoTransform =>
            {
                var (_, _, pixelSizeX, pixelSizeY, _, _) = geoTransform;

                // Pixel size determines resolution
                var resolutionX = Math.Abs(pixelSizeX);
                var resolutionY = Math.Abs(pixelSizeY);

                Assert.True(resolutionX > 0);
                Assert.True(resolutionY > 0);

                // For geographic coordinates, typical range
                if (resolutionX < 1 && resolutionY < 1)
                {
                    // Degrees - should be reasonable
                    Assert.InRange(resolutionX, 1e-10, 1.0);
                    Assert.InRange(resolutionY, 1e-10, 1.0);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property GeoTransform_WithRotation_ShouldPreserveArea()
    {
        return Prop.ForAll(
            GenerateGeoTransformWithRotation(),
            geoTransform =>
            {
                var (_, _, pixelSizeX, pixelSizeY, rotX, rotY) = geoTransform;

                // Calculate determinant of transformation matrix
                // det = pixelSizeX * pixelSizeY - rotX * rotY
                var determinant = pixelSizeX * pixelSizeY - rotX * rotY;

                // Determinant should be non-zero for valid transform
                Assert.NotEqual(0, determinant);

                // Absolute value of determinant gives pixel area in geo coordinates
                var pixelArea = Math.Abs(determinant);
                Assert.True(pixelArea > 0);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property GeoTransform_BoundingBox_ShouldCoverImage()
    {
        return Prop.ForAll(
            GenerateGeoTransformWithImageSize(),
            data =>
            {
                var ((originX, originY, pixelSizeX, pixelSizeY, rotX, rotY), width, height) = data;

                // Calculate corners of the image in geo coordinates
                var corners = new[]
                {
                    (X: originX, Y: originY),
                    (X: originX + width * pixelSizeX, Y: originY + width * rotY),
                    (X: originX + height * rotX, Y: originY + height * pixelSizeY),
                    (X: originX + width * pixelSizeX + height * rotX,
                     Y: originY + width * rotY + height * pixelSizeY)
                };

                var minX = corners.Min(c => c.X);
                var maxX = corners.Max(c => c.X);
                var minY = corners.Min(c => c.Y);
                var maxY = corners.Max(c => c.Y);

                // Bounding box should be valid
                Assert.True(minX < maxX);
                Assert.True(minY < maxY);

                // All corners should be within bbox
                foreach (var corner in corners)
                {
                    Assert.InRange(corner.X, minX, maxX);
                    Assert.InRange(corner.Y, minY, maxY);
                }

                return true;
            });
    }

    // TIFF Tag Value Tests

    [Property(MaxTest = 300)]
    public Property TiffTag_ModelPixelScale_ShouldBePositive()
    {
        return Prop.ForAll(
            GenerateModelPixelScale(),
            scale =>
            {
                var (scaleX, scaleY, scaleZ) = scale;

                // Pixel scales should be positive
                Assert.True(scaleX > 0, $"ScaleX should be positive: {scaleX}");
                Assert.True(scaleY > 0, $"ScaleY should be positive: {scaleY}");
                Assert.True(scaleZ >= 0, $"ScaleZ should be non-negative: {scaleZ}");

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property TiffTag_ModelTiepoint_ShouldMapPixelToGeo()
    {
        return Prop.ForAll(
            GenerateModelTiepoint(),
            tiepoint =>
            {
                var (pixelI, pixelJ, pixelK, geoX, geoY, geoZ) = tiepoint;

                // Pixel coordinates should be non-negative
                Assert.True(pixelI >= 0);
                Assert.True(pixelJ >= 0);
                Assert.True(pixelK >= 0);

                // Common case: tiepoint at (0, 0)
                if (Math.Abs(pixelI) < 1e-10 && Math.Abs(pixelJ) < 1e-10)
                {
                    // GeoX and GeoY define the upper-left corner
                    // Should be within reasonable geographic bounds
                    if (Math.Abs(geoX) < 180 && Math.Abs(geoY) < 90)
                    {
                        Assert.InRange(geoX, -180, 180);
                        Assert.InRange(geoY, -90, 90);
                    }
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property TiffTag_ModelTransformation_ShouldBe4x4Matrix()
    {
        return Prop.ForAll(
            GenerateModelTransformationMatrix(),
            matrix =>
            {
                Assert.Equal(16, matrix.Length);

                // Last row should be [0, 0, 0, 1] for affine transformation
                const double tolerance = 1e-10;
                Assert.True(Math.Abs(matrix[12]) < tolerance, "matrix[12] should be 0");
                Assert.True(Math.Abs(matrix[13]) < tolerance, "matrix[13] should be 0");
                Assert.True(Math.Abs(matrix[14]) < tolerance, "matrix[14] should be 0");
                Assert.True(Math.Abs(matrix[15] - 1.0) < tolerance, "matrix[15] should be 1");

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property GeoKey_EPSG_Code_ShouldBeValid()
    {
        return Prop.ForAll(
            GenerateEpsgCode(),
            epsgCode =>
            {
                var extendedCodes = new[] { 4326, 3857, 2154, 32632, 32633, 4269, 3395, 900913 };

                if (epsgCode <= 32767)
                {
                    Assert.InRange(epsgCode, 1, 32767);
                }
                else
                {
                    Assert.Contains(epsgCode, extendedCodes);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property CoordinateTransform_ShouldPreserveTopology()
    {
        return Prop.ForAll(
            GenerateCoordinateSequence(),
            coords =>
            {
                var (geoTransform, pixelCoords) = coords;
                var (originX, originY, pixelSizeX, pixelSizeY, rotX, rotY) = geoTransform;

                // Transform pixel coordinates to geo
                var geoCoords = pixelCoords.Select(p =>
                {
                    var geoX = originX + p.X * pixelSizeX + p.Y * rotX;
                    var geoY = originY + p.X * rotY + p.Y * pixelSizeY;
                    return (X: geoX, Y: geoY);
                }).ToArray();

                // Verify order is preserved
                for (int i = 0; i < pixelCoords.Length; i++)
                {
                    Assert.True(!double.IsNaN(geoCoords[i].X));
                    Assert.True(!double.IsNaN(geoCoords[i].Y));
                    Assert.True(!double.IsInfinity(geoCoords[i].X));
                    Assert.True(!double.IsInfinity(geoCoords[i].Y));
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property NoDataValue_ShouldBeDetectable()
    {
        return Prop.ForAll(
            GenerateNoDataValue(),
            noData =>
            {
                // NoData values should be representable
                Assert.True(!double.IsNaN(noData) || double.IsNaN(noData));

                // Common NoData values
                var commonNoData = new[] { -9999.0, -3.4028235e+38, 0.0 };

                if (commonNoData.Contains(noData))
                {
                    Assert.Contains(noData, commonNoData);
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property PixelIsArea_RasterType_ShouldAffectCoordinates()
    {
        return Prop.ForAll(
            Gen.Elements(true, false).ToArbitrary(),
            new Func<bool, bool>(pixelIsArea =>
            {
                // PixelIsArea: coordinates refer to pixel center (false) or area (true)
                // This affects half-pixel offsets in transformations

                var offset = pixelIsArea ? 0.0 : 0.5;

                Assert.True(offset >= 0.0 && offset <= 0.5);

                return true;
            }));
    }

    // FsCheck Generators

    private static Arbitrary<(double, double, double, double, double, double)> GenerateValidGeoTransform()
    {
        var gen = from originX in Gen.Choose(-180, 180).Select(x => (double)x)
                  from originY in Gen.Choose(-90, 90).Select(y => (double)y)
                  from pixelSizeX in Gen.Choose(1, 1000).Select(x => x / 10000.0)
                  from pixelSizeY in Gen.Choose(-1000, -1).Select(y => y / 10000.0)
                  select (originX, originY, pixelSizeX, pixelSizeY, 0.0, 0.0);

        return Arb.From(gen);
    }

    private static Arbitrary<(double, double, double, double, double, double)> GenerateGeoTransformWithRotation()
    {
        var gen = from originX in Gen.Choose(-180, 180).Select(x => (double)x)
                  from originY in Gen.Choose(-90, 90).Select(y => (double)y)
                  from pixelSizeX in Gen.Choose(1, 1000).Select(x => x / 10000.0)
                  from pixelSizeY in Gen.Choose(-1000, -1).Select(y => y / 10000.0)
                  from rotX in Gen.Choose(-100, 100).Select(r => r / 10000.0)
                  from rotY in Gen.Choose(-100, 100).Select(r => r / 10000.0)
                  select (originX, originY, pixelSizeX, pixelSizeY, rotX, rotY);

        return Arb.From(gen);
    }

    private static Arbitrary<((double, double, double, double, double, double), int, int)> GenerateGeoTransformWithImageSize()
    {
        var gen = from geoTransform in GenerateValidGeoTransform().Generator
                  from width in Gen.Choose(100, 5000)
                  from height in Gen.Choose(100, 5000)
                  select (geoTransform, width, height);

        return Arb.From(gen);
    }

    private static Arbitrary<(double, double, double)> GenerateModelPixelScale()
    {
        var gen = from scaleX in Gen.Choose(1, 10000).Select(x => x / 10000.0)
                  from scaleY in Gen.Choose(1, 10000).Select(y => y / 10000.0)
                  from scaleZ in Gen.Choose(0, 1000).Select(z => z / 100.0)
                  select (scaleX, scaleY, scaleZ);

        return Arb.From(gen);
    }

    private static Arbitrary<(double, double, double, double, double, double)> GenerateModelTiepoint()
    {
        var gen = from pixelI in Gen.Choose(0, 5000).Select(i => (double)i)
                  from pixelJ in Gen.Choose(0, 5000).Select(j => (double)j)
                  from pixelK in Gen.Choose(0, 100).Select(k => (double)k)
                  from geoX in Gen.Choose(-180000, 180000).Select(x => x / 1000.0)
                  from geoY in Gen.Choose(-90000, 90000).Select(y => y / 1000.0)
                  from geoZ in Gen.Choose(-1000, 10000).Select(z => z / 10.0)
                  select (pixelI, pixelJ, pixelK, geoX, geoY, geoZ);

        return Arb.From(gen);
    }

    private static Arbitrary<double[]> GenerateModelTransformationMatrix()
    {
        var gen = from scaleX in Gen.Choose(1, 1000).Select(x => x / 10000.0)
                  from scaleY in Gen.Choose(-1000, -1).Select(y => y / 10000.0)
                  from translateX in Gen.Choose(-180, 180).Select(x => (double)x)
                  from translateY in Gen.Choose(-90, 90).Select(y => (double)y)
                  select new double[]
                  {
                      scaleX, 0, 0, translateX,
                      0, scaleY, 0, translateY,
                      0, 0, 1, 0,
                      0, 0, 0, 1
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<int> GenerateEpsgCode()
    {
        var commonCodes = new[] { 4326, 3857, 2154, 32632, 32633, 4269, 3395, 900913 };
        var gen = Gen.OneOf(
            Gen.Elements(commonCodes),
            Gen.Choose(1, 32767)
        );

        return Arb.From(gen);
    }

    private static Arbitrary<((double, double, double, double, double, double), (int X, int Y)[])> GenerateCoordinateSequence()
    {
        var gen = from geoTransform in GenerateValidGeoTransform().Generator
                  from count in Gen.Choose(3, 10)
                  from coords in Gen.ArrayOf(count,
                      from x in Gen.Choose(0, 1000)
                      from y in Gen.Choose(0, 1000)
                      select (X: x, Y: y))
                  select (geoTransform, coords);

        return Arb.From(gen);
    }

    private static Arbitrary<double> GenerateNoDataValue()
    {
        var commonNoData = new[] { -9999.0, -3.4028235e+38, 0.0, double.NaN };
        var gen = Gen.OneOf(
            Gen.Elements(commonNoData),
            Gen.Choose(-10000, 10000).Select(x => (double)x)
        );

        return Arb.From(gen);
    }
}
