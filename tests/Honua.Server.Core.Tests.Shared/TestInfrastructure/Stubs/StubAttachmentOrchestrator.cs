using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// In-memory stub implementation of IFeatureAttachmentOrchestrator for testing.
/// Provides configurable attachment data without requiring actual file storage.
/// </summary>
/// <remarks>
/// This implementation:
/// - Returns pre-configured attachments based on feature IDs
/// - Supports listing attachments by feature
/// - Returns null for individual attachment lookups (GetAsync)
/// - Throws NotSupportedException for write operations (Add, Update, Delete)
///
/// Use this stub when testing code that needs to verify attachment metadata
/// without actually uploading or downloading files.
/// </remarks>
public sealed class StubAttachmentOrchestrator : IFeatureAttachmentOrchestrator
{
    private readonly IDictionary<string, IReadOnlyList<AttachmentDescriptor>> _attachments;

    /// <summary>
    /// Initializes a new instance with no attachments.
    /// </summary>
    public StubAttachmentOrchestrator()
        : this(new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase))
    {
    }

    /// <summary>
    /// Initializes a new instance with pre-configured attachments.
    /// </summary>
    /// <param name="attachments">
    /// Dictionary mapping feature keys (format: "serviceId:layerId:featureId") to attachment lists.
    /// </param>
    public StubAttachmentOrchestrator(IDictionary<string, IReadOnlyList<AttachmentDescriptor>> attachments)
    {
        _attachments = attachments;
    }

    /// <summary>
    /// Adds attachments for a specific feature. Useful for configuring test data.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="layerId">The layer ID.</param>
    /// <param name="featureId">The feature ID.</param>
    /// <param name="descriptors">The attachment descriptors to return for this feature.</param>
    public void AddAttachments(string serviceId, string layerId, string featureId, params AttachmentDescriptor[] descriptors)
    {
        var key = BuildKey(serviceId, layerId, featureId);
        _attachments[key] = descriptors;
    }

    /// <summary>
    /// Lists attachments for a feature.
    /// </summary>
    public Task<IReadOnlyList<AttachmentDescriptor>> ListAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(serviceId, layerId, featureId);
        return Task.FromResult(_attachments.TryGetValue(key, out var descriptors)
            ? descriptors
            : Array.Empty<AttachmentDescriptor>());
    }

    /// <summary>
    /// Lists attachments for multiple features (batch operation).
    /// </summary>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListBatchAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase);

        foreach (var featureId in featureIds)
        {
            var key = BuildKey(serviceId, layerId, featureId);
            result[featureId] = _attachments.TryGetValue(key, out var descriptors)
                ? descriptors
                : Array.Empty<AttachmentDescriptor>();
        }

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>>(result);
    }

    /// <summary>
    /// Always returns null. Individual attachment retrieval is not supported by this stub.
    /// </summary>
    public Task<AttachmentDescriptor?> GetAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
        => Task.FromResult<AttachmentDescriptor?>(null);

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<FeatureAttachmentOperationResult> AddAsync(AddFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("StubAttachmentOrchestrator is read-only. Use a real implementation for write operations.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<FeatureAttachmentOperationResult> UpdateAsync(UpdateFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("StubAttachmentOrchestrator is read-only. Use a real implementation for write operations.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<bool> DeleteAsync(DeleteFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("StubAttachmentOrchestrator is read-only. Use a real implementation for write operations.");

    private static string BuildKey(string serviceId, string layerId, string featureId)
        => $"{serviceId}:{layerId}:{featureId}";
}
