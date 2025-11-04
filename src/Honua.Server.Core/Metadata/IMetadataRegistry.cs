// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

public interface IMetadataRegistry
    {
        [Obsolete("Use GetSnapshotAsync() instead. This property uses blocking calls and will be removed in a future version.")]
        MetadataSnapshot Snapshot { get; }
        bool IsInitialized { get; }
        bool TryGetSnapshot(out MetadataSnapshot snapshot);
        ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
        Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
        Task ReloadAsync(CancellationToken cancellationToken = default);

        [Obsolete("Use UpdateAsync() instead. This method uses blocking calls and will be removed in a future version.")]
        void Update(MetadataSnapshot snapshot);

        Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default);
        IChangeToken GetChangeToken();
    }
