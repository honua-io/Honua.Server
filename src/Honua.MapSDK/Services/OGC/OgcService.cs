// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Text.Json;
using Honua.MapSDK.Models.OGC;

namespace Honua.MapSDK.Services.OGC;

/// <summary>
/// Service for interacting with OGC web services (WMS, WFS, etc.)
/// </summary>
public class OgcService
{
    private readonly HttpClient _httpClient;
    private readonly WmsCapabilitiesParser _wmsParser;
    private readonly WfsCapabilitiesParser _wfsParser;

    public OgcService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _wmsParser = new WmsCapabilitiesParser();
        _wfsParser = new WfsCapabilitiesParser();
    }

    /// <summary>
    /// Detect the type of OGC service from a URL
    /// </summary>
    public async Task<OgcServiceInfo> DetectServiceAsync(string url)
    {
        try
        {
            // Try WMS first
            var wmsCapabilities = await GetWmsCapabilitiesAsync(url);
            if (wmsCapabilities != null)
            {
                return new OgcServiceInfo
                {
                    Url = url,
                    ServiceType = OgcServiceType.WMS,
                    Version = wmsCapabilities.Version,
                    Title = wmsCapabilities.Service.Title,
                    Abstract = wmsCapabilities.Service.Abstract,
                    Keywords = wmsCapabilities.Service.Keywords,
                    IsValid = true
                };
            }
        }
        catch
        {
            // Continue to try WFS
        }

        try
        {
            // Try WFS
            var wfsCapabilities = await GetWfsCapabilitiesAsync(url);
            if (wfsCapabilities != null)
            {
                return new OgcServiceInfo
                {
                    Url = url,
                    ServiceType = OgcServiceType.WFS,
                    Version = wfsCapabilities.Version,
                    Title = wfsCapabilities.Service.Title,
                    Abstract = wfsCapabilities.Service.Abstract,
                    Keywords = wfsCapabilities.Service.Keywords,
                    IsValid = true
                };
            }
        }
        catch
        {
            // Service detection failed
        }

        return new OgcServiceInfo
        {
            Url = url,
            ServiceType = OgcServiceType.Unknown,
            IsValid = false,
            ErrorMessage = "Could not detect OGC service type. URL may not be a valid WMS or WFS endpoint."
        };
    }

    /// <summary>
    /// Get WMS GetCapabilities
    /// </summary>
    public async Task<WmsCapabilities?> GetWmsCapabilitiesAsync(string baseUrl, string version = "1.3.0")
    {
        var url = BuildGetCapabilitiesUrl(baseUrl, "WMS", version);
        var xml = await _httpClient.GetStringAsync(url);
        return _wmsParser.Parse(xml);
    }

    /// <summary>
    /// Get WFS GetCapabilities
    /// </summary>
    public async Task<WfsCapabilities?> GetWfsCapabilitiesAsync(string baseUrl, string version = "2.0.0")
    {
        var url = BuildGetCapabilitiesUrl(baseUrl, "WFS", version);
        var xml = await _httpClient.GetStringAsync(url);
        return _wfsParser.Parse(xml);
    }

    /// <summary>
    /// Execute WMS GetFeatureInfo request
    /// </summary>
    public async Task<string> GetFeatureInfoAsync(WmsGetFeatureInfoRequest request)
    {
        var url = request.BuildUrl();
        return await _httpClient.GetStringAsync(url);
    }

    /// <summary>
    /// Execute WFS GetFeature request and return GeoJSON
    /// </summary>
    public async Task<WfsFeatureCollection?> GetFeaturesAsync(string baseUrl, WfsQueryOptions options)
    {
        var url = BuildGetFeatureUrl(baseUrl, options);

        try
        {
            var json = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<WfsFeatureCollection>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get features from WFS: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get legend graphic URL for a WMS layer
    /// </summary>
    public string GetLegendGraphicUrl(WmsGetLegendGraphicRequest request)
    {
        return request.BuildUrl();
    }

    /// <summary>
    /// Build GetCapabilities URL
    /// </summary>
    private string BuildGetCapabilitiesUrl(string baseUrl, string service, string version)
    {
        var separator = baseUrl.Contains("?") ? "&" : "?";
        return $"{baseUrl}{separator}SERVICE={service}&VERSION={version}&REQUEST=GetCapabilities";
    }

    /// <summary>
    /// Build WFS GetFeature URL
    /// </summary>
    private string BuildGetFeatureUrl(string baseUrl, WfsQueryOptions options)
    {
        var parameters = new List<string>
        {
            "SERVICE=WFS",
            $"VERSION={options.Version}",
            "REQUEST=GetFeature",
            $"TYPENAME={options.FeatureType}",
            $"OUTPUTFORMAT={options.OutputFormat}"
        };

        if (options.Srs != null)
        {
            parameters.Add(options.Version.StartsWith("2") ? $"SRSNAME={options.Srs}" : $"SRS={options.Srs}");
        }

        if (options.MaxFeatures.HasValue)
        {
            parameters.Add(options.Version.StartsWith("2") ? $"COUNT={options.MaxFeatures.Value}" : $"MAXFEATURES={options.MaxFeatures.Value}");
        }

        if (options.StartIndex.HasValue)
        {
            parameters.Add($"STARTINDEX={options.StartIndex.Value}");
        }

        if (options.PropertyNames != null && options.PropertyNames.Count > 0)
        {
            parameters.Add($"PROPERTYNAME={string.Join(",", options.PropertyNames)}");
        }

        if (options.BoundingBox != null && options.BoundingBox.Length == 4)
        {
            var bbox = string.Join(",", options.BoundingBox);
            if (!string.IsNullOrEmpty(options.Srs))
            {
                parameters.Add($"BBOX={bbox},{options.Srs}");
            }
            else
            {
                parameters.Add($"BBOX={bbox}");
            }
        }

        if (!string.IsNullOrEmpty(options.CqlFilter))
        {
            parameters.Add($"CQL_FILTER={Uri.EscapeDataString(options.CqlFilter)}");
        }

        if (!string.IsNullOrEmpty(options.Filter))
        {
            parameters.Add($"FILTER={Uri.EscapeDataString(options.Filter)}");
        }

        if (!string.IsNullOrEmpty(options.SortBy))
        {
            parameters.Add($"SORTBY={options.SortBy}");
        }

        var queryString = string.Join("&", parameters);
        return baseUrl.Contains("?")
            ? $"{baseUrl}&{queryString}"
            : $"{baseUrl}?{queryString}";
    }

    /// <summary>
    /// Test connection to OGC service
    /// </summary>
    public async Task<bool> TestConnectionAsync(string url)
    {
        try
        {
            var serviceInfo = await DetectServiceAsync(url);
            return serviceInfo.IsValid;
        }
        catch
        {
            return false;
        }
    }
}
