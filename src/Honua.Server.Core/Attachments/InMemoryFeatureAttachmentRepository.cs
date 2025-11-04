// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Data;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Attachments;

public sealed class InMemoryFeatureAttachmentRepository : InMemoryStoreBase<AttachmentDescriptor>, IFeatureAttachmentRepository
{
    private readonly ConcurrentDictionary<string, int> _attachmentCounters = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryFeatureAttachmentRepository()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Extracts the attachment key from an AttachmentDescriptor entity.
    /// </summary>
    protected override string GetKey(AttachmentDescriptor entity)
    {
        Guard.NotNull(entity);
        return BuildKey(entity.ServiceId, entity.LayerId, entity.AttachmentId);
    }

    public Task<AttachmentDescriptor?> FindByIdAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(serviceId, layerId, attachmentId);
        return GetAsync(key, cancellationToken);
    }

    public async Task<IReadOnlyList<AttachmentDescriptor>> ListByFeatureAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default)
    {
        var results = await QueryAsync(
            attachment => string.Equals(attachment.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(attachment.LayerId, layerId, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(attachment.FeatureId, featureId, StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        return results.OrderBy(attachment => attachment.CreatedUtc).ToList();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListByFeaturesAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(featureIds);

        if (featureIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase);
        }

        // Create a HashSet for efficient lookup
        var featureIdSet = new HashSet<string>(featureIds, StringComparer.OrdinalIgnoreCase);

        // Query all attachments for the service/layer matching any of the feature IDs
        var results = await QueryAsync(
            attachment => string.Equals(attachment.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(attachment.LayerId, layerId, StringComparison.OrdinalIgnoreCase)
                          && featureIdSet.Contains(attachment.FeatureId),
            cancellationToken);

        // Group by feature ID and sort by creation date
        var grouped = results
            .GroupBy(attachment => attachment.FeatureId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AttachmentDescriptor>)g.OrderBy(a => a.CreatedUtc).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return grouped;
    }

    public async Task<AttachmentDescriptor> CreateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(descriptor);

        var counterKey = $"{descriptor.ServiceId}:{descriptor.LayerId}";
        var nextId = _attachmentCounters.AddOrUpdate(counterKey, 1, static (_, current) => checked(current + 1));
        var finalDescriptor = descriptor.AttachmentObjectId != 0 ? descriptor : descriptor with { AttachmentObjectId = nextId };

        await PutAsync(finalDescriptor, cancellationToken);
        return finalDescriptor;
    }

    public async Task<AttachmentDescriptor?> UpdateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(descriptor);
        var key = BuildKey(descriptor.ServiceId, descriptor.LayerId, descriptor.AttachmentId);

        if (!await ExistsAsync(key, cancellationToken))
        {
            return null;
        }

        await PutAsync(descriptor, cancellationToken);
        return descriptor;
    }

    public Task<bool> DeleteAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(serviceId, layerId, attachmentId);
        return DeleteAsync(key, cancellationToken);
    }

    public Task<IFeatureAttachmentTransaction> BeginTransactionAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IFeatureAttachmentTransaction>(new PassthroughAttachmentTransaction(this, serviceId, layerId));
    }

    private static string BuildKey(string serviceId, string layerId, string attachmentId)
    {
        return $"{serviceId}:{layerId}:{attachmentId}";
    }

    private sealed class PassthroughAttachmentTransaction : IFeatureAttachmentTransaction
    {
        private readonly InMemoryFeatureAttachmentRepository _repository;
        private readonly string _serviceId;
        private readonly string _layerId;

        public PassthroughAttachmentTransaction(InMemoryFeatureAttachmentRepository repository, string serviceId, string layerId)
        {
            _repository = repository;
            _serviceId = serviceId;
            _layerId = layerId;
        }

        public Task<AttachmentDescriptor> CreateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            return _repository.CreateAsync(descriptor, cancellationToken);
        }

        public Task<AttachmentDescriptor?> UpdateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            return _repository.UpdateAsync(descriptor, cancellationToken);
        }

        public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
        {
            return _repository.DeleteAsync(_serviceId, _layerId, attachmentId, cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
