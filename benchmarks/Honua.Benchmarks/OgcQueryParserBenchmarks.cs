using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Data;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Honua.Benchmarks;

/// <summary>
/// Benchmarks for OGC query parameter parsing including format negotiation, CRS parsing, and Accept-CRS header processing.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class OgcQueryParserBenchmarks
{
    private DefaultHttpContext _httpContext = null!;
    private DefaultHttpContext _httpContextWithAcceptHeader = null!;
    private DefaultHttpContext _httpContextWithComplexAccept = null!;
    private DefaultHttpContext _httpContextWithAcceptCrs = null!;
    private DefaultHttpContext _httpContextWithComplexAcceptCrs = null!;
    private List<string> _supportedCrsList = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup basic HTTP context
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.Host = new HostString("example.com");
        _httpContext.Request.Path = "/collections/test";

        // Setup HTTP context with Accept header
        _httpContextWithAcceptHeader = new DefaultHttpContext();
        _httpContextWithAcceptHeader.Request.Scheme = "https";
        _httpContextWithAcceptHeader.Request.Host = new HostString("example.com");
        _httpContextWithAcceptHeader.Request.Path = "/collections/test";
        _httpContextWithAcceptHeader.Request.Headers.Accept = "application/geo+json";

        // Setup HTTP context with complex Accept header (quality values)
        _httpContextWithComplexAccept = new DefaultHttpContext();
        _httpContextWithComplexAccept.Request.Scheme = "https";
        _httpContextWithComplexAccept.Request.Host = new HostString("example.com");
        _httpContextWithComplexAccept.Request.Path = "/collections/test";
        _httpContextWithComplexAccept.Request.Headers.Accept = "text/html;q=0.9, application/geo+json;q=1.0, application/json;q=0.8";

        // Setup HTTP context with Accept-Crs header
        _httpContextWithAcceptCrs = new DefaultHttpContext();
        _httpContextWithAcceptCrs.Request.Scheme = "https";
        _httpContextWithAcceptCrs.Request.Host = new HostString("example.com");
        _httpContextWithAcceptCrs.Request.Path = "/collections/test";
        _httpContextWithAcceptCrs.Request.Headers["Accept-Crs"] = "http://www.opengis.net/def/crs/EPSG/0/4326";

        // Setup HTTP context with complex Accept-Crs header (multiple CRS with quality values)
        _httpContextWithComplexAcceptCrs = new DefaultHttpContext();
        _httpContextWithComplexAcceptCrs.Request.Scheme = "https";
        _httpContextWithComplexAcceptCrs.Request.Host = new HostString("example.com");
        _httpContextWithComplexAcceptCrs.Request.Path = "/collections/test";
        _httpContextWithComplexAcceptCrs.Request.Headers["Accept-Crs"] = "http://www.opengis.net/def/crs/EPSG/0/3857;q=0.9, http://www.opengis.net/def/crs/EPSG/0/4326;q=1.0";

        // Setup supported CRS list
        _supportedCrsList = new List<string>
        {
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "http://www.opengis.net/def/crs/EPSG/0/3857",
            "http://www.opengis.net/def/crs/EPSG/0/32610"
        };
    }

    #region Format Parsing Benchmarks

    [Benchmark]
    public object ParseFormat_GeoJson()
    {
        return OgcQueryParser.ParseFormat("geojson");
    }

    [Benchmark]
    public object ParseFormat_Json()
    {
        return OgcQueryParser.ParseFormat("json");
    }

    [Benchmark]
    public object ParseFormat_Html()
    {
        return OgcQueryParser.ParseFormat("html");
    }

    [Benchmark]
    public object ParseFormat_FullMimeType()
    {
        return OgcQueryParser.ParseFormat("application/vnd.google-earth.kml+xml");
    }

    [Benchmark]
    public object ParseFormat_Invalid()
    {
        return OgcQueryParser.ParseFormat("invalid-format");
    }

    [Benchmark]
    public object ParseFormat_Null()
    {
        return OgcQueryParser.ParseFormat(null);
    }

    #endregion

    #region Media Type Mapping Benchmarks

    [Benchmark]
    public bool TryMapMediaType_GeoJson()
    {
        return OgcQueryParser.TryMapMediaType("application/geo+json", out _);
    }

    [Benchmark]
    public bool TryMapMediaType_Json()
    {
        return OgcQueryParser.TryMapMediaType("application/json", out _);
    }

    [Benchmark]
    public bool TryMapMediaType_Html()
    {
        return OgcQueryParser.TryMapMediaType("text/html", out _);
    }

    [Benchmark]
    public bool TryMapMediaType_Kml()
    {
        return OgcQueryParser.TryMapMediaType("application/vnd.google-earth.kml+xml", out _);
    }

    [Benchmark]
    public bool TryMapMediaType_Invalid()
    {
        return OgcQueryParser.TryMapMediaType("invalid/type", out _);
    }

    #endregion

    #region Response Format Resolution Benchmarks

    [Benchmark]
    public object ResolveResponseFormat_NoParameters()
    {
        return OgcQueryParser.ResolveResponseFormat(_httpContext.Request);
    }

    [Benchmark]
    public object ResolveResponseFormat_WithQueryParameter()
    {
        _httpContext.Request.QueryString = new QueryString("?f=json");
        var result = OgcQueryParser.ResolveResponseFormat(_httpContext.Request);
        _httpContext.Request.QueryString = QueryString.Empty;
        return result;
    }

    [Benchmark]
    public object ResolveResponseFormat_WithAcceptHeader()
    {
        return OgcQueryParser.ResolveResponseFormat(_httpContextWithAcceptHeader.Request);
    }

    [Benchmark]
    public object ResolveResponseFormat_WithComplexAcceptHeader()
    {
        return OgcQueryParser.ResolveResponseFormat(_httpContextWithComplexAccept.Request);
    }

    #endregion

    #region Accept-Crs Resolution Benchmarks

    [Benchmark]
    public object ResolveAcceptCrs_NoHeader()
    {
        return OgcQueryParser.ResolveAcceptCrs(_httpContext.Request, _supportedCrsList);
    }

    [Benchmark]
    public object ResolveAcceptCrs_SimpleHeader()
    {
        return OgcQueryParser.ResolveAcceptCrs(_httpContextWithAcceptCrs.Request, _supportedCrsList);
    }

    [Benchmark]
    public object ResolveAcceptCrs_ComplexHeader()
    {
        return OgcQueryParser.ResolveAcceptCrs(_httpContextWithComplexAcceptCrs.Request, _supportedCrsList);
    }

    [Benchmark]
    public object ResolveAcceptCrs_UnsupportedCrs()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Crs"] = "http://www.opengis.net/def/crs/EPSG/0/99999";
        return OgcQueryParser.ResolveAcceptCrs(context.Request, _supportedCrsList);
    }

    #endregion

    #region CRS Helper Benchmarks

    [Benchmark]
    public string CrsNormalize_Epsg4326()
    {
        return CrsHelper.NormalizeIdentifier("EPSG:4326");
    }

    [Benchmark]
    public string CrsNormalize_Crs84()
    {
        return CrsHelper.NormalizeIdentifier("CRS84");
    }

    [Benchmark]
    public string CrsNormalize_FullUri()
    {
        return CrsHelper.NormalizeIdentifier("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Benchmark]
    public string CrsNormalize_Numeric()
    {
        return CrsHelper.NormalizeIdentifier("4326");
    }

    [Benchmark]
    public string CrsNormalize_Null()
    {
        return CrsHelper.NormalizeIdentifier(null);
    }

    [Benchmark]
    public int CrsParse_Epsg4326()
    {
        return CrsHelper.ParseCrs("EPSG:4326");
    }

    [Benchmark]
    public int CrsParse_Crs84()
    {
        return CrsHelper.ParseCrs("CRS84");
    }

    [Benchmark]
    public int CrsParse_FullUri()
    {
        return CrsHelper.ParseCrs("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Benchmark]
    public int CrsParse_Numeric()
    {
        return CrsHelper.ParseCrs("3857");
    }

    [Benchmark]
    public int CrsParse_Null()
    {
        return CrsHelper.ParseCrs(null);
    }

    #endregion

    [GlobalCleanup]
    public void Cleanup()
    {
        // Cleanup resources if needed
    }
}
