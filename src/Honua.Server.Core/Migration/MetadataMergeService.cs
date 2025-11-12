// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Migration.GeoservicesRest;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Migration;

public sealed class MetadataMergeService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriterOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHonuaConfigurationService _configurationService;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IMetadataSchemaValidator _schemaValidator;

    public MetadataMergeService(
        IHonuaConfigurationService configurationService,
        IMetadataRegistry metadataRegistry,
        IMetadataSchemaValidator schemaValidator)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
    }

    public async Task<MetadataSnapshot> AddServiceAsync(
        GeoservicesRestMigrationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // LEGACY CONFIGURATION SYSTEM - DEPRECATED
        // This method requires legacy metadata configuration
        if (_configurationService.Current.Metadata is null)
        {
            throw new InvalidOperationException(
                "Legacy metadata configuration is required for migration operations. " +
                "Please provide a 'metadata' section in your configuration with a valid provider (json/yaml) and path.");
        }

        var metadataPath = _configurationService.Current.Metadata.Path;
        if (string.IsNullOrWhiteSpace(metadataPath))
        {
            throw new InvalidOperationException("Metadata path is not configured.");
        }

        MetadataDocument document = await LoadDocumentAsync(metadataPath, cancellationToken).ConfigureAwait(false);

        document.Services ??= new List<ServiceDocument>();
        document.Layers ??= new List<LayerDocument>();

        EnsureServiceNotExists(document.Services, plan.ServiceDocument.Id);
        EnsureLayersNotExist(document.Layers, plan.LayerDocuments);

        document.Services.Add(plan.ServiceDocument);
        document.Layers.AddRange(plan.LayerDocuments);

        var payload = JsonSerializer.Serialize(document, WriterOptions);
        ValidatePayload(payload);

        await WriteMetadataAsync(metadataPath, payload, cancellationToken).ConfigureAwait(false);
        await _metadataRegistry.ReloadAsync(cancellationToken).ConfigureAwait(false);

        return await _metadataRegistry.GetSnapshotAsync(cancellationToken);
    }

    private async Task<MetadataDocument> LoadDocumentAsync(string metadataPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException($"Metadata file not found at '{metadataPath}'.", metadataPath);
        }

        await using var stream = File.OpenRead(metadataPath);
        var document = await JsonSerializer.DeserializeAsync<MetadataDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return document ?? new MetadataDocument();
    }

    private void ValidatePayload(string payload)
    {
        var schemaResult = _schemaValidator.Validate(payload);
        if (!schemaResult.IsValid)
        {
            var message = schemaResult.Errors.Count == 0
                ? "Metadata schema validation failed."
                : string.Join(Environment.NewLine, schemaResult.Errors);
            throw new InvalidDataException(message);
        }

        _ = JsonMetadataProvider.Parse(payload);
    }

    private static async Task WriteMetadataAsync(string metadataPath, string payload, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"honua-metadata-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(tempFile, payload, cancellationToken).ConfigureAwait(false);
            FilePermissionHelper.ApplyFilePermissions(tempFile);

            File.Copy(tempFile, metadataPath, overwrite: true);
            FilePermissionHelper.ApplyFilePermissions(metadataPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    private static void EnsureServiceNotExists(IReadOnlyCollection<ServiceDocument> services, string? serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new InvalidOperationException("Service id must be provided.");
        }

        var exists = services.Any(s => string.Equals(s?.Id, serviceId, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Service '{0}' already exists in metadata.", serviceId));
        }
    }

    private static void EnsureLayersNotExist(IReadOnlyCollection<LayerDocument> existingLayers, IReadOnlyList<LayerDocument> newLayers)
    {
        foreach (var layer in newLayers)
        {
            if (string.IsNullOrWhiteSpace(layer?.Id))
            {
                throw new InvalidOperationException("Layer id must be provided for all migrated layers.");
            }

            var duplicate = existingLayers.Any(existing => string.Equals(existing?.Id, layer.Id, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Layer '{0}' already exists in metadata.", layer.Id));
            }
        }
    }
}
