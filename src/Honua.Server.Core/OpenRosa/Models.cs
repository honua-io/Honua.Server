// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// Represents a form submission from an ODK client.
/// </summary>
public sealed class Submission
{
    public required string Id { get; init; }
    public required string InstanceId { get; init; }
    public required string FormId { get; init; }
    public required string FormVersion { get; init; }
    public required string LayerId { get; init; }
    public required string ServiceId { get; init; }
    public required string SubmittedBy { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
    public string? DeviceId { get; init; }
    public SubmissionStatus Status { get; init; } = SubmissionStatus.Pending;
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public string? ReviewNotes { get; init; }
    public required XDocument XmlData { get; init; }
    public Geometry? Geometry { get; init; }
    public required IReadOnlyDictionary<string, object?> Attributes { get; init; }
    public IReadOnlyList<SubmissionAttachment> Attachments { get; init; } = Array.Empty<SubmissionAttachment>();
}

/// <summary>
/// Status of a submission in the review workflow.
/// </summary>
public enum SubmissionStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// File attachment associated with a submission (photo, audio, video, signature).
/// </summary>
public sealed class SubmissionAttachment
{
    public required string Filename { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required string StoragePath { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// XForm definition for a layer, generated from metadata.
/// </summary>
public sealed class XForm
{
    public required string FormId { get; init; }
    public required string Version { get; init; }
    public required string Title { get; init; }
    public required string LayerId { get; init; }
    public required string ServiceId { get; init; }
    public required XDocument Xml { get; init; }
    public required string Hash { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// OpenRosa FormList item (returned by /openrosa/formList endpoint).
/// </summary>
public sealed class FormListItem
{
    public required string FormId { get; init; }
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Hash { get; init; }
    public string? DescriptionText { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ManifestUrl { get; init; }
}

/// <summary>
/// Request to process a submission from ODK Collect.
/// </summary>
public sealed class SubmissionRequest
{
    public required XDocument XmlDocument { get; init; }
    public required string SubmittedBy { get; init; }
    public string? DeviceId { get; init; }
    public IReadOnlyList<AttachmentFile> Attachments { get; init; } = Array.Empty<AttachmentFile>();
}

/// <summary>
/// Uploaded attachment file from multipart request.
/// </summary>
public sealed class AttachmentFile
{
    public required string Filename { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required Func<CancellationToken, Task<Stream>> OpenStreamAsync { get; init; }
}

/// <summary>
/// Result of processing a submission.
/// </summary>
public sealed class SubmissionResult
{
    public bool Success { get; init; }
    public string? InstanceId { get; init; }
    public string? ErrorMessage { get; init; }
    public SubmissionResultType ResultType { get; init; }
}

public enum SubmissionResultType
{
    Accepted,
    DirectPublished,
    StagedForReview,
    Rejected
}

/// <summary>
/// OpenRosa error response.
/// </summary>
public sealed class OpenRosaError
{
    public required string Message { get; init; }
    public string? ErrorCode { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
