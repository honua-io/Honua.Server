// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Host.Wfs;

internal interface IWfsLockManager
{
    Task<WfsLockAcquisitionResult> TryAcquireAsync(string owner, TimeSpan duration, IReadOnlyCollection<WfsLockTarget> targets, CancellationToken cancellationToken);

    Task<WfsLockValidationResult> ValidateAsync(string? lockId, IReadOnlyCollection<WfsLockTarget> targets, CancellationToken cancellationToken);

    Task ReleaseAsync(string requestingUser, string lockId, IReadOnlyCollection<WfsLockTarget>? targets, CancellationToken cancellationToken);

    Task ResetAsync();
}

internal sealed record WfsLockTarget(string ServiceId, string LayerId, string FeatureId);

internal sealed record WfsLockAcquisition(string LockId, string Owner, DateTimeOffset ExpiresAt, IReadOnlyList<WfsLockTarget> Targets);

internal sealed record WfsLockAcquisitionResult(bool Success, WfsLockAcquisition? Lock, string? Error);

internal sealed record WfsLockValidationResult(bool Success, string? ErrorMessage);
