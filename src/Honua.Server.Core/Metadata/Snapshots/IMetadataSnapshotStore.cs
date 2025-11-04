// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Metadata.Snapshots;

public interface IMetadataSnapshotStore
{
    Task<MetadataSnapshotDescriptor> CreateAsync(MetadataSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetadataSnapshotDescriptor>> ListAsync(CancellationToken cancellationToken = default);
    Task<MetadataSnapshotDetails?> GetAsync(string label, CancellationToken cancellationToken = default);
    Task RestoreAsync(string label, CancellationToken cancellationToken = default);
}
