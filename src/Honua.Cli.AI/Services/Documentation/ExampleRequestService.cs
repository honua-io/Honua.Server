// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Documentation;

/// <summary>
/// Service for generating example API requests in multiple formats.
/// </summary>
public sealed class ExampleRequestService
{
    /// <summary>
    /// Generates example API requests in curl, HTTP, and various programming languages.
    /// </summary>
    /// <param name="endpoints">List of endpoints as JSON array</param>
    /// <returns>JSON containing examples in multiple formats</returns>
    public string GenerateExampleRequests(string endpoints = "[\"/\",\"/conformance\",\"/collections\"]")
    {
        var examples = new
        {
            curlExamples = new[]
            {
                new
                {
                    description = "Get landing page",
                    curl = "curl -X GET https://api.example.com/",
                    response = "Links to API resources"
                },
                new
                {
                    description = "List all collections",
                    curl = "curl -X GET https://api.example.com/collections",
                    response = "Array of collection metadata"
                },
                new
                {
                    description = "Get features with bounding box",
                    curl = "curl -X GET 'https://api.example.com/collections/buildings/items?bbox=-122.5,37.7,-122.3,37.9&limit=100'",
                    response = "GeoJSON FeatureCollection"
                },
                new
                {
                    description = "Get features with CQL filter",
                    curl = "curl -X GET 'https://api.example.com/collections/buildings/items?filter=height>100&limit=50'",
                    response = "Filtered features"
                },
                new
                {
                    description = "Request specific CRS",
                    curl = "curl -X GET -H 'Accept-Crs: http://www.opengis.net/def/crs/EPSG/0/3857' 'https://api.example.com/collections/buildings/items'",
                    response = "Features in Web Mercator projection"
                }
            },
            httpExamples = new[]
            {
                new
                {
                    description = "GET Features",
                    request = @"GET /collections/buildings/items?limit=10 HTTP/1.1
Host: api.example.com
Accept: application/geo+json",
                    response = @"HTTP/1.1 200 OK
Content-Type: application/geo+json
Cache-Control: public, max-age=3600

{
  ""type"": ""FeatureCollection"",
  ""features"": [...]
}"
                },
                new
                {
                    description = "POST Query (CQL-JSON)",
                    request = @"POST /collections/buildings/items HTTP/1.1
Host: api.example.com
Content-Type: application/json

{
  ""filter"": {
    ""op"": ""and"",
    ""args"": [
      {""op"": "">"", ""args"": [{""property"": ""height""}, 100]},
      {""op"": ""="", ""args"": [{""property"": ""city""}, ""San Francisco""]}
    ]
  },
  ""limit"": 50
}",
                    response = @"HTTP/1.1 200 OK
Content-Type: application/geo+json

{""type"": ""FeatureCollection"", ""features"": [...]}"
                }
            },
            codeExamples = new
            {
                javascript = @"// Using fetch API
async function fetchFeatures(collectionId, options = {}) {
    const params = new URLSearchParams({
        limit: options.limit || 100,
        ...(options.bbox && { bbox: options.bbox }),
        ...(options.filter && { filter: options.filter })
    });

    const response = await fetch(
        `https://api.example.com/collections/${collectionId}/items?${params}`
    );

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    return await response.json();
}

// Usage
const buildings = await fetchFeatures('buildings', {
    bbox: '-122.5,37.7,-122.3,37.9',
    filter: 'height>100',
    limit: 50
});",

                python = @"import requests
from typing import Optional, Dict, Any

def fetch_features(
    collection_id: str,
    bbox: Optional[str] = None,
    filter_expr: Optional[str] = None,
    limit: int = 100,
    offset: int = 0
) -> Dict[str, Any]:
    """"""Fetch features from OGC API Features endpoint""""""

    base_url = 'https://api.example.com'
    params = {
        'limit': limit,
        'offset': offset
    }

    if bbox:
        params['bbox'] = bbox
    if filter_expr:
        params['filter'] = filter_expr

    response = requests.get(
        f'{base_url}/collections/{collection_id}/items',
        params=params,
        headers={'Accept': 'application/geo+json'}
    )

    response.raise_for_status()
    return response.json()

# Usage
buildings = fetch_features(
    'buildings',
    bbox='-122.5,37.7,-122.3,37.9',
    filter_expr='height>100',
    limit=50
)",

                csharp = @"using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

public class OgcApiClient
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OgcApiClient(string baseUrl, IHttpClientFactory? httpClientFactory = null)
    {
        if (httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient(""OgcApiClient"");
            _httpClient.BaseAddress = new Uri(baseUrl);
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
            _ownsHttpClient = true;
        }
    }

    public async Task<FeatureCollection> GetFeaturesAsync(
        string collectionId,
        string bbox = null,
        string filter = null,
        int limit = 100)
    {
        var query = $""?limit={limit}"";
        if (!bbox.IsNullOrEmpty())
            query += $""&bbox={bbox}"";
        if (!filter.IsNullOrEmpty())
            query += $""&filter={filter}"";

        var response = await _httpClient.GetAsync(
            $""/collections/{collectionId}/items{query}""
        );

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<FeatureCollection>(content);
    }
}

// Usage
var client = new OgcApiClient(""https://api.example.com"");
var buildings = await client.GetFeaturesAsync(
    ""buildings"",
    bbox: ""-122.5,37.7,-122.3,37.9"",
    filter: ""height>100"",
    limit: 50
);"
            }
        };

        return JsonSerializer.Serialize(examples, CliJsonOptions.Indented);
    }
}
