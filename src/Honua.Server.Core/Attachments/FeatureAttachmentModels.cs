// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

public enum FeatureAttachmentOperation
{
    Add,
    Update,
    Delete
}

public sealed class FeatureAttachmentUpload
{
    public FeatureAttachmentUpload(
        string fileName,
        string mimeType,
        Func<CancellationToken, Task<Stream>> contentFactory,
        long? declaredSizeBytes = null,
        string? declaredChecksumSha256 = null)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
        ContentFactory = contentFactory ?? throw new ArgumentNullException(nameof(contentFactory));
        DeclaredSizeBytes = declaredSizeBytes;
        DeclaredChecksumSha256 = declaredChecksumSha256;
    }

    public string FileName { get; }
    public string MimeType { get; }
    public Func<CancellationToken, Task<Stream>> ContentFactory { get; }
    public long? DeclaredSizeBytes { get; }
    public string? DeclaredChecksumSha256 { get; }
}

public sealed class AddFeatureAttachmentRequest
{
    public AddFeatureAttachmentRequest(
        string serviceId,
        string layerId,
        string featureId,
        FeatureAttachmentUpload upload,
        string? featureGlobalId = null,
        string? requestedBy = null)
    {
        ServiceId = serviceId ?? throw new ArgumentNullException(nameof(serviceId));
        LayerId = layerId ?? throw new ArgumentNullException(nameof(layerId));
        FeatureId = featureId ?? throw new ArgumentNullException(nameof(featureId));
        Upload = upload ?? throw new ArgumentNullException(nameof(upload));
        FeatureGlobalId = featureGlobalId;
        RequestedBy = requestedBy;
    }

    public string ServiceId { get; }
    public string LayerId { get; }
    public string FeatureId { get; }
    public FeatureAttachmentUpload Upload { get; }
    public string? FeatureGlobalId { get; }
    public string? RequestedBy { get; }
}

public sealed class UpdateFeatureAttachmentRequest
{
    public UpdateFeatureAttachmentRequest(
        string serviceId,
        string layerId,
        string featureId,
        string attachmentId,
        FeatureAttachmentUpload upload,
        string? featureGlobalId = null,
        string? requestedBy = null)
    {
        ServiceId = serviceId ?? throw new ArgumentNullException(nameof(serviceId));
        LayerId = layerId ?? throw new ArgumentNullException(nameof(layerId));
        FeatureId = featureId ?? throw new ArgumentNullException(nameof(featureId));
        AttachmentId = attachmentId ?? throw new ArgumentNullException(nameof(attachmentId));
        Upload = upload ?? throw new ArgumentNullException(nameof(upload));
        FeatureGlobalId = featureGlobalId;
        RequestedBy = requestedBy;
    }

    public string ServiceId { get; }
    public string LayerId { get; }
    public string FeatureId { get; }
    public string AttachmentId { get; }
    public FeatureAttachmentUpload Upload { get; }
    public string? FeatureGlobalId { get; }
    public string? RequestedBy { get; }
}

public sealed class DeleteFeatureAttachmentRequest
{
    public DeleteFeatureAttachmentRequest(
        string serviceId,
        string layerId,
        string featureId,
        string attachmentId,
        string? requestedBy = null)
    {
        ServiceId = serviceId ?? throw new ArgumentNullException(nameof(serviceId));
        LayerId = layerId ?? throw new ArgumentNullException(nameof(layerId));
        FeatureId = featureId ?? throw new ArgumentNullException(nameof(featureId));
        AttachmentId = attachmentId ?? throw new ArgumentNullException(nameof(attachmentId));
        RequestedBy = requestedBy;
    }

    public string ServiceId { get; }
    public string LayerId { get; }
    public string FeatureId { get; }
    public string AttachmentId { get; }
    public string? RequestedBy { get; }
}

public sealed record FeatureAttachmentError(string Code, string Message, IReadOnlyDictionary<string, string?>? Details = null)
{
    public static FeatureAttachmentError AttachmentsDisabled(string serviceId, string layerId) =>
        new("attachments_disabled", $"Attachments are not enabled for layer '{layerId}' in service '{serviceId}'.");

    public static FeatureAttachmentError LayerNotFound(string serviceId, string layerId) =>
        new("layer_not_found", $"Layer '{layerId}' was not found in service '{serviceId}'.");

    public static FeatureAttachmentError FeatureNotFound(string featureId) =>
        new("feature_not_found", $"Feature '{featureId}' was not found.");

    public static FeatureAttachmentError AttachmentNotFound(string attachmentId) =>
        new("attachment_not_found", $"Attachment '{attachmentId}' was not found.");

    public static FeatureAttachmentError StorageProfileMissing(string storageProfileId) =>
        new("storage_profile_missing", $"Attachment storage profile '{storageProfileId}' is not configured.");

    public static FeatureAttachmentError GlobalIdRequired() =>
        new("global_id_required", "Layer requires globalId values when working with attachments.");

    public static FeatureAttachmentError MimeTypeNotAllowed(string mimeType) =>
        new("mime_type_not_allowed", $"Attachment MIME type '{mimeType}' is not permitted for this layer.");

    public static FeatureAttachmentError MimeTypeBlocked(string mimeType) =>
        new("mime_type_blocked", $"Attachment MIME type '{mimeType}' is explicitly blocked for this layer.");

    public static FeatureAttachmentError MaxSizeExceeded(long maxSizeBytes, long actualSizeBytes) =>
        new("max_size_exceeded", $"Attachment exceeds the configured maximum size of {maxSizeBytes} bytes.", new Dictionary<string, string?>
        {
            ["maxSizeBytes"] = maxSizeBytes.ToString(),
            ["actualSizeBytes"] = actualSizeBytes.ToString()
        });

    public static FeatureAttachmentError DeclaredSizeMismatch(long declaredSizeBytes, long actualSizeBytes) =>
        new("declared_size_mismatch", "Declared attachment size does not match uploaded content.", new Dictionary<string, string?>
        {
            ["declaredSizeBytes"] = declaredSizeBytes.ToString(),
            ["actualSizeBytes"] = actualSizeBytes.ToString()
        });

    public static FeatureAttachmentError ChecksumMismatch(string expectedChecksum, string actualChecksum) =>
        new("checksum_mismatch", "Attachment checksum does not match provided value.", new Dictionary<string, string?>
        {
            ["expectedChecksum"] = expectedChecksum,
            ["actualChecksum"] = actualChecksum
        });
}

public sealed record FeatureAttachmentOperationResult(
    FeatureAttachmentOperation Operation,
    bool Success,
    AttachmentDescriptor? Attachment,
    FeatureAttachmentError? Error)
{
    public static FeatureAttachmentOperationResult SuccessResult(FeatureAttachmentOperation operation, AttachmentDescriptor descriptor) =>
        new(operation, true, descriptor ?? throw new ArgumentNullException(nameof(descriptor)), null);

    public static FeatureAttachmentOperationResult Failure(FeatureAttachmentOperation operation, FeatureAttachmentError error) =>
        new(operation, false, null, error ?? throw new ArgumentNullException(nameof(error)));
}

public interface IFeatureAttachmentOrchestrator
{
    Task<IReadOnlyList<AttachmentDescriptor>> ListAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch retrieves attachments for multiple features to avoid N+1 query patterns.
    /// Returns a dictionary mapping feature IDs to their attachment lists.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListBatchAsync(string serviceId, string layerId, IReadOnlyList<string> featureIds, CancellationToken cancellationToken = default);

    Task<AttachmentDescriptor?> GetAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default);
    Task<FeatureAttachmentOperationResult> AddAsync(AddFeatureAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<FeatureAttachmentOperationResult> UpdateAsync(UpdateFeatureAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(DeleteFeatureAttachmentRequest request, CancellationToken cancellationToken = default);
}
