// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Contracts for persisting attachment metadata alongside feature records.
/// </summary>
public interface IFeatureAttachmentRepository
{
    Task<AttachmentDescriptor?> FindByIdAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttachmentDescriptor>> ListByFeatureAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch retrieves attachments for multiple features to avoid N+1 query patterns.
    /// Returns a dictionary mapping feature IDs to their attachment lists.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListByFeaturesAsync(string serviceId, string layerId, IReadOnlyList<string> featureIds, CancellationToken cancellationToken = default);

    Task<AttachmentDescriptor> CreateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default);
    Task<AttachmentDescriptor?> UpdateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default);
    Task<IFeatureAttachmentTransaction> BeginTransactionAsync(string serviceId, string layerId, CancellationToken cancellationToken = default);
}
