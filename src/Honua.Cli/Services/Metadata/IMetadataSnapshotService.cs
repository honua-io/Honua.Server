// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.Metadata;

public interface IMetadataSnapshotService
{
    Task<MetadataSnapshotResult> CreateSnapshotAsync(MetadataSnapshotRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MetadataSnapshotDescriptor>> ListSnapshotsAsync(string? snapshotsRootOverride, CancellationToken cancellationToken);
    Task RestoreSnapshotAsync(MetadataRestoreRequest request, CancellationToken cancellationToken);
    Task<MetadataValidationResult> ValidateAsync(MetadataValidationRequest request, CancellationToken cancellationToken);
}
