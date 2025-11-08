// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

public sealed class FeatureAttachmentOrchestrator : IFeatureAttachmentOrchestrator
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly IFeatureRepository _featureRepository;
    private readonly IFeatureAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStoreSelector _storeSelector;
    private readonly ILogger<FeatureAttachmentOrchestrator> _logger;

    public FeatureAttachmentOrchestrator(
        IMetadataRegistry metadataRegistry,
        IHonuaConfigurationService configurationService,
        IFeatureRepository featureRepository,
        IFeatureAttachmentRepository attachmentRepository,
        IAttachmentStoreSelector storeSelector,
        ILogger<FeatureAttachmentOrchestrator> logger)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _featureRepository = featureRepository ?? throw new ArgumentNullException(nameof(featureRepository));
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
        _storeSelector = storeSelector ?? throw new ArgumentNullException(nameof(storeSelector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<AttachmentDescriptor>> ListAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);
        Guard.NotNullOrWhiteSpace(featureId);

        var layer = await ResolveLayerAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        if (layer is null || !layer.Attachments.Enabled)
        {
            return Array.Empty<AttachmentDescriptor>();
        }

        return await _attachmentRepository.ListByFeatureAsync(serviceId, layerId, featureId, cancellationToken);
    }


    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListBatchAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        var layer = await ResolveLayerAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        if (layer is null || !layer.Attachments.Enabled)
        {
            // Return empty dictionary with all feature IDs mapped to empty lists
            return featureIds.ToDictionary(
                fid => fid,
                _ => (IReadOnlyList<AttachmentDescriptor>)Array.Empty<AttachmentDescriptor>(),
                StringComparer.OrdinalIgnoreCase);
        }

        return await _attachmentRepository.ListByFeaturesAsync(serviceId, layerId, featureIds, cancellationToken);
    }

    public async Task<AttachmentDescriptor?> GetAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
    {
        var layer = await ResolveLayerAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        if (layer is null || !layer.Attachments.Enabled)
        {
            return null;
        }

        return await _attachmentRepository.FindByIdAsync(serviceId, layerId, attachmentId, cancellationToken);
    }

    public async Task<FeatureAttachmentOperationResult> AddAsync(AddFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var layer = await ResolveLayerAsync(request.ServiceId, request.LayerId, cancellationToken).ConfigureAwait(false);
        if (layer is null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.LayerNotFound(request.ServiceId, request.LayerId));
        }

        if (!layer.Attachments.Enabled)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.AttachmentsDisabled(request.ServiceId, request.LayerId));
        }

        if (layer.Attachments.RequireGlobalIds && string.IsNullOrWhiteSpace(request.FeatureGlobalId))
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.GlobalIdRequired());
        }

        var feature = await _featureRepository.GetAsync(request.ServiceId, request.LayerId, request.FeatureId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (feature is null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.FeatureNotFound(request.FeatureId));
        }

        // When RequireGlobalIds is true, validate that the provided globalId matches the feature's stored globalId
        // This prevents attachment spoofing where an attacker supplies a valid globalId for a different feature
        if (layer.Attachments.RequireGlobalIds && !string.IsNullOrWhiteSpace(request.FeatureGlobalId))
        {
            // Extract globalId from feature attributes (typically stored in "globalId" or "GlobalId" field)
            var featureGlobalId = feature.Attributes.TryGetValue("globalId", out var gid1) ? gid1?.ToString() :
                                  feature.Attributes.TryGetValue("GlobalId", out var gid2) ? gid2?.ToString() :
                                  feature.Attributes.TryGetValue("GLOBALID", out var gid3) ? gid3?.ToString() : null;

            if (!string.Equals(request.FeatureGlobalId, featureGlobalId, StringComparison.Ordinal))
            {
                return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add,
                    new FeatureAttachmentError("GLOBALID_MISMATCH",
                        $"Provided globalId '{request.FeatureGlobalId}' does not match feature's globalId '{featureGlobalId}'."));
            }
        }

        var settings = ResolveAttachmentSettings(layer);
        if (!settings.IsValid)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.StorageProfileMissing(settings.StorageProfileId ?? string.Empty));
        }

        var validationError = ValidateUpload(settings, request.Upload);
        if (validationError is not null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, validationError);
        }

        // Pre-validate declared size if provided
        if (request.Upload.DeclaredSizeBytes is long declaredSize && declaredSize > settings.MaxSizeBytes)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.MaxSizeExceeded(settings.MaxSizeBytes, declaredSize));
        }

        var store = ResolveStoreForProfile(settings.StorageProfileId!, request.ServiceId, request.LayerId);
        var attachmentId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceId"] = request.ServiceId,
            ["layerId"] = request.LayerId,
            ["featureId"] = request.FeatureId,
            ["attachmentId"] = attachmentId
        };

        // Stream upload directly to storage while computing checksum and enforcing size limit
        await using var streamedContent = await StreamUploadAsync(request.Upload, settings.MaxSizeBytes, cancellationToken).ConfigureAwait(false);

        var storeResult = await store.PutAsync(
            streamedContent.Stream,
            new AttachmentStorePutRequest
            {
                AttachmentId = attachmentId,
                FileName = request.Upload.FileName,
                MimeType = NormalizeMimeType(request.Upload.MimeType),
                SizeBytes = request.Upload.DeclaredSizeBytes ?? 0,
                ChecksumSha256 = string.Empty, // Will be computed during upload
                Metadata = metadata
            },
            cancellationToken).ConfigureAwait(false);

        // Validate size after upload completes
        var actualSize = streamedContent.SizeBytes;
        if (actualSize > settings.MaxSizeBytes)
        {
            // Delete the uploaded file since it exceeds size limit
            await SafeDeletePointerAsync(store, storeResult.Pointer, cancellationToken).ConfigureAwait(false);
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.MaxSizeExceeded(settings.MaxSizeBytes, actualSize));
        }

        if (request.Upload.DeclaredSizeBytes is long declaredSizeValue && declaredSizeValue != actualSize)
        {
            await SafeDeletePointerAsync(store, storeResult.Pointer, cancellationToken).ConfigureAwait(false);
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.DeclaredSizeMismatch(declaredSizeValue, actualSize));
        }

        var actualChecksum = streamedContent.ChecksumSha256;
        if (!string.IsNullOrWhiteSpace(request.Upload.DeclaredChecksumSha256))
        {
            var declared = NormalizeChecksum(request.Upload.DeclaredChecksumSha256!);
            if (!string.Equals(declared, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                await SafeDeletePointerAsync(store, storeResult.Pointer, cancellationToken).ConfigureAwait(false);
                return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add, FeatureAttachmentError.ChecksumMismatch(declared, actualChecksum));
            }
        }

        var descriptor = new AttachmentDescriptor
        {
            AttachmentId = attachmentId,
            ServiceId = request.ServiceId,
            LayerId = request.LayerId,
            FeatureId = request.FeatureId,
            FeatureGlobalId = request.FeatureGlobalId,
            Name = request.Upload.FileName,
            MimeType = NormalizeMimeType(request.Upload.MimeType),
            SizeBytes = actualSize,
            ChecksumSha256 = actualChecksum,
            StorageProvider = storeResult.Pointer.StorageProvider,
            StorageKey = storeResult.Pointer.StorageKey,
            CreatedUtc = now,
            CreatedBy = request.RequestedBy,
            UpdatedUtc = null,
            UpdatedBy = null,
            IsSoftDeleted = false
        };

        // Guard against orphaned blobs: if metadata persistence fails, delete the stored file
        AttachmentDescriptor persisted;
        try
        {
            persisted = await _attachmentRepository.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist attachment metadata for {AttachmentId}. Attempting to delete orphaned blob.", attachmentId);

            // Best-effort cleanup of the orphaned blob
            try
            {
                await store.DeleteAsync(storeResult.Pointer, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Successfully deleted orphaned blob for attachment {AttachmentId}", attachmentId);
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning(deleteEx, "Failed to delete orphaned blob for attachment {AttachmentId}. Manual cleanup may be required.", attachmentId);
            }

            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Add,
                new FeatureAttachmentError("METADATA_PERSISTENCE_FAILED", $"Failed to persist attachment metadata: {ex.Message}"));
        }

        _logger.LogInformation("Attachment {AttachmentId} created for feature {FeatureId} on layer {LayerId} using profile {ProfileId}", attachmentId, request.FeatureId, request.LayerId, settings.StorageProfileId);

        return FeatureAttachmentOperationResult.SuccessResult(FeatureAttachmentOperation.Add, persisted);
    }

    public async Task<FeatureAttachmentOperationResult> UpdateAsync(UpdateFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var layer = await ResolveLayerAsync(request.ServiceId, request.LayerId, cancellationToken).ConfigureAwait(false);
        if (layer is null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.LayerNotFound(request.ServiceId, request.LayerId));
        }

        if (!layer.Attachments.Enabled)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.AttachmentsDisabled(request.ServiceId, request.LayerId));
        }

        var existing = await _attachmentRepository.FindByIdAsync(request.ServiceId, request.LayerId, request.AttachmentId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.AttachmentNotFound(request.AttachmentId));
        }

        if (!string.Equals(existing.FeatureId, request.FeatureId, StringComparison.OrdinalIgnoreCase))
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.FeatureNotFound(request.FeatureId));
        }

        var effectiveGlobalId = request.FeatureGlobalId ?? existing.FeatureGlobalId;
        if (layer.Attachments.RequireGlobalIds && string.IsNullOrWhiteSpace(effectiveGlobalId))
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.GlobalIdRequired());
        }

        var settings = ResolveAttachmentSettings(layer);
        if (!settings.IsValid)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.StorageProfileMissing(settings.StorageProfileId ?? string.Empty));
        }

        var validationError = ValidateUpload(settings, request.Upload);
        if (validationError is not null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, validationError);
        }

        // Pre-validate declared size if provided
        if (request.Upload.DeclaredSizeBytes is long declaredSize && declaredSize > settings.MaxSizeBytes)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.MaxSizeExceeded(settings.MaxSizeBytes, declaredSize));
        }

        var store = ResolveStoreForProfile(settings.StorageProfileId!, request.ServiceId, request.LayerId);
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceId"] = request.ServiceId,
            ["layerId"] = request.LayerId,
            ["featureId"] = request.FeatureId,
            ["attachmentId"] = existing.AttachmentId
        };

        // Stream upload directly to storage while computing checksum and enforcing size limit
        await using var streamedContent = await StreamUploadAsync(request.Upload, settings.MaxSizeBytes, cancellationToken).ConfigureAwait(false);

        var storeResult = await store.PutAsync(
            streamedContent.Stream,
            new AttachmentStorePutRequest
            {
                AttachmentId = existing.AttachmentId,
                FileName = request.Upload.FileName,
                MimeType = NormalizeMimeType(request.Upload.MimeType),
                SizeBytes = request.Upload.DeclaredSizeBytes ?? 0,
                ChecksumSha256 = string.Empty, // Will be computed during upload
                Metadata = metadata
            },
            cancellationToken).ConfigureAwait(false);

        // Validate size after upload completes
        var actualSize = streamedContent.SizeBytes;
        if (actualSize > settings.MaxSizeBytes)
        {
            // Delete the uploaded file since it exceeds size limit
            await SafeDeletePointerAsync(store, storeResult.Pointer, cancellationToken).ConfigureAwait(false);
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.MaxSizeExceeded(settings.MaxSizeBytes, actualSize));
        }

        if (request.Upload.DeclaredSizeBytes is long declaredSizeValue && declaredSizeValue != actualSize)
        {
            await SafeDeletePointerAsync(store, storeResult.Pointer, cancellationToken).ConfigureAwait(false);
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.DeclaredSizeMismatch(declaredSizeValue, actualSize));
        }

        var actualChecksum = streamedContent.ChecksumSha256;
        if (!string.IsNullOrWhiteSpace(request.Upload.DeclaredChecksumSha256))
        {
            var declared = NormalizeChecksum(request.Upload.DeclaredChecksumSha256!);
            if (!string.Equals(declared, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                await SafeDeletePointerAsync(store, storeResult.Pointer, cancellationToken).ConfigureAwait(false);
                return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.ChecksumMismatch(declared, actualChecksum));
            }
        }

        var updatedDescriptor = existing with
        {
            Name = request.Upload.FileName,
            MimeType = NormalizeMimeType(request.Upload.MimeType),
            SizeBytes = actualSize,
            ChecksumSha256 = actualChecksum,
            StorageProvider = storeResult.Pointer.StorageProvider,
            StorageKey = storeResult.Pointer.StorageKey,
            UpdatedUtc = DateTimeOffset.UtcNow,
            UpdatedBy = request.RequestedBy,
            FeatureGlobalId = effectiveGlobalId
        };

        var persisted = await _attachmentRepository.UpdateAsync(updatedDescriptor, cancellationToken).ConfigureAwait(false);
        if (persisted is null)
        {
            return FeatureAttachmentOperationResult.Failure(FeatureAttachmentOperation.Update, FeatureAttachmentError.AttachmentNotFound(request.AttachmentId));
        }

        var pointerChanged = !string.Equals(existing.StorageProvider, storeResult.Pointer.StorageProvider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.StorageKey, storeResult.Pointer.StorageKey, StringComparison.Ordinal);
        if (pointerChanged)
        {
            var previousPointer = new AttachmentPointer(existing.StorageProvider, existing.StorageKey);
            await SafeDeletePointerAsync(store, previousPointer, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Attachment {AttachmentId} updated for feature {FeatureId} on layer {LayerId}", existing.AttachmentId, request.FeatureId, request.LayerId);
        return FeatureAttachmentOperationResult.SuccessResult(FeatureAttachmentOperation.Update, persisted);
    }

    public async Task<bool> DeleteAsync(DeleteFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var layer = await ResolveLayerAsync(request.ServiceId, request.LayerId, cancellationToken).ConfigureAwait(false);
        if (layer is null || !layer.Attachments.Enabled)
        {
            return false;
        }

        var descriptor = await _attachmentRepository.FindByIdAsync(request.ServiceId, request.LayerId, request.AttachmentId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return false;
        }

        if (!string.Equals(descriptor.FeatureId, request.FeatureId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Attempt metadata deletion first
        var removed = await _attachmentRepository.DeleteAsync(request.ServiceId, request.LayerId, request.AttachmentId, cancellationToken).ConfigureAwait(false);

        // Always attempt storage cleanup even if metadata deletion failed
        // This prevents orphaned blobs when metadata deletion succeeds but method returns false for other reasons
        try
        {
            var store = ResolveStoreForProvider(descriptor.StorageProvider, request.ServiceId, request.LayerId);
            var pointer = new AttachmentPointer(descriptor.StorageProvider, descriptor.StorageKey);
            await SafeDeletePointerAsync(store, pointer, cancellationToken).ConfigureAwait(false);
        }
        catch (AttachmentStoreNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unable to resolve storage provider {StorageProvider} for attachment {AttachmentId}. The attachment metadata removal status: {MetadataRemoved}. Manual blob cleanup may be required.", descriptor.StorageProvider, request.AttachmentId, removed);
        }

        if (!removed)
        {
            // Metadata deletion failed but storage cleanup was attempted
            return false;
        }

        _logger.LogInformation("Attachment {AttachmentId} deleted for feature {FeatureId} on layer {LayerId}", request.AttachmentId, request.FeatureId, request.LayerId);
        return true;
    }

    private async Task<LayerDefinition?> ResolveLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken)
    {
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.TryGetLayer(serviceId, layerId, out var layer) ? layer : null;
    }

    private AttachmentValidationSettings ResolveAttachmentSettings(LayerDefinition layer)
    {
        var config = _configurationService.Current.Attachments;
        var layerSettings = layer.Attachments;

        if (!layerSettings.Enabled)
        {
            return AttachmentValidationSettings.Disabled;
        }

        var maxSizeMiB = layerSettings.MaxSizeMiB ?? config.DefaultMaxSizeMiB;
        if (maxSizeMiB <= 0)
        {
            maxSizeMiB = config.DefaultMaxSizeMiB;
        }

        var maxSizeBytes = Convert.ToInt64(maxSizeMiB) * 1024L * 1024L;
        if (maxSizeBytes <= 0)
        {
            maxSizeBytes = long.MaxValue;
        }

        var storageProfileId = layerSettings.StorageProfileId;
        if (string.IsNullOrWhiteSpace(storageProfileId))
        {
            return AttachmentValidationSettings.Invalid;
        }

        return new AttachmentValidationSettings(
            true,
            storageProfileId,
            maxSizeBytes,
            layerSettings.AllowedContentTypes,
            layerSettings.DisallowedContentTypes,
            layerSettings.RequireGlobalIds);
    }

    private static FeatureAttachmentError? ValidateUpload(AttachmentValidationSettings settings, FeatureAttachmentUpload upload)
    {
        var mimeType = NormalizeMimeType(upload.MimeType);

        if (settings.AllowedContentTypes.Count > 0 && !settings.AllowedContentTypes.Contains(mimeType))
        {
            return FeatureAttachmentError.MimeTypeNotAllowed(mimeType);
        }

        if (settings.DisallowedContentTypes.Contains(mimeType))
        {
            return FeatureAttachmentError.MimeTypeBlocked(mimeType);
        }

        return null;
    }

    /// <summary>
    /// Resolves an attachment store by storage profile ID (for new uploads).
    /// </summary>
    private IAttachmentStore ResolveStoreForProfile(string storageProfileId, string serviceId, string layerId)
    {
        try
        {
            return _storeSelector.Resolve(storageProfileId);
        }
        catch (AttachmentStoreNotFoundException ex)
        {
            _logger.LogError(ex, "Unable to resolve attachment store for profile {ProfileId} (service {ServiceId}, layer {LayerId})", storageProfileId, serviceId, layerId);
            throw;
        }
    }

    /// <summary>
    /// Resolves an attachment store by provider name (for retrieving/deleting existing attachments).
    /// This ensures we can access attachments even if the layer's storage profile has changed.
    /// </summary>
    private IAttachmentStore ResolveStoreForProvider(string storageProviderName, string serviceId, string layerId)
    {
        try
        {
            return _storeSelector.Resolve(storageProviderName);
        }
        catch (AttachmentStoreNotFoundException ex)
        {
            _logger.LogError(ex, "Unable to resolve attachment store for provider {ProviderName} (service {ServiceId}, layer {LayerId})", storageProviderName, serviceId, layerId);
            throw;
        }
    }

    /// <summary>
    /// Computes checksum and size of upload stream without buffering entire content.
    /// Returns a stream wrapper that can be read once to upload to storage.
    /// </summary>
    private static async Task<StreamedAttachmentContent> StreamUploadAsync(FeatureAttachmentUpload upload, long maxSizeBytes, CancellationToken cancellationToken)
    {
        var sourceStream = await upload.ContentFactory(cancellationToken).ConfigureAwait(false);
        if (sourceStream is null)
        {
            throw new InvalidOperationException("Attachment upload content factory returned null stream.");
        }

        // Create a stream that computes checksum as it's read and enforces maximum size limit
        var hashingStream = new ChecksumComputingStream(sourceStream, maxSizeBytes);
        return new StreamedAttachmentContent(hashingStream);
    }

    /// <summary>
    /// Legacy buffered upload path - kept for validation scenarios that require multiple reads.
    /// </summary>
    private static async Task<BufferedAttachmentContent> BufferUploadAsync(FeatureAttachmentUpload upload, CancellationToken cancellationToken)
    {
        await using var sourceStream = await upload.ContentFactory(cancellationToken).ConfigureAwait(false);
        if (sourceStream is null)
        {
            throw new InvalidOperationException("Attachment upload content factory returned null stream.");
        }

        var memory = new MemoryStream(upload.DeclaredSizeBytes is { } declared && declared > 0 && declared <= int.MaxValue
            ? (int)declared
            : 0);

        var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int read;
            while ((read = await sourceStream.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hasher.AppendData(rented, 0, read);
                await memory.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            memory.Position = 0;
            var hash = hasher.GetHashAndReset();
            var checksum = Convert.ToHexString(hash).ToLowerInvariant();
            return new BufferedAttachmentContent(memory, memory.Length, checksum);
        }
        catch
        {
            await memory.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async Task SafeDeletePointerAsync(IAttachmentStore store, AttachmentPointer pointer, CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteAsync(pointer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log cleanup failures but don't bubble - orphaned blobs are tracked via metrics
            _logger.LogWarning(ex, "Failed to delete orphaned attachment blob with storage key {StorageKey}", pointer.StorageKey);
        }
    }

    private static string NormalizeMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return "application/octet-stream";
        }

        return mimeType.Trim().ToLowerInvariant();
    }

    private static string NormalizeChecksum(string checksum)
    {
        return checksum.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    private sealed class BufferedAttachmentContent : IAsyncDisposable
    {
        public BufferedAttachmentContent(MemoryStream stream, long sizeBytes, string checksumSha256)
        {
            Stream = stream;
            SizeBytes = sizeBytes;
            ChecksumSha256 = checksumSha256;
        }

        public MemoryStream Stream { get; }
        public long SizeBytes { get; }
        public string ChecksumSha256 { get; }

        public ValueTask DisposeAsync()
        {
            return Stream.DisposeAsync();
        }
    }

    private sealed class StreamedAttachmentContent : IAsyncDisposable
    {
        private readonly ChecksumComputingStream _stream;

        public StreamedAttachmentContent(ChecksumComputingStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Stream Stream => _stream;
        public long SizeBytes => _stream.BytesRead;
        public string ChecksumSha256 => _stream.GetChecksum();

        public ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }
    }

    /// <summary>
    /// Stream wrapper that computes SHA256 checksum as data is read and enforces maximum size limit.
    /// </summary>
    private sealed class ChecksumComputingStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly IncrementalHash _hasher;
        private readonly long _maxSizeBytes;
        private long _bytesRead;
        private bool _disposed;

        public ChecksumComputingStream(Stream innerStream, long maxSizeBytes)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            _maxSizeBytes = maxSizeBytes;
        }

        public long BytesRead => _bytesRead;

        public string GetChecksum()
        {
            var hash = _hasher.GetHashAndReset();
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _bytesRead;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Enforce maximum size limit
            if (_bytesRead >= _maxSizeBytes)
            {
                return 0; // Signal end of stream when limit reached
            }

            // Limit read to not exceed max size
            var maxToRead = (int)Math.Min(count, _maxSizeBytes - _bytesRead);
            var read = _innerStream.Read(buffer, offset, maxToRead);
            if (read > 0)
            {
                _hasher.AppendData(buffer, offset, read);
                _bytesRead += read;
            }
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Enforce maximum size limit
            if (_bytesRead >= _maxSizeBytes)
            {
                return 0; // Signal end of stream when limit reached
            }

            // Limit read to not exceed max size
            var maxToRead = (int)Math.Min(count, _maxSizeBytes - _bytesRead);
            var read = await _innerStream.ReadAsync(buffer.AsMemory(offset, maxToRead), cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                _hasher.AppendData(buffer, offset, read);
                _bytesRead += read;
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Enforce maximum size limit
            if (_bytesRead >= _maxSizeBytes)
            {
                return 0; // Signal end of stream when limit reached
            }

            // Limit read to not exceed max size
            var maxToRead = (int)Math.Min(buffer.Length, _maxSizeBytes - _bytesRead);
            var read = await _innerStream.ReadAsync(buffer.Slice(0, maxToRead), cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                _hasher.AppendData(buffer.Span.Slice(0, read));
                _bytesRead += read;
            }
            return read;
        }

        public override void Flush() => _innerStream.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hasher?.Dispose();
                    _innerStream?.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _hasher?.Dispose();
                await _innerStream.DisposeAsync().ConfigureAwait(false);
                _disposed = true;
            }
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    private readonly struct AttachmentValidationSettings
    {
        public static AttachmentValidationSettings Disabled => new(false, null, 0, Array.Empty<string>(), Array.Empty<string>(), false);
        public static AttachmentValidationSettings Invalid => new(false, null, 0, Array.Empty<string>(), Array.Empty<string>(), false);

        public AttachmentValidationSettings(
            bool isValid,
            string? storageProfileId,
            long maxSizeBytes,
            IReadOnlyList<string> allowedContentTypes,
            IReadOnlyList<string> disallowedContentTypes,
            bool requireGlobalIds)
        {
            IsValid = isValid;
            StorageProfileId = storageProfileId;
            MaxSizeBytes = maxSizeBytes;
            AllowedContentTypes = allowedContentTypes is null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(allowedContentTypes, StringComparer.OrdinalIgnoreCase);
            DisallowedContentTypes = disallowedContentTypes is null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(disallowedContentTypes, StringComparer.OrdinalIgnoreCase);
            RequireGlobalIds = requireGlobalIds;
        }

        public bool IsValid { get; }
        public string? StorageProfileId { get; }
        public long MaxSizeBytes { get; }
        public HashSet<string> AllowedContentTypes { get; }
        public HashSet<string> DisallowedContentTypes { get; }
        public bool RequireGlobalIds { get; }
    }
}
