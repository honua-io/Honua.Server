// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// No-op metadata provider used when legacy configuration is not present.
/// This allows Configuration V2-only deployments to work without legacy metadata.json files.
/// Throws NotSupportedException when accessed to make it clear that legacy configuration is required
/// for endpoints that still depend on it.
/// </summary>
public sealed class NoOpMetadataProvider : IMetadataProvider
{
    public bool SupportsChangeNotifications => false;

    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged
    {
        add { }
        remove { }
    }

    public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Legacy metadata configuration is not available. " +
            "This deployment is using Configuration V2 (.hcl files). " +
            "To use legacy endpoints, provide a 'metadata' section in your configuration with a valid provider (json/yaml) and path.");
    }
}
