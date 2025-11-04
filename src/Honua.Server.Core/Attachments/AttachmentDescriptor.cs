// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Describes a single attachment associated with a feature record.
/// </summary>
public sealed record AttachmentDescriptor
{
    public int AttachmentObjectId { get; init; }
    public required string AttachmentId { get; init; }
    public required string ServiceId { get; init; }
    public required string LayerId { get; init; }
    public required string FeatureId { get; init; }
    public string? FeatureGlobalId { get; init; }
    public required string Name { get; init; }
    public required string MimeType { get; init; }
    public long SizeBytes { get; init; }
    public required string ChecksumSha256 { get; init; }
    public required string StorageProvider { get; init; }
    public required string StorageKey { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedUtc { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsSoftDeleted { get; init; }
}
