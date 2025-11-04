// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Extension methods for IMetadataRegistry to reduce boilerplate in OGC API handlers.
/// </summary>
public static class OgcMetadataExtensions
{
    /// <summary>
    /// Ensures the metadata registry is initialized and returns the current snapshot.
    /// This combines EnsureInitializedAsync + GetSnapshotAsync into a single call.
    /// </summary>
    public static async Task<MetadataSnapshot> GetInitializedSnapshotAsync(
        this IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(metadataRegistry);

        await metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }
}
