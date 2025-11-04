// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public interface IGeoservicesRestMigrationService
{
    Task<GeoservicesRestMigrationJobSnapshot> EnqueueAsync(GeoservicesRestMigrationPlan plan, CancellationToken cancellationToken = default);

    Task<GeoservicesRestMigrationJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeoservicesRestMigrationJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default);

    Task<GeoservicesRestMigrationJobSnapshot?> CancelAsync(Guid jobId, string? reason = null);
}
