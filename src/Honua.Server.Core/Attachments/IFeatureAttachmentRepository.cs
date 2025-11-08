// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Contracts for persisting attachment metadata alongside feature records.
/// Provides CRUD operations and transactional support for feature attachments.
/// </summary>
public interface IFeatureAttachmentRepository
{
    /// <summary>
    /// Finds an attachment by its unique identifier.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="attachmentId">The unique attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The attachment descriptor if found, null otherwise.</returns>
    Task<AttachmentDescriptor?> FindByIdAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all attachments associated with a specific feature.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="featureId">The unique feature identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of attachment descriptors ordered by creation time.</returns>
    Task<IReadOnlyList<AttachmentDescriptor>> ListByFeatureAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch retrieves attachments for multiple features to avoid N+1 query patterns.
    /// Returns a dictionary mapping feature IDs to their attachment lists.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="featureIds">List of feature identifiers to retrieve attachments for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Dictionary mapping feature IDs to their attachment descriptors.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListByFeaturesAsync(string serviceId, string layerId, IReadOnlyList<string> featureIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new attachment descriptor in the repository.
    /// </summary>
    /// <param name="descriptor">The attachment descriptor to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The created attachment descriptor with generated identifier.</returns>
    Task<AttachmentDescriptor> CreateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing attachment descriptor.
    /// </summary>
    /// <param name="descriptor">The attachment descriptor with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated attachment descriptor if successful, null if not found.</returns>
    Task<AttachmentDescriptor?> UpdateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attachment descriptor from the repository.
    /// Note: This only removes the metadata; blob storage cleanup is handled separately.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="attachmentId">The unique attachment identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the attachment was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction for atomic attachment operations.
    /// Used to ensure consistency when creating/updating multiple attachments.
    /// </summary>
    /// <param name="serviceId">The unique service identifier.</param>
    /// <param name="layerId">The unique layer identifier within the service.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A transaction object that must be committed or disposed.</returns>
    Task<IFeatureAttachmentTransaction> BeginTransactionAsync(string serviceId, string layerId, CancellationToken cancellationToken = default);
}
