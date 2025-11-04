// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Host.GeoservicesREST;

internal sealed record GeoservicesAttachmentInfo
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = "application/octet-stream";

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    [JsonPropertyName("keywords")]
    public IReadOnlyList<string>? Keywords { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("parentGlobalId")]
    public string? ParentGlobalId { get; init; }
}

internal sealed record GeoservicesAttachmentGroup
{
    [JsonPropertyName("objectId")]
    public int ObjectId { get; init; }

    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    [JsonPropertyName("attachmentInfos")]
    public IReadOnlyList<GeoservicesAttachmentInfo> AttachmentInfos { get; init; } = Array.Empty<GeoservicesAttachmentInfo>();
}

internal sealed record GeoservicesQueryAttachmentsResponse
{
    [JsonPropertyName("attachmentGroups")]
    public IReadOnlyList<GeoservicesAttachmentGroup> AttachmentGroups { get; init; } = Array.Empty<GeoservicesAttachmentGroup>();

    [JsonPropertyName("hasAttachments")]
    public bool HasAttachments { get; init; }
}

internal sealed record GeoservicesAttachmentError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string?>? Details { get; init; }
}

internal sealed record GeoservicesAddAttachmentResponse
{
    [JsonPropertyName("addAttachmentResult")]
    public GeoservicesAttachmentMutationResult Result { get; init; } = new();
}

internal sealed record GeoservicesUpdateAttachmentResponse
{
    [JsonPropertyName("updateAttachmentResult")]
    public GeoservicesAttachmentMutationResult Result { get; init; } = new();
}

internal sealed record GeoservicesDeleteAttachmentsResponse
{
    [JsonPropertyName("deleteAttachmentResults")]
    public IReadOnlyList<GeoservicesAttachmentDeleteResult> Results { get; init; } = Array.Empty<GeoservicesAttachmentDeleteResult>();
}

internal sealed record GeoservicesAttachmentMutationResult
{
    [JsonPropertyName("objectId")]
    public int ObjectId { get; init; }

    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public GeoservicesAttachmentError? Error { get; init; }
}

internal sealed record GeoservicesAttachmentDeleteResult
{
    [JsonPropertyName("objectId")]
    public int ObjectId { get; init; }

    [JsonPropertyName("attachmentId")]
    public int AttachmentId { get; init; }

    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public GeoservicesAttachmentError? Error { get; init; }
}
