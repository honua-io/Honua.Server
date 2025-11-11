// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.OGC;

/// <summary>
/// Information about an OGC service endpoint
/// </summary>
public class OgcServiceInfo
{
    public required string Url { get; set; }
    public required OgcServiceType ServiceType { get; set; }
    public string? Version { get; set; }
    public string? Title { get; set; }
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Type of OGC service
/// </summary>
public enum OgcServiceType
{
    WMS,
    WFS,
    WMTS,
    WCS,
    Unknown
}

/// <summary>
/// WMS GetMap request parameters
/// </summary>
public class WmsGetMapRequest
{
    public required string BaseUrl { get; set; }
    public required List<string> Layers { get; set; }
    public required string Srs { get; set; }
    public required double[] BoundingBox { get; set; } // [minx, miny, maxx, maxy]
    public required int Width { get; set; }
    public required int Height { get; set; }
    public string Format { get; set; } = "image/png";
    public string Version { get; set; } = "1.3.0";
    public List<string>? Styles { get; set; }
    public bool Transparent { get; set; } = true;
    public string? BackgroundColor { get; set; }
    public string? Time { get; set; }
    public string? Elevation { get; set; }
    public Dictionary<string, string>? CustomParameters { get; set; }

    public string BuildUrl()
    {
        var parameters = new List<string>
        {
            "SERVICE=WMS",
            $"VERSION={Version}",
            "REQUEST=GetMap",
            $"LAYERS={string.Join(",", Layers)}",
            $"STYLES={string.Join(",", Styles ?? new List<string>(new string[Layers.Count]))}",
            Version == "1.3.0" ? $"CRS={Srs}" : $"SRS={Srs}",
            $"BBOX={string.Join(",", BoundingBox)}",
            $"WIDTH={Width}",
            $"HEIGHT={Height}",
            $"FORMAT={Format}",
            $"TRANSPARENT={Transparent.ToString().ToUpper()}"
        };

        if (!string.IsNullOrEmpty(BackgroundColor))
        {
            parameters.Add($"BGCOLOR={BackgroundColor}");
        }

        if (!string.IsNullOrEmpty(Time))
        {
            parameters.Add($"TIME={Time}");
        }

        if (!string.IsNullOrEmpty(Elevation))
        {
            parameters.Add($"ELEVATION={Elevation}");
        }

        if (CustomParameters != null)
        {
            foreach (var param in CustomParameters)
            {
                parameters.Add($"{param.Key}={param.Value}");
            }
        }

        var queryString = string.Join("&", parameters);
        return BaseUrl.Contains("?")
            ? $"{BaseUrl}&{queryString}"
            : $"{BaseUrl}?{queryString}";
    }
}

/// <summary>
/// WMS GetFeatureInfo request parameters
/// </summary>
public class WmsGetFeatureInfoRequest
{
    public required string BaseUrl { get; set; }
    public required List<string> Layers { get; set; }
    public required List<string> QueryLayers { get; set; }
    public required string Srs { get; set; }
    public required double[] BoundingBox { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public required int X { get; set; } // pixel coordinate
    public required int Y { get; set; } // pixel coordinate
    public string Format { get; set; } = "image/png";
    public string InfoFormat { get; set; } = "application/json";
    public string Version { get; set; } = "1.3.0";
    public int FeatureCount { get; set; } = 10;
    public Dictionary<string, string>? CustomParameters { get; set; }

    public string BuildUrl()
    {
        var parameters = new List<string>
        {
            "SERVICE=WMS",
            $"VERSION={Version}",
            "REQUEST=GetFeatureInfo",
            $"LAYERS={string.Join(",", Layers)}",
            $"QUERY_LAYERS={string.Join(",", QueryLayers)}",
            Version == "1.3.0" ? $"CRS={Srs}" : $"SRS={Srs}",
            $"BBOX={string.Join(",", BoundingBox)}",
            $"WIDTH={Width}",
            $"HEIGHT={Height}",
            $"FORMAT={Format}",
            $"INFO_FORMAT={InfoFormat}",
            Version == "1.3.0" ? $"I={X}" : $"X={X}",
            Version == "1.3.0" ? $"J={Y}" : $"Y={Y}",
            $"FEATURE_COUNT={FeatureCount}"
        };

        if (CustomParameters != null)
        {
            foreach (var param in CustomParameters)
            {
                parameters.Add($"{param.Key}={param.Value}");
            }
        }

        var queryString = string.Join("&", parameters);
        return BaseUrl.Contains("?")
            ? $"{BaseUrl}&{queryString}"
            : $"{BaseUrl}?{queryString}";
    }
}

/// <summary>
/// WMS GetLegendGraphic request
/// </summary>
public class WmsGetLegendGraphicRequest
{
    public required string BaseUrl { get; set; }
    public required string Layer { get; set; }
    public string Format { get; set; } = "image/png";
    public string Version { get; set; } = "1.3.0";
    public string? Style { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public Dictionary<string, string>? CustomParameters { get; set; }

    public string BuildUrl()
    {
        var parameters = new List<string>
        {
            "SERVICE=WMS",
            $"VERSION={Version}",
            "REQUEST=GetLegendGraphic",
            $"LAYER={Layer}",
            $"FORMAT={Format}"
        };

        if (!string.IsNullOrEmpty(Style))
        {
            parameters.Add($"STYLE={Style}");
        }

        if (Width.HasValue)
        {
            parameters.Add($"WIDTH={Width.Value}");
        }

        if (Height.HasValue)
        {
            parameters.Add($"HEIGHT={Height.Value}");
        }

        if (CustomParameters != null)
        {
            foreach (var param in CustomParameters)
            {
                parameters.Add($"{param.Key}={param.Value}");
            }
        }

        var queryString = string.Join("&", parameters);
        return BaseUrl.Contains("?")
            ? $"{BaseUrl}&{queryString}"
            : $"{BaseUrl}?{queryString}";
    }
}
