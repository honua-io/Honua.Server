// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Migration.GeoservicesRest;

namespace Honua.Server.Core.Migration;

public sealed class EsriServiceMigrationRequest
{
    public required Uri SourceServiceUri { get; init; }

    public required string TargetServiceId { get; init; }

    public required string TargetFolderId { get; init; }

    public required string TargetDataSourceId { get; init; }

    public IReadOnlyCollection<int>? LayerIds { get; init; }

    public bool IncludeData { get; init; } = true;

    public int? BatchSize { get; init; }

    public GeoservicesRestMetadataTranslatorOptions? TranslatorOptions { get; init; }

    /// <summary>
    /// Optional security profile ID for authenticated access to secured services.
    /// References a profile in ExternalServiceSecurityConfiguration.
    /// </summary>
    public string? SecurityProfileId { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(SourceServiceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(TargetServiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(TargetFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(TargetDataSourceId);

        if (BatchSize.HasValue && BatchSize.Value <= 0)
        {
            throw new ArgumentException("BatchSize must be greater than zero.", nameof(BatchSize));
        }
    }
}
