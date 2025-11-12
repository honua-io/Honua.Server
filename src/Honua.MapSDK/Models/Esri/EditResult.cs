// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models.Esri;

/// <summary>
/// Result from ApplyEdits operation
/// </summary>
public class EsriApplyEditsResult
{
    /// <summary>
    /// Results from adding features
    /// </summary>
    [JsonPropertyName("addResults")]
    public List<EsriEditResult>? AddResults { get; set; }

    /// <summary>
    /// Results from updating features
    /// </summary>
    [JsonPropertyName("updateResults")]
    public List<EsriEditResult>? UpdateResults { get; set; }

    /// <summary>
    /// Results from deleting features
    /// </summary>
    [JsonPropertyName("deleteResults")]
    public List<EsriEditResult>? DeleteResults { get; set; }
}

/// <summary>
/// Result of a single edit operation
/// </summary>
public class EsriEditResult
{
    /// <summary>
    /// Object ID of the feature
    /// </summary>
    [JsonPropertyName("objectId")]
    public int? ObjectId { get; set; }

    /// <summary>
    /// Global ID of the feature
    /// </summary>
    [JsonPropertyName("globalId")]
    public string? GlobalId { get; set; }

    /// <summary>
    /// Success status
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error information (if success is false)
    /// </summary>
    [JsonPropertyName("error")]
    public EsriError? Error { get; set; }
}

/// <summary>
/// Error information
/// </summary>
public class EsriError
{
    /// <summary>
    /// Error code
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Error details
    /// </summary>
    [JsonPropertyName("details")]
    public List<string>? Details { get; set; }
}

/// <summary>
/// Error response from Esri REST API
/// </summary>
public class EsriErrorResponse
{
    [JsonPropertyName("error")]
    public EsriError? Error { get; set; }
}
