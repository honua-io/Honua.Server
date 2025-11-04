// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRESTMigrationService
{
    private readonly IGeoservicesRestServiceClient _esriClient;
    private readonly MetadataMergeService _metadataMergeService;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly ILogger<GeoservicesRESTMigrationService> _logger;

    public GeoservicesRESTMigrationService(
        IGeoservicesRestServiceClient esriClient,
        MetadataMergeService metadataMergeService,
        IMetadataRegistry metadataRegistry,
        IHonuaConfigurationService configurationService,
        ILogger<GeoservicesRESTMigrationService> logger)
    {
        _esriClient = esriClient ?? throw new ArgumentNullException(nameof(esriClient));
        _metadataMergeService = metadataMergeService ?? throw new ArgumentNullException(nameof(metadataMergeService));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GeoservicesRestMigrationResult> MigrateAsync(
        GeoservicesRestServiceMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        EnsureFolderExists(snapshot, request.TargetFolderId);
        EnsureDataSourceExists(snapshot, request.TargetDataSourceId);

        var token = ResolveSecurityToken(request.SecurityProfileId);

        var serviceInfo = await _esriClient.GetServiceAsync(request.ServiceUri, token, cancellationToken).ConfigureAwait(false);

        var layerIds = request.LayerIds?.ToHashSet() ?? serviceInfo.Layers.Select(layer => layer.Id).ToHashSet();
        if (layerIds.Count == 0)
        {
            throw new InvalidOperationException("No layers were selected for migration.");
        }

        var layerInfos = new List<GeoservicesRestLayerInfo>(layerIds.Count);
        foreach (var layerId in layerIds)
        {
            var layerUri = new Uri(request.ServiceUri, $"{layerId}");
            var layerInfo = await _esriClient.GetLayerAsync(layerUri, token, cancellationToken).ConfigureAwait(false);
            layerInfos.Add(layerInfo);
        }

        var translator = new GeoservicesRestMetadataTranslator(request.TranslatorOptions);
        var plan = translator.Translate(
            request.ServiceUri,
            request.TargetServiceId,
            request.TargetFolderId,
            request.TargetDataSourceId,
            serviceInfo,
            layerInfos,
            layerIds);

        var mergedSnapshot = await _metadataMergeService.AddServiceAsync(plan, cancellationToken).ConfigureAwait(false);

        return new GeoservicesRestMigrationResult
        {
            Plan = plan,
            Snapshot = mergedSnapshot
        };
    }

    private static void EnsureFolderExists(MetadataSnapshot snapshot, string folderId)
    {
        if (!snapshot.Folders.Any(folder => string.Equals(folder.Id, folderId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Folder '{folderId}' does not exist in metadata.");
        }
    }

    private static void EnsureDataSourceExists(MetadataSnapshot snapshot, string dataSourceId)
    {
        if (!snapshot.DataSources.Any(source => string.Equals(source.Id, dataSourceId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Data source '{dataSourceId}' does not exist in metadata.");
        }
    }

    private string? ResolveSecurityToken(string? securityProfileId)
    {
        if (string.IsNullOrWhiteSpace(securityProfileId))
        {
            return null;
        }

        var config = _configurationService.Current;
        if (!config.ExternalServiceSecurity.Profiles.TryGetValue(securityProfileId, out var profile))
        {
            _logger.LogWarning("Security profile '{ProfileId}' not found in configuration", securityProfileId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Token))
        {
            _logger.LogWarning("Security profile '{ProfileId}' has no token configured", securityProfileId);
            return null;
        }

        return profile.Token;
    }
}
