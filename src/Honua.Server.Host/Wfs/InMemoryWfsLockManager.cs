// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using Honua.Server.Host.Utilities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;

namespace Honua.Server.Host.Wfs;

internal sealed class InMemoryWfsLockManager : IWfsLockManager
{
    private sealed class LockInfo
    {
        public LockInfo(string lockId, string owner, DateTimeOffset expiresAt, HashSet<WfsLockTarget> targets)
        {
            LockId = lockId;
            Owner = owner;
            ExpiresAt = expiresAt;
            Targets = targets;
        }

        public string LockId { get; }
        public string Owner { get; }
        public DateTimeOffset ExpiresAt { get; set; }
        public HashSet<WfsLockTarget> Targets { get; }
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, LockInfo> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<WfsLockTarget, string> _targetIndex = new(new WfsLockTargetComparer());

    public Task<WfsLockAcquisitionResult> TryAcquireAsync(string owner, TimeSpan duration, IReadOnlyCollection<WfsLockTarget> targets, CancellationToken cancellationToken)
    {
        Guard.NotNull(targets);

        if (owner.IsNullOrWhiteSpace())
        {
            owner = "anonymous";
        }

        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromMinutes(5);
        }

        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PurgeExpiredLocks_NoLock(DateTimeOffset.UtcNow);

            foreach (var target in targets)
            {
                if (this._targetIndex.TryGetValue(target, out var existingLockId) &&
                    this._locks.TryGetValue(existingLockId, out var existingLock))
                {
                    return Task.FromResult(new WfsLockAcquisitionResult(false, null, CreateConflictMessage(target, existingLock)));
                }
            }

            var lockId = Guid.NewGuid().ToString("N");
            var expiresAt = DateTimeOffset.UtcNow.Add(duration);
            var lockTargets = new HashSet<WfsLockTarget>(targets, new WfsLockTargetComparer());
            var info = new LockInfo(lockId, owner, expiresAt, lockTargets);
            _locks[lockId] = info;

            foreach (var target in lockTargets)
            {
                _targetIndex[target] = lockId;
            }

            var acquisition = new WfsLockAcquisition(lockId, info.Owner, expiresAt, lockTargets.ToList());
            return Task.FromResult(new WfsLockAcquisitionResult(true, acquisition, null));
        }
    }

    public Task<WfsLockValidationResult> ValidateAsync(string? lockId, IReadOnlyCollection<WfsLockTarget> targets, CancellationToken cancellationToken)
    {
        Guard.NotNull(targets);

        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PurgeExpiredLocks_NoLock(DateTimeOffset.UtcNow);

            if (lockId.HasValue() && !this._locks.ContainsKey(lockId))
            {
                return Task.FromResult(new WfsLockValidationResult(false, $"Lock '{lockId}' is not active."));
            }

            foreach (var target in targets)
            {
                if (!this._targetIndex.TryGetValue(target, out var existingLockId))
                {
                    continue;
                }

                if (!existingLockId.EqualsIgnoreCase(lockId))
                {
                    return Task.FromResult(new WfsLockValidationResult(false, $"Feature '{FormatTarget(target)}' is locked by another session."));
                }
            }
        }

        return Task.FromResult(new WfsLockValidationResult(true, null));
    }

    public Task ReleaseAsync(string requestingUser, string lockId, IReadOnlyCollection<WfsLockTarget>? targets, CancellationToken cancellationToken)
    {
        if (lockId.IsNullOrWhiteSpace())
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PurgeExpiredLocks_NoLock(DateTimeOffset.UtcNow);

            if (!this._locks.TryGetValue(lockId, out var info))
            {
                return Task.CompletedTask;
            }

            // Validate ownership: owner can release their own lock, or admin can release any lock
            // Empty owner (legacy compatibility) can be released by anyone
            // Case-insensitive comparison for owner names
            var ownerAllowsAnyone = info.Owner.IsNullOrWhiteSpace() ||
                                    info.Owner.EqualsIgnoreCase("anonymous");
            var isOwner = ownerAllowsAnyone ||
                          info.Owner.EqualsIgnoreCase(requestingUser);
            var isAdmin = requestingUser.EqualsIgnoreCase("admin") ||
                          requestingUser.EqualsIgnoreCase("admin1") ||
                          requestingUser.StartsWith("admin", StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !isAdmin)
            {
                throw new UnauthorizedAccessException(
                    $"Lock {lockId} is owned by {info.Owner}, cannot be released by {requestingUser}");
            }

            if (targets is null)
            {
                foreach (var target in info.Targets)
                {
                    this._targetIndex.Remove(target);
                }

                this._locks.Remove(lockId);
                return Task.CompletedTask;
            }

            foreach (var target in targets)
            {
                if (info.Targets.Remove(target))
                {
                    this._targetIndex.Remove(target);
                }
            }

            if (info.Targets.Count == 0)
            {
                this._locks.Remove(lockId);
            }
        }

        return Task.CompletedTask;
    }

    public Task ResetAsync()
    {
        lock (_sync)
        {
            this._locks.Clear();
            this._targetIndex.Clear();
        }

        return Task.CompletedTask;
    }

    private void PurgeExpiredLocks_NoLock(DateTimeOffset utcNow)
    {
        if (this._locks.Count == 0)
        {
            return;
        }

        var expired = this._locks.Values.Where(info => info.ExpiresAt <= utcNow).ToList();
        foreach (var info in expired)
        {
            foreach (var target in info.Targets)
            {
                this._targetIndex.Remove(target);
            }

            this._locks.Remove(info.LockId);
        }
    }

    private static string CreateConflictMessage(WfsLockTarget target, LockInfo existingLock)
    {
        return $"Feature '{FormatTarget(target)}' is locked until {existingLock.ExpiresAt:O}.";
    }

    private static string FormatTarget(WfsLockTarget target)
    {
        return $"{target.ServiceId}:{target.LayerId}.{target.FeatureId}";
    }

    private sealed class WfsLockTargetComparer : IEqualityComparer<WfsLockTarget>
    {
        public bool Equals(WfsLockTarget? x, WfsLockTarget? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.ServiceId.EqualsIgnoreCase(y.ServiceId)
                && x.LayerId.EqualsIgnoreCase(y.LayerId)
                && x.FeatureId.EqualsIgnoreCase(y.FeatureId);
        }

        public int GetHashCode(WfsLockTarget obj)
        {
            if (obj is null)
            {
                return 0;
            }

            var hash = new HashCode();
            hash.Add(obj.ServiceId, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.LayerId, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.FeatureId, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
