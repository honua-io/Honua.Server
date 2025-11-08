using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Realistic GIS test data based on real-world patterns, formats, and edge cases.
/// Uses actual coordinate systems, projection codes, parcel ID formats, and addresses
/// from production GIS systems worldwide.
/// </summary>
public static class RealisticGisTestData
{
    private static readonly GeometryFactory Factory4326 = new(new PrecisionModel(), 4326);
    private static readonly GeometryFactory Factory2227 = new(new PrecisionModel(), 2227);
    private static readonly GeometryFactory Factory26910 = new(new PrecisionModel(), 26910);
    private static readonly GeometryFactory Factory3857 = new(new PrecisionModel(), 3857);

    #region Real Parcel ID Patterns

    /// <summary>
    /// California Assessor Parcel Number (APN) format: ###-###-###-###
    /// Example from Los Angeles County
    /// </summary>
    public static string CaliforniaParcelId => "123-456-789-000";

    /// <summary>
    /// New York Borough-Block-Lot format
    /// Example: Manhattan (1), Block 123, Lot 1
    /// </summary>
    public static string NewYorkTaxId => "1-00123-0001";

    /// <summary>
    /// Texas Property ID with dash separator
    /// Example from Harris County
    /// </summary>
    public static string TexasPropertyId => "0123-0001-0001-0000";

    /// <summary>
    /// Washington State Parcel Number format
    /// Example from King County
    /// </summary>
    public static string WashingtonParcelNumber => "322404-9075-06";

    /// <summary>
    /// Florida folio number format
    /// Example from Miami-Dade County
    /// </summary>
    public static string FloridaFolioNumber => "01-3213-023-0010";

    /// <summary>
    /// European INSPIRE parcel identifier
    /// </summary>
    public static string EuropeanParcelId => "DE-NW-12345678-001";

    /// <summary>
    /// Australian Lot/Plan format
    /// </summary>
    public static string AustralianLotPlan => "LOT 123 DP 456789";

    #endregion

    #region Real Addresses with Special Characters

    /// <summary>
    /// Address with apostrophe (O'Brien, O'Malley, etc.)
    /// Common in Irish and Scottish surnames
    /// </summary>
    public static string AddressWithApostrophe => "123 O'Brien Street, Apt #4A";

    /// <summary>
    /// Address with Unicode characters (Spanish)
    /// </summary>
    public static string AddressWithSpanishUnicode => "Calle José María López, 4º Izq.";

    /// <summary>
    /// Address with French Unicode characters
    /// </summary>
    public static string AddressWithFrenchUnicode => "12 Rue de l'Église, Côte d'Azur";

    /// <summary>
    /// Address with German umlauts
    /// </summary>
    public static string AddressWithGermanUnicode => "Münchener Straße 42, München";

    /// <summary>
    /// Address with ampersand
    /// </summary>
    public static string AddressWithAmpersand => "Smith & Johnson Building, Suite 200";

    /// <summary>
    /// Address with forward slash (common in unit numbers)
    /// </summary>
    public static string AddressWithSlash => "456 Main St, Unit 3/4";

    /// <summary>
    /// Address with hyphenated street name
    /// </summary>
    public static string AddressWithHyphen => "789 Twenty-First Avenue NE";

    /// <summary>
    /// Address with combining diacritics (é as e + combining acute)
    /// Tests normalization form handling
    /// </summary>
    public static string AddressWithCombiningDiacritics => "123 Cafe\u0301 Boulevard"; // Café with combining acute

    /// <summary>
    /// Asian address with mixed scripts (Japanese)
    /// </summary>
    public static string AddressWithJapanese => "東京都新宿区西新宿2-8-1";

    /// <summary>
    /// Right-to-left text (Arabic)
    /// </summary>
    public static string AddressWithArabic => "شارع الملك فهد، الرياض";

    #endregion

    #region Real City Coordinates (Major World Cities)

    /// <summary>
    /// New York City, NY, USA (Times Square)
    /// Typical North American urban coordinate
    /// </summary>
    public static (double lon, double lat) NewYork => (-73.9855, 40.7580);

    /// <summary>
    /// Tokyo, Japan (Shibuya Crossing)
    /// East Asian urban coordinate
    /// </summary>
    public static (double lon, double lat) Tokyo => (139.7006, 35.6595);

    /// <summary>
    /// Sydney, Australia (Opera House)
    /// Southern hemisphere coordinate
    /// </summary>
    public static (double lon, double lat) Sydney => (151.2153, -33.8568);

    /// <summary>
    /// London, United Kingdom (Big Ben)
    /// Western European coordinate
    /// </summary>
    public static (double lon, double lat) London => (-0.1246, 51.5007);

    /// <summary>
    /// São Paulo, Brazil
    /// Southern hemisphere, South American coordinate
    /// </summary>
    public static (double lon, double lat) SaoPaulo => (-46.6333, -23.5505);

    /// <summary>
    /// Mumbai, India
    /// South Asian coordinate
    /// </summary>
    public static (double lon, double lat) Mumbai => (72.8777, 19.0760);

    /// <summary>
    /// Cairo, Egypt
    /// North African coordinate
    /// </summary>
    public static (double lon, double lat) Cairo => (31.2357, 30.0444);

    /// <summary>
    /// Moscow, Russia
    /// Eastern European coordinate
    /// </summary>
    public static (double lon, double lat) Moscow => (37.6173, 55.7558);

    /// <summary>
    /// Reykjavik, Iceland
    /// High northern latitude coordinate
    /// </summary>
    public static (double lon, double lat) Reykjavik => (-21.8952, 64.1466);

    /// <summary>
    /// Ushuaia, Argentina
    /// Southernmost city, extreme southern latitude
    /// </summary>
    public static (double lon, double lat) Ushuaia => (-68.3029, -54.8019);

    /// <summary>
    /// Fiji (crosses antimeridian)
    /// Tests antimeridian handling
    /// </summary>
    public static (double lon, double lat) Fiji => (178.4419, -18.1416);

    /// <summary>
    /// Alert, Nunavut, Canada
    /// Northernmost permanently inhabited place
    /// </summary>
    public static (double lon, double lat) Alert => (-62.3481, 82.5018);

    #endregion

    #region Real SRID Values

    /// <summary>
    /// WGS 84 - World Geodetic System 1984
    /// Most common coordinate system for GPS and web mapping
    /// </summary>
    public static int WGS84 => 4326;

    /// <summary>
    /// Web Mercator (Google Maps, OpenStreetMap)
    /// Used by virtually all web mapping applications
    /// </summary>
    public static int WebMercator => 3857;

    /// <summary>
    /// NAD83 California State Plane Zone III (US Survey Feet)
    /// Used for California cadastral and engineering data
    /// </summary>
    public static int NAD83_StatePlane_CA_III_Feet => 2227;

    /// <summary>
    /// NAD83 UTM Zone 10N
    /// Used for Pacific Northwest US
    /// </summary>
    public static int NAD83_UTM_Zone_10N => 26910;

    /// <summary>
    /// NAD83 UTM Zone 18N
    /// Used for East Coast US (New York, etc.)
    /// </summary>
    public static int NAD83_UTM_Zone_18N => 26918;

    /// <summary>
    /// OSGB 1936 British National Grid
    /// Used in United Kingdom
    /// </summary>
    public static int OSGB_BritishNationalGrid => 27700;

    /// <summary>
    /// ETRS89 UTM Zone 32N
    /// Used in Central Europe
    /// </summary>
    public static int ETRS89_UTM_Zone_32N => 25832;

    /// <summary>
    /// GDA94 MGA Zone 55 (Australia)
    /// Used in Eastern Australia
    /// </summary>
    public static int GDA94_MGA_Zone_55 => 28355;

    /// <summary>
    /// Tokyo Datum (Japan Plane Rectangular CS IX)
    /// Used in Japan
    /// </summary>
    public static int TokyoDatum_JapanZone9 => 2451;

    /// <summary>
    /// NZGD2000 New Zealand Transverse Mercator
    /// Used in New Zealand
    /// </summary>
    public static int NZGD2000_NZTM => 2193;

    #endregion

    #region Complex Geometries

    /// <summary>
    /// Creates a large parcel polygon with 1000+ vertices
    /// Simulates complex boundary surveys or detailed property lines
    /// Based on actual parcel complexity seen in rural/agricultural areas
    /// </summary>
    public static Polygon CreateLargeParcel()
    {
        var vertexCount = 1024; // Power of 2 for testing
        var coordinates = new Coordinate[vertexCount + 1]; // +1 for closing coordinate
        var centerLon = -122.4;
        var centerLat = 45.5;
        var radius = 0.01; // Roughly 1km radius

        // Create irregular polygon with many vertices
        var random = new Random(42); // Fixed seed for reproducibility
        for (var i = 0; i < vertexCount; i++)
        {
            var angle = 2 * Math.PI * i / vertexCount;
            // Add random variation to create irregular boundary
            var radiusVariation = radius * (0.8 + 0.4 * random.NextDouble());
            var lon = centerLon + radiusVariation * Math.Cos(angle);
            var lat = centerLat + radiusVariation * Math.Sin(angle);
            coordinates[i] = new Coordinate(lon, lat);
        }
        // Close the ring
        coordinates[vertexCount] = new Coordinate(coordinates[0].X, coordinates[0].Y);

        var shell = Factory4326.CreateLinearRing(coordinates);
        return Factory4326.CreatePolygon(shell);
    }

    /// <summary>
    /// Creates a polygon with multiple holes (donut)
    /// Simulates parcels with exclusions, lakes, or protected areas
    /// </summary>
    public static Polygon CreateParcelWithMultipleHoles()
    {
        // Outer boundary
        var shell = Factory4326.CreateLinearRing(new[]
        {
            new Coordinate(-122.5, 45.5),
            new Coordinate(-122.3, 45.5),
            new Coordinate(-122.3, 45.7),
            new Coordinate(-122.5, 45.7),
            new Coordinate(-122.5, 45.5)
        });

        // Three interior holes
        var holes = new[]
        {
            // Hole 1 (northwest)
            Factory4326.CreateLinearRing(new[]
            {
                new Coordinate(-122.48, 45.65),
                new Coordinate(-122.45, 45.65),
                new Coordinate(-122.45, 45.68),
                new Coordinate(-122.48, 45.68),
                new Coordinate(-122.48, 45.65)
            }),
            // Hole 2 (northeast)
            Factory4326.CreateLinearRing(new[]
            {
                new Coordinate(-122.38, 45.65),
                new Coordinate(-122.35, 45.65),
                new Coordinate(-122.35, 45.68),
                new Coordinate(-122.38, 45.68),
                new Coordinate(-122.38, 45.65)
            }),
            // Hole 3 (center)
            Factory4326.CreateLinearRing(new[]
            {
                new Coordinate(-122.42, 45.58),
                new Coordinate(-122.38, 45.58),
                new Coordinate(-122.38, 45.62),
                new Coordinate(-122.42, 45.62),
                new Coordinate(-122.42, 45.58)
            })
        };

        return Factory4326.CreatePolygon(shell, holes);
    }

    /// <summary>
    /// Polygon that crosses the antimeridian (180° longitude)
    /// Tests proper handling of Pacific features
    /// Based on real coordinates near Fiji/Tonga
    /// </summary>
    public static Polygon CreateAntimeridianCrossingPolygon()
    {
        var shell = Factory4326.CreateLinearRing(new[]
        {
            new Coordinate(179.5, -17.0),
            new Coordinate(-179.5, -17.0), // Crosses antimeridian
            new Coordinate(-179.5, -18.0),
            new Coordinate(179.5, -18.0),
            new Coordinate(179.5, -17.0)
        });

        return Factory4326.CreatePolygon(shell);
    }

    /// <summary>
    /// LineString that crosses the antimeridian (west to east)
    /// Simulates ship routes or flight paths across Pacific
    /// </summary>
    public static LineString CreateAntimeridianCrossingLine_WestToEast()
    {
        return Factory4326.CreateLineString(new[]
        {
            new Coordinate(179.9, 0.0),
            new Coordinate(-179.9, 0.0)
        });
    }

    /// <summary>
    /// LineString that crosses the antimeridian (east to west)
    /// </summary>
    public static LineString CreateAntimeridianCrossingLine_EastToWest()
    {
        return Factory4326.CreateLineString(new[]
        {
            new Coordinate(-179.9, 0.0),
            new Coordinate(179.9, 0.0)
        });
    }

    /// <summary>
    /// Polygon around the North Pole
    /// Tests polar coordinate system handling
    /// </summary>
    public static Polygon CreateNorthPolePolygon()
    {
        // Create a valid polygon around the North Pole with varying latitudes
        // Avoid spanning the antimeridian by staying within -179 to +179
        var shell = Factory4326.CreateLinearRing(new[]
        {
            new Coordinate(-179.0, 85.0),
            new Coordinate(-90.0, 89.0),
            new Coordinate(0.0, 89.5),
            new Coordinate(90.0, 89.0),
            new Coordinate(179.0, 85.0),
            new Coordinate(0.0, 85.0),
            new Coordinate(-179.0, 85.0) // Close ring
        });

        return Factory4326.CreatePolygon(shell);
    }

    /// <summary>
    /// Polygon around the South Pole
    /// Tests Antarctic coordinate handling
    /// </summary>
    public static Polygon CreateSouthPolePolygon()
    {
        // Create a valid polygon around the South Pole with varying latitudes
        // Avoid spanning the antimeridian by staying within -179 to +179
        var shell = Factory4326.CreateLinearRing(new[]
        {
            new Coordinate(-179.0, -85.0),
            new Coordinate(-90.0, -89.0),
            new Coordinate(0.0, -89.5),
            new Coordinate(90.0, -89.0),
            new Coordinate(179.0, -85.0),
            new Coordinate(0.0, -85.0),
            new Coordinate(-179.0, -85.0) // Close ring
        });

        return Factory4326.CreatePolygon(shell);
    }

    #endregion

    #region Degenerate and Edge Case Geometries

    /// <summary>
    /// Point at "Null Island" (0,0) - common coordinate error location
    /// Where equator meets prime meridian in the Atlantic Ocean
    /// </summary>
    public static Point CreateNullIsland()
    {
        return Factory4326.CreatePoint(new Coordinate(0.0, 0.0));
    }

    /// <summary>
    /// Zero-area polygon (all points collinear)
    /// Tests handling of degenerate geometries
    /// </summary>
    public static Polygon CreateDegeneratePolygon_ZeroArea()
    {
        var shell = Factory4326.CreateLinearRing(new[]
        {
            new Coordinate(-122.4, 45.5),
            new Coordinate(-122.3, 45.5),
            new Coordinate(-122.2, 45.5),
            new Coordinate(-122.1, 45.5),
            new Coordinate(-122.4, 45.5) // All on same latitude = zero area
        });

        return Factory4326.CreatePolygon(shell);
    }

    /// <summary>
    /// Zero-length linestring (single point repeated)
    /// </summary>
    public static LineString CreateDegenerateLineString_ZeroLength()
    {
        return Factory4326.CreateLineString(new[]
        {
            new Coordinate(-122.4, 45.5),
            new Coordinate(-122.4, 45.5) // Same point twice
        });
    }

    /// <summary>
    /// Vertical line along prime meridian (0° longitude)
    /// </summary>
    public static LineString CreatePrimeMeridianLine()
    {
        return Factory4326.CreateLineString(new[]
        {
            new Coordinate(0.0, -85.0),
            new Coordinate(0.0, 85.0)
        });
    }

    /// <summary>
    /// Horizontal line along equator (0° latitude)
    /// </summary>
    public static LineString CreateEquatorLine()
    {
        return Factory4326.CreateLineString(new[]
        {
            new Coordinate(-180.0, 0.0),
            new Coordinate(180.0, 0.0)
        });
    }

    /// <summary>
    /// Point with maximum precision (15 decimal places)
    /// Tests coordinate precision handling
    /// </summary>
    public static Point CreateMaxPrecisionPoint()
    {
        return Factory4326.CreatePoint(new Coordinate(
            -122.419412345678901,
            45.523123456789012
        ));
    }

    /// <summary>
    /// Point with subnormal float values (near machine epsilon)
    /// Tests numeric edge cases
    /// </summary>
    public static Point CreateSubnormalPrecisionPoint()
    {
        return Factory4326.CreatePoint(new Coordinate(
            1.0e-10,
            1.0e-10
        ));
    }

    #endregion

    #region Feature Attributes with Special Characters and Edge Cases

    /// <summary>
    /// Feature attributes with SQL injection attempts
    /// </summary>
    public static Dictionary<string, object?> GetSqlInjectionTestAttributes()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "1' OR '1'='1",
            ["name"] = "Robert'); DROP TABLE parcels;--",
            ["address"] = "123 Main St'; DELETE FROM users WHERE 't'='t",
            ["owner"] = "Smith\" OR \"1\"=\"1"
        };
    }

    /// <summary>
    /// Feature attributes with Unicode edge cases
    /// </summary>
    public static Dictionary<string, object?> GetUnicodeEdgeCaseAttributes()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name_combining"] = "Cafe\u0301", // Café with combining acute accent
            ["name_precomposed"] = "Café", // Café with precomposed é
            ["rtl_text"] = "\u200Fالنص العربي", // Arabic with RLM marker
            ["zero_width"] = "Test\u200BString", // Zero-width space
            ["emoji"] = "Property 🏠",
            ["japanese"] = "東京都",
            ["mixed_scripts"] = "Улица Main Street 大道"
        };
    }

    /// <summary>
    /// Feature attributes with extreme numeric values
    /// </summary>
    public static Dictionary<string, object?> GetNumericEdgeCaseAttributes()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["int_max"] = int.MaxValue,
            ["int_min"] = int.MinValue,
            ["long_max"] = long.MaxValue,
            ["long_min"] = long.MinValue,
            ["double_max"] = double.MaxValue,
            ["double_min"] = double.MinValue,
            ["double_epsilon"] = double.Epsilon,
            ["double_nan"] = double.NaN,
            ["double_infinity"] = double.PositiveInfinity,
            ["decimal_max"] = decimal.MaxValue,
            ["decimal_min"] = decimal.MinValue
        };
    }

    /// <summary>
    /// Feature attributes with date/time edge cases
    /// </summary>
    public static Dictionary<string, object?> GetDateTimeEdgeCaseAttributes()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["dt_min"] = DateTime.MinValue,
            ["dt_max"] = DateTime.MaxValue,
            ["dt_unix_epoch"] = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ["dt_y2k"] = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ["dt_leap_day"] = new DateTime(2024, 2, 29, 12, 0, 0, DateTimeKind.Utc),
            ["dto_offset_pos"] = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(14)),
            ["dto_offset_neg"] = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(-12))
        };
    }

    /// <summary>
    /// Realistic parcel feature with California data
    /// </summary>
    public static Dictionary<string, object?> GetRealisticCaliforniaParcel(int index)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["apn"] = $"123-456-{index:D3}-000",
            ["owner"] = "O'Brien Family Trust",
            ["address"] = "123 José María Street, Unit #4A",
            ["city"] = "San José",
            ["zip"] = "95110",
            ["acreage"] = 0.25 + (index * 0.1),
            ["assessed_value"] = 850000 + (index * 10000),
            ["year_built"] = 1985 + index,
            ["use_code"] = "R1", // Single family residential
            ["last_sale_date"] = new DateTime(2020, 1, 1).AddDays(index),
            ["tax_rate"] = 0.0125
        };
    }

    #endregion

    #region Realistic UUIDs for Feature IDs

    /// <summary>
    /// Generate realistic UUIDs for feature IDs instead of simple integers
    /// </summary>
    public static string GenerateFeatureUuid(int seed)
    {
        // Use seed to make UUIDs deterministic for testing
        var guidBytes = new byte[16];
        var random = new Random(seed);
        random.NextBytes(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    /// <summary>
    /// Generate realistic UUID v7 (time-ordered) for feature IDs
    /// </summary>
    public static string GenerateTimeOrderedUuid(DateTime timestamp, int sequence)
    {
        // Simplified UUID v7 format for testing
        var unixMs = ((DateTimeOffset)timestamp).ToUnixTimeMilliseconds();
        return $"{unixMs:x12}-{sequence:x4}-7{sequence:x3}-{sequence:x4}-{sequence:x12}";
    }

    #endregion

    #region Realistic Field Names with Special Characters

    /// <summary>
    /// Field names that require quoting in SQL
    /// </summary>
    public static class ProblematicFieldNames
    {
        public const string WithSpace = "Property Owner";
        public const string WithHyphen = "tax-year";
        public const string WithUnderscore = "assessed_value";
        public const string WithNumber = "2024_revenue";
        public const string ReservedWord = "select"; // SQL reserved word
        public const string CaseSensitive = "FeatureID";
        public const string Unicode = "propriétaire";
        public const string WithPeriod = "owner.name";
    }

    #endregion
}
