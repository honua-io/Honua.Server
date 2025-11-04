using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Data;

namespace Honua.Benchmarks;

/// <summary>
/// Benchmarks for CRS (Coordinate Reference System) operations including parsing, normalization, and identifier conversion.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class CrsTransformationBenchmarks
{
    private string[] _commonCrsFormats = null!;
    private string[] _epsgFormats = null!;
    private string[] _ogcFormats = null!;
    private string[] _numericFormats = null!;
    private string[] _fullUriFormats = null!;
    private string[] _mixedCaseFormats = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Common CRS formats used in real-world scenarios
        _commonCrsFormats = new[]
        {
            "EPSG:4326",
            "EPSG:3857",
            "CRS84",
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "http://www.opengis.net/def/crs/EPSG/0/3857"
        };

        // EPSG-specific formats
        _epsgFormats = new[]
        {
            "EPSG:4326",
            "EPSG:3857",
            "EPSG:32610",
            "EPSG:32611",
            "EPSG:2154",
            "EPSG:27700",
            "EPSG:3395",
            "EPSG:4269"
        };

        // OGC formats
        _ogcFormats = new[]
        {
            "CRS84",
            "OGC:CRS84",
            "CRS84H",
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "http://www.opengis.net/def/crs/OGC/0/CRS84h"
        };

        // Numeric formats
        _numericFormats = new[]
        {
            "4326",
            "3857",
            "32610",
            "32611",
            "2154",
            "27700",
            "84"
        };

        // Full URI formats
        _fullUriFormats = new[]
        {
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "http://www.opengis.net/def/crs/EPSG/0/3857",
            "http://www.opengis.net/def/crs/EPSG/0/32610",
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "http://www.opengis.net/def/crs/EPSG/0/4269"
        };

        // Mixed case formats (testing case-insensitive handling)
        _mixedCaseFormats = new[]
        {
            "epsg:4326",
            "EPSG:4326",
            "Epsg:4326",
            "crs84",
            "CRS84",
            "Crs84"
        };
    }

    #region CRS Normalization Benchmarks - Common Formats

    [Benchmark]
    public string Normalize_Epsg4326()
    {
        return CrsHelper.NormalizeIdentifier("EPSG:4326");
    }

    [Benchmark]
    public string Normalize_Epsg3857()
    {
        return CrsHelper.NormalizeIdentifier("EPSG:3857");
    }

    [Benchmark]
    public string Normalize_Crs84()
    {
        return CrsHelper.NormalizeIdentifier("CRS84");
    }

    [Benchmark]
    public string Normalize_Crs84H()
    {
        return CrsHelper.NormalizeIdentifier("CRS84H");
    }

    [Benchmark]
    public string Normalize_OgcCrs84()
    {
        return CrsHelper.NormalizeIdentifier("OGC:CRS84");
    }

    [Benchmark]
    public string Normalize_Numeric4326()
    {
        return CrsHelper.NormalizeIdentifier("4326");
    }

    [Benchmark]
    public string Normalize_Numeric3857()
    {
        return CrsHelper.NormalizeIdentifier("3857");
    }

    [Benchmark]
    public string Normalize_FullUri4326()
    {
        return CrsHelper.NormalizeIdentifier("http://www.opengis.net/def/crs/EPSG/0/4326");
    }

    [Benchmark]
    public string Normalize_FullUri3857()
    {
        return CrsHelper.NormalizeIdentifier("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Benchmark]
    public string Normalize_FullUriCrs84()
    {
        return CrsHelper.NormalizeIdentifier("http://www.opengis.net/def/crs/OGC/1.3/CRS84");
    }

    #endregion

    #region CRS Normalization Benchmarks - Edge Cases

    [Benchmark]
    public string Normalize_Null()
    {
        return CrsHelper.NormalizeIdentifier(null);
    }

    [Benchmark]
    public string Normalize_EmptyString()
    {
        return CrsHelper.NormalizeIdentifier("");
    }

    [Benchmark]
    public string Normalize_Whitespace()
    {
        return CrsHelper.NormalizeIdentifier("   ");
    }

    [Benchmark]
    public string Normalize_WithWhitespace()
    {
        return CrsHelper.NormalizeIdentifier("  EPSG:4326  ");
    }

    #endregion

    #region CRS Parsing Benchmarks - Common Formats

    [Benchmark]
    public int Parse_Epsg4326()
    {
        return CrsHelper.ParseCrs("EPSG:4326");
    }

    [Benchmark]
    public int Parse_Epsg3857()
    {
        return CrsHelper.ParseCrs("EPSG:3857");
    }

    [Benchmark]
    public int Parse_Crs84()
    {
        return CrsHelper.ParseCrs("CRS84");
    }

    [Benchmark]
    public int Parse_Numeric4326()
    {
        return CrsHelper.ParseCrs("4326");
    }

    [Benchmark]
    public int Parse_Numeric3857()
    {
        return CrsHelper.ParseCrs("3857");
    }

    [Benchmark]
    public int Parse_Numeric84()
    {
        return CrsHelper.ParseCrs("84");
    }

    [Benchmark]
    public int Parse_FullUri4326()
    {
        return CrsHelper.ParseCrs("http://www.opengis.net/def/crs/EPSG/0/4326");
    }

    [Benchmark]
    public int Parse_FullUri3857()
    {
        return CrsHelper.ParseCrs("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Benchmark]
    public int Parse_FullUriCrs84()
    {
        return CrsHelper.ParseCrs("http://www.opengis.net/def/crs/OGC/1.3/CRS84");
    }

    [Benchmark]
    public int Parse_Crs84H()
    {
        return CrsHelper.ParseCrs("CRS84H");
    }

    #endregion

    #region CRS Parsing Benchmarks - Edge Cases

    [Benchmark]
    public int Parse_Null()
    {
        return CrsHelper.ParseCrs(null);
    }

    [Benchmark]
    public int Parse_EmptyString()
    {
        return CrsHelper.ParseCrs("");
    }

    [Benchmark]
    public int Parse_InvalidFormat()
    {
        return CrsHelper.ParseCrs("invalid-crs-format");
    }

    #endregion

    #region Batch Processing Benchmarks

    [Benchmark]
    public List<string> NormalizeBatch_CommonFormats()
    {
        var results = new List<string>(_commonCrsFormats.Length);
        foreach (var crs in _commonCrsFormats)
        {
            results.Add(CrsHelper.NormalizeIdentifier(crs));
        }
        return results;
    }

    [Benchmark]
    public List<string> NormalizeBatch_EpsgFormats()
    {
        var results = new List<string>(_epsgFormats.Length);
        foreach (var crs in _epsgFormats)
        {
            results.Add(CrsHelper.NormalizeIdentifier(crs));
        }
        return results;
    }

    [Benchmark]
    public List<string> NormalizeBatch_OgcFormats()
    {
        var results = new List<string>(_ogcFormats.Length);
        foreach (var crs in _ogcFormats)
        {
            results.Add(CrsHelper.NormalizeIdentifier(crs));
        }
        return results;
    }

    [Benchmark]
    public List<int> ParseBatch_CommonFormats()
    {
        var results = new List<int>(_commonCrsFormats.Length);
        foreach (var crs in _commonCrsFormats)
        {
            results.Add(CrsHelper.ParseCrs(crs));
        }
        return results;
    }

    [Benchmark]
    public List<int> ParseBatch_EpsgFormats()
    {
        var results = new List<int>(_epsgFormats.Length);
        foreach (var crs in _epsgFormats)
        {
            results.Add(CrsHelper.ParseCrs(crs));
        }
        return results;
    }

    [Benchmark]
    public List<int> ParseBatch_NumericFormats()
    {
        var results = new List<int>(_numericFormats.Length);
        foreach (var crs in _numericFormats)
        {
            results.Add(CrsHelper.ParseCrs(crs));
        }
        return results;
    }

    #endregion

    #region Mixed Case Handling Benchmarks

    [Benchmark]
    public List<string> NormalizeBatch_MixedCase()
    {
        var results = new List<string>(_mixedCaseFormats.Length);
        foreach (var crs in _mixedCaseFormats)
        {
            results.Add(CrsHelper.NormalizeIdentifier(crs));
        }
        return results;
    }

    [Benchmark]
    public List<int> ParseBatch_MixedCase()
    {
        var results = new List<int>(_mixedCaseFormats.Length);
        foreach (var crs in _mixedCaseFormats)
        {
            results.Add(CrsHelper.ParseCrs(crs));
        }
        return results;
    }

    #endregion

    #region Real-World Scenario Benchmarks

    [Benchmark]
    public (string normalized, int parsed) RoundTrip_Epsg4326()
    {
        var normalized = CrsHelper.NormalizeIdentifier("EPSG:4326");
        var parsed = CrsHelper.ParseCrs(normalized);
        return (normalized, parsed);
    }

    [Benchmark]
    public (string normalized, int parsed) RoundTrip_Crs84()
    {
        var normalized = CrsHelper.NormalizeIdentifier("CRS84");
        var parsed = CrsHelper.ParseCrs(normalized);
        return (normalized, parsed);
    }

    [Benchmark]
    public List<(string normalized, int parsed)> RoundTripBatch_100Items()
    {
        var results = new List<(string, int)>(100);
        for (int i = 0; i < 100; i++)
        {
            var crs = _commonCrsFormats[i % _commonCrsFormats.Length];
            var normalized = CrsHelper.NormalizeIdentifier(crs);
            var parsed = CrsHelper.ParseCrs(normalized);
            results.Add((normalized, parsed));
        }
        return results;
    }

    #endregion

    [GlobalCleanup]
    public void Cleanup()
    {
        // Cleanup resources if needed
    }
}
