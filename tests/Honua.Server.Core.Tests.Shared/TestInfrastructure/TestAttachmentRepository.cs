using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;

namespace Honua.Server.Core.Tests.Shared;

public sealed class TestAttachmentRepository : IFeatureAttachmentRepository
{
    private readonly ConcurrentDictionary<string, AttachmentDescriptor> _attachments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);

    public void Reset()
    {
        _attachments.Clear();
        _counters.Clear();
    }

    public Task<AttachmentDescriptor?> FindByIdAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
    {
        _attachments.TryGetValue(BuildKey(serviceId, layerId, attachmentId), out var descriptor);
        return Task.FromResult(descriptor);
    }

    public Task<IReadOnlyList<AttachmentDescriptor>> ListByFeatureAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default)
    {
        var results = _attachments.Values
            .Where(descriptor => string.Equals(descriptor.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(descriptor.LayerId, layerId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(descriptor.FeatureId, featureId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(descriptor => descriptor.CreatedUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<AttachmentDescriptor>>(results);
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListByFeaturesAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        if (featureIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>>(
                new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase));
        }

        var featureIdSet = new HashSet<string>(featureIds, StringComparer.OrdinalIgnoreCase);
        var grouped = _attachments.Values
            .Where(descriptor => string.Equals(descriptor.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(descriptor.LayerId, layerId, StringComparison.OrdinalIgnoreCase)
                && featureIdSet.Contains(descriptor.FeatureId))
            .GroupBy(descriptor => descriptor.FeatureId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AttachmentDescriptor>)g.OrderBy(a => a.CreatedUtc).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>>(grouped);
    }

    public Task<AttachmentDescriptor> CreateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var finalDescriptor = EnsureAttachmentObjectId(descriptor);
        _attachments[BuildKey(finalDescriptor.ServiceId, finalDescriptor.LayerId, finalDescriptor.AttachmentId)] = finalDescriptor;
        return Task.FromResult(finalDescriptor);
    }

    public Task<AttachmentDescriptor?> UpdateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(descriptor.ServiceId, descriptor.LayerId, descriptor.AttachmentId);
        if (!_attachments.ContainsKey(key))
        {
            return Task.FromResult<AttachmentDescriptor?>(null);
        }

        var finalDescriptor = EnsureAttachmentObjectId(descriptor);
        _attachments[key] = finalDescriptor;
        return Task.FromResult<AttachmentDescriptor?>(finalDescriptor);
    }

    public Task<bool> DeleteAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_attachments.TryRemove(BuildKey(serviceId, layerId, attachmentId), out _));
    }

    public Task<IFeatureAttachmentTransaction> BeginTransactionAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IFeatureAttachmentTransaction>(new PassthroughTransaction(this, serviceId, layerId));
    }

    private AttachmentDescriptor EnsureAttachmentObjectId(AttachmentDescriptor descriptor)
    {
        if (descriptor.AttachmentObjectId > 0)
        {
            return descriptor;
        }

        var counterKey = BuildCounterKey(descriptor.ServiceId, descriptor.LayerId, descriptor.FeatureId);
        var nextId = _counters.AddOrUpdate(counterKey, 1, static (_, current) => checked(current + 1));
        return descriptor with { AttachmentObjectId = nextId };
    }

    private static string BuildKey(string serviceId, string layerId, string attachmentId)
    {
        return $"{serviceId}:{layerId}:{attachmentId}";
    }

    private static string BuildCounterKey(string serviceId, string layerId, string featureId)
    {
        return $"{serviceId}:{layerId}:{featureId}";
    }

    private sealed class PassthroughTransaction : IFeatureAttachmentTransaction
    {
        private readonly TestAttachmentRepository _repository;
        private readonly string _serviceId;
        private readonly string _layerId;

        public PassthroughTransaction(TestAttachmentRepository repository, string serviceId, string layerId)
        {
            _repository = repository;
            _serviceId = serviceId;
            _layerId = layerId;
        }

        public Task<AttachmentDescriptor> CreateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            return _repository.CreateAsync(descriptor with { ServiceId = _serviceId, LayerId = _layerId }, cancellationToken);
        }

        public Task<AttachmentDescriptor?> UpdateAsync(AttachmentDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            return _repository.UpdateAsync(descriptor with { ServiceId = _serviceId, LayerId = _layerId }, cancellationToken);
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
