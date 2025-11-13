// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.MapSDK.Models.Esri;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;

namespace Honua.MapSDK.Services.Esri;

/// <summary>
/// Client for interacting with ArcGIS FeatureServer and MapServer REST APIs
/// Supports query, edit, attachment, and relationship operations
/// </summary>
public class EsriFeatureServerClient
{
    private readonly HttpClient _httpClient;
    private readonly EsriAuthenticationService? _authService;
    private string? _token;

    public EsriFeatureServerClient(HttpClient httpClient, EsriAuthenticationService? authService = null)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    /// <summary>
    /// Set authentication token
    /// </summary>
    public void SetToken(string token)
    {
        _token = token;
    }

    /// <summary>
    /// Get service metadata
    /// </summary>
    /// <param name="url">FeatureServer or MapServer URL</param>
    public async Task<EsriServiceInfo> GetServiceInfoAsync(string url)
    {
        var serviceUrl = BuildUrl(url, new Dictionary<string, string> { ["f"] = "json" });
        var json = await _httpClient.GetStringAsync(serviceUrl);

        var serviceInfo = JsonSerializer.Deserialize<EsriServiceInfo>(json, GetJsonOptions());
        if (serviceInfo == null)
        {
            throw new InvalidOperationException("Failed to deserialize service info");
        }

        return serviceInfo;
    }

    /// <summary>
    /// Get layer metadata
    /// </summary>
    /// <param name="url">Layer URL (e.g., .../FeatureServer/0)</param>
    public async Task<EsriLayerMetadata> GetLayerMetadataAsync(string url)
    {
        var layerUrl = BuildUrl(url, new Dictionary<string, string> { ["f"] = "json" });
        var json = await _httpClient.GetStringAsync(layerUrl);

        var metadata = JsonSerializer.Deserialize<EsriLayerMetadata>(json, GetJsonOptions());
        if (metadata == null)
        {
            throw new InvalidOperationException("Failed to deserialize layer metadata");
        }

        return metadata;
    }

    /// <summary>
    /// Query features from a layer
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="parameters">Query parameters</param>
    public async Task<EsriFeatureSet> QueryAsync(string url, EsriQueryParameters parameters)
    {
        var queryUrl = BuildUrl($"{url}/query", parameters.ToQueryParameters());
        var json = await _httpClient.GetStringAsync(queryUrl);

        // Check for errors
        CheckForError(json);

        var featureSet = JsonSerializer.Deserialize<EsriFeatureSet>(json, GetJsonOptions());
        if (featureSet == null)
        {
            throw new InvalidOperationException("Failed to deserialize feature set");
        }

        return featureSet;
    }

    /// <summary>
    /// Query features and convert to GeoJSON
    /// </summary>
    public async Task<FeatureCollection> QueryAsGeoJsonAsync(string url, EsriQueryParameters parameters)
    {
        var featureSet = await QueryAsync(url, parameters);
        return ConvertToGeoJson(featureSet);
    }

    /// <summary>
    /// Add features to a layer
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="features">Features to add</param>
    public async Task<EsriApplyEditsResult> AddFeaturesAsync(string url, EsriFeature[] features)
    {
        return await ApplyEditsAsync(url, addFeatures: features);
    }

    /// <summary>
    /// Update features in a layer
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="features">Features to update (must include ObjectID)</param>
    public async Task<EsriApplyEditsResult> UpdateFeaturesAsync(string url, EsriFeature[] features)
    {
        return await ApplyEditsAsync(url, updateFeatures: features);
    }

    /// <summary>
    /// Delete features from a layer
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="objectIds">Object IDs to delete</param>
    public async Task<EsriApplyEditsResult> DeleteFeaturesAsync(string url, int[] objectIds)
    {
        var deleteSpec = string.Join(",", objectIds);
        return await ApplyEditsAsync(url, deletes: deleteSpec);
    }

    /// <summary>
    /// Apply edits (add, update, delete) in a single transaction
    /// </summary>
    private async Task<EsriApplyEditsResult> ApplyEditsAsync(
        string url,
        EsriFeature[]? addFeatures = null,
        EsriFeature[]? updateFeatures = null,
        string? deletes = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["f"] = "json"
        };

        if (addFeatures != null && addFeatures.Length > 0)
        {
            parameters["adds"] = JsonSerializer.Serialize(addFeatures, GetJsonOptions());
        }

        if (updateFeatures != null && updateFeatures.Length > 0)
        {
            parameters["updates"] = JsonSerializer.Serialize(updateFeatures, GetJsonOptions());
        }

        if (!string.IsNullOrEmpty(deletes))
        {
            parameters["deletes"] = deletes;
        }

        var applyEditsUrl = BuildUrl($"{url}/applyEdits", parameters);
        var content = new FormUrlEncodedContent(parameters);
        var response = await _httpClient.PostAsync($"{url}/applyEdits", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        CheckForError(json);

        var result = JsonSerializer.Deserialize<EsriApplyEditsResult>(json, GetJsonOptions());
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize apply edits result");
        }

        return result;
    }

    /// <summary>
    /// Get attachments for a feature
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="objectId">Object ID of the feature</param>
    public async Task<EsriAttachmentInfo[]> GetAttachmentsAsync(string url, int objectId)
    {
        var attachmentsUrl = BuildUrl(
            $"{url}/{objectId}/attachments",
            new Dictionary<string, string> { ["f"] = "json" }
        );

        var json = await _httpClient.GetStringAsync(attachmentsUrl);
        CheckForError(json);

        var result = JsonSerializer.Deserialize<EsriAttachmentInfos>(json, GetJsonOptions());
        return result?.AttachmentInfos.ToArray() ?? Array.Empty<EsriAttachmentInfo>();
    }

    /// <summary>
    /// Add an attachment to a feature
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="objectId">Object ID of the feature</param>
    /// <param name="fileName">File name</param>
    /// <param name="fileData">File data</param>
    /// <param name="contentType">Content type (MIME type)</param>
    public async Task<EsriAddAttachmentResult> AddAttachmentAsync(
        string url,
        int objectId,
        string fileName,
        byte[] fileData,
        string contentType = "application/octet-stream")
    {
        var attachmentUrl = $"{url}/{objectId}/addAttachment";
        if (!string.IsNullOrEmpty(_token))
        {
            attachmentUrl = EsriAuthenticationService.AppendTokenToUrl(attachmentUrl, _token);
        }

        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent("json"), "f");

        var fileContent = new ByteArrayContent(fileData);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        formData.Add(fileContent, "attachment", fileName);

        var response = await _httpClient.PostAsync(attachmentUrl, formData);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        CheckForError(json);

        var result = JsonSerializer.Deserialize<EsriAddAttachmentResult>(json, GetJsonOptions());
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize add attachment result");
        }

        return result;
    }

    /// <summary>
    /// Delete an attachment
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="objectId">Object ID of the feature</param>
    /// <param name="attachmentId">Attachment ID</param>
    public async Task<EsriDeleteAttachmentResult> DeleteAttachmentAsync(string url, int objectId, int attachmentId)
    {
        var parameters = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["attachmentIds"] = attachmentId.ToString()
        };

        var deleteUrl = BuildUrl($"{url}/{objectId}/deleteAttachments", parameters);
        var content = new FormUrlEncodedContent(parameters);
        var response = await _httpClient.PostAsync($"{url}/{objectId}/deleteAttachments", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        CheckForError(json);

        var result = JsonSerializer.Deserialize<EsriDeleteAttachmentResult>(json, GetJsonOptions());
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize delete attachment result");
        }

        return result;
    }

    /// <summary>
    /// Query related records
    /// </summary>
    /// <param name="url">Layer URL</param>
    /// <param name="objectIds">Object IDs to query relationships for</param>
    /// <param name="relationshipId">Relationship ID</param>
    /// <param name="outFields">Output fields</param>
    public async Task<EsriRelatedRecordsResult> QueryRelatedAsync(
        string url,
        int[] objectIds,
        int relationshipId,
        string[] outFields)
    {
        var parameters = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["objectIds"] = string.Join(",", objectIds),
            ["relationshipId"] = relationshipId.ToString(),
            ["outFields"] = string.Join(",", outFields),
            ["returnGeometry"] = "true"
        };

        var queryUrl = BuildUrl($"{url}/queryRelatedRecords", parameters);
        var json = await _httpClient.GetStringAsync(queryUrl);
        CheckForError(json);

        var result = JsonSerializer.Deserialize<EsriRelatedRecordsResult>(json, GetJsonOptions());
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize related records result");
        }

        return result;
    }

    /// <summary>
    /// Build URL with query parameters and token
    /// </summary>
    private string BuildUrl(string baseUrl, Dictionary<string, string> parameters)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            parameters["token"] = _token;
        }

        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var separator = baseUrl.Contains("?") ? "&" : "?";
        return $"{baseUrl}{separator}{queryString}";
    }

    /// <summary>
    /// Check for errors in JSON response
    /// </summary>
    private static void CheckForError(string json)
    {
        var errorResponse = JsonSerializer.Deserialize<EsriErrorResponse>(json, GetJsonOptions());
        if (errorResponse?.Error != null)
        {
            throw new EsriServiceException(errorResponse.Error);
        }
    }

    /// <summary>
    /// Get JSON serialization options
    /// </summary>
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Convert Esri FeatureSet to GeoJSON FeatureCollection
    /// </summary>
    public static FeatureCollection ConvertToGeoJson(EsriFeatureSet featureSet)
    {
        var features = new List<IFeature>();

        foreach (var esriFeature in featureSet.Features)
        {
            var geometry = ConvertEsriGeometryToGeoJson(esriFeature.Geometry);
            var attributes = new AttributesTable();

            foreach (var attr in esriFeature.Attributes)
            {
                attributes.Add(attr.Key, attr.Value);
            }

            features.Add(new Feature(geometry, attributes));
        }

        var collection = new FeatureCollection();
        foreach (var feature in features)
        {
            collection.Add(feature);
        }
        return collection;
    }

    /// <summary>
    /// Convert Esri geometry to NetTopologySuite geometry
    /// </summary>
    private static Geometry? ConvertEsriGeometryToGeoJson(EsriGeometry? esriGeometry)
    {
        if (esriGeometry == null) return null;

        var factory = new GeometryFactory();

        return esriGeometry switch
        {
            EsriPoint point => factory.CreatePoint(new CoordinateZ(point.X, point.Y, point.Z ?? double.NaN)),

            EsriMultipoint multipoint => factory.CreateMultiPoint(
                multipoint.Points.Select(p => factory.CreatePoint(new CoordinateZ(p[0], p[1], p.Length > 2 ? p[2] : double.NaN))).ToArray()
            ),

            EsriPolyline polyline => factory.CreateMultiLineString(
                polyline.Paths.Select(path =>
                    factory.CreateLineString(
                        path.Select(p => (Coordinate)new CoordinateZ(p[0], p[1], p.Length > 2 ? p[2] : double.NaN)).ToArray()
                    )
                ).ToArray()
            ),

            EsriPolygon polygon => factory.CreatePolygon(
                polygon.Rings.Length > 0
                    ? factory.CreateLinearRing(
                        polygon.Rings[0].Select(p => (Coordinate)new CoordinateZ(p[0], p[1], p.Length > 2 ? p[2] : double.NaN)).ToArray()
                    )
                    : null
            ),

            _ => null
        };
    }

    /// <summary>
    /// Convert GeoJSON Feature to Esri Feature
    /// </summary>
    public static EsriFeature ConvertFromGeoJson(IFeature feature)
    {
        var esriFeature = new EsriFeature();

        // Convert geometry
        if (feature.Geometry != null)
        {
            esriFeature.Geometry = ConvertGeometryToEsri(feature.Geometry);
        }

        // Convert attributes
        if (feature.Attributes != null)
        {
            foreach (var name in feature.Attributes.GetNames())
            {
                var value = feature.Attributes[name];
                esriFeature.Attributes[name] = value;
            }
        }

        return esriFeature;
    }

    /// <summary>
    /// Convert NetTopologySuite geometry to Esri geometry
    /// </summary>
    private static EsriGeometry? ConvertGeometryToEsri(Geometry geometry)
    {
        return geometry switch
        {
            Point point => new EsriPoint
            {
                X = point.X,
                Y = point.Y,
                Z = double.IsNaN(point.Coordinate.Z) ? null : point.Coordinate.Z
            },

            MultiPoint multipoint => new EsriMultipoint
            {
                Points = multipoint.Coordinates.Select(c =>
                    double.IsNaN(c.Z) ? new[] { c.X, c.Y } : new[] { c.X, c.Y, c.Z }
                ).ToArray()
            },

            LineString lineString => new EsriPolyline
            {
                Paths = new[]
                {
                    lineString.Coordinates.Select(c =>
                        double.IsNaN(c.Z) ? new[] { c.X, c.Y } : new[] { c.X, c.Y, c.Z }
                    ).ToArray()
                }
            },

            MultiLineString multiLineString => new EsriPolyline
            {
                Paths = multiLineString.Geometries.Cast<LineString>().Select(ls =>
                    ls.Coordinates.Select(c =>
                        double.IsNaN(c.Z) ? new[] { c.X, c.Y } : new[] { c.X, c.Y, c.Z }
                    ).ToArray()
                ).ToArray()
            },

            Polygon polygon => new EsriPolygon
            {
                Rings = new[] { polygon.Shell.Coordinates.Select(c =>
                    double.IsNaN(c.Z) ? new[] { c.X, c.Y } : new[] { c.X, c.Y, c.Z }
                ).ToArray() }
                .Concat(polygon.Holes.Select(hole => hole.Coordinates.Select(c =>
                    double.IsNaN(c.Z) ? new[] { c.X, c.Y } : new[] { c.X, c.Y, c.Z }
                ).ToArray()))
                .ToArray()
            },

            _ => null
        };
    }
}

/// <summary>
/// Related records query result
/// </summary>
public class EsriRelatedRecordsResult
{
    [JsonPropertyName("relatedRecordGroups")]
    public List<EsriRelatedRecordGroup>? RelatedRecordGroups { get; set; }
}

/// <summary>
/// Related record group
/// </summary>
public class EsriRelatedRecordGroup
{
    [JsonPropertyName("objectId")]
    public int ObjectId { get; set; }

    [JsonPropertyName("relatedRecords")]
    public List<EsriFeature>? RelatedRecords { get; set; }
}

/// <summary>
/// Exception thrown when Esri service returns an error
/// </summary>
public class EsriServiceException : Exception
{
    public EsriError Error { get; }

    public EsriServiceException(EsriError error)
        : base($"Esri service error {error.Code}: {error.Message}")
    {
        Error = error;
    }
}

/// <summary>
/// Helper extension methods
/// </summary>
internal static class EsriExtensions
{
    public static double[][] ToArrayOfArray(this double[] coords)
    {
        return new[] { coords };
    }
}
