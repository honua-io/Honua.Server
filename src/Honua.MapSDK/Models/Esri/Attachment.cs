// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models.Esri;

/// <summary>
/// Attachment information
/// </summary>
public class EsriAttachmentInfo
{
    /// <summary>
    /// Attachment ID
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Content type (MIME type)
    /// </summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    /// <summary>
    /// Size in bytes
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Name of the attachment
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Parent object ID (feature ID)
    /// </summary>
    [JsonPropertyName("parentObjectId")]
    public int? ParentObjectId { get; set; }

    /// <summary>
    /// Parent global ID
    /// </summary>
    [JsonPropertyName("parentGlobalId")]
    public string? ParentGlobalId { get; set; }

    /// <summary>
    /// Keywords
    /// </summary>
    [JsonPropertyName("keywords")]
    public string? Keywords { get; set; }
}

/// <summary>
/// List of attachments for a feature
/// </summary>
public class EsriAttachmentInfos
{
    /// <summary>
    /// Object ID field name
    /// </summary>
    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; set; }

    /// <summary>
    /// Global ID field name
    /// </summary>
    [JsonPropertyName("globalIdFieldName")]
    public string? GlobalIdFieldName { get; set; }

    /// <summary>
    /// Array of attachment information
    /// </summary>
    [JsonPropertyName("attachmentInfos")]
    public List<EsriAttachmentInfo> AttachmentInfos { get; set; } = new();
}

/// <summary>
/// Result of adding an attachment
/// </summary>
public class EsriAddAttachmentResult
{
    /// <summary>
    /// Attachment ID
    /// </summary>
    [JsonPropertyName("objectId")]
    public int ObjectId { get; set; }

    /// <summary>
    /// Global ID of the attachment
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
/// Result of deleting an attachment
/// </summary>
public class EsriDeleteAttachmentResult
{
    /// <summary>
    /// Attachment ID that was deleted
    /// </summary>
    [JsonPropertyName("objectId")]
    public int ObjectId { get; set; }

    /// <summary>
    /// Global ID of the deleted attachment
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
