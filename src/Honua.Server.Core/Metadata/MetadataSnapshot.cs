// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Metadata;

public sealed class MetadataSnapshot
{
    private readonly MetadataIndexes _indexes;
    private readonly ILogger<MetadataSnapshot>? _logger;

    public MetadataSnapshot(
        CatalogDefinition catalog,
        IReadOnlyList<FolderDefinition> folders,
        IReadOnlyList<DataSourceDefinition> dataSources,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition>? rasterDatasets = null,
        IReadOnlyList<StyleDefinition>? styles = null,
        IReadOnlyList<LayerGroupDefinition>? layerGroups = null,
        ServerDefinition? server = null,
        ILogger<MetadataSnapshot>? logger = null)
    {
        _logger = logger;
        this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.Folders = folders ?? throw new ArgumentNullException(nameof(folders));
        this.DataSources = dataSources ?? throw new ArgumentNullException(nameof(dataSources));
        this.Layers = layers ?? throw new ArgumentNullException(nameof(layers));
        this.RasterDatasets = rasterDatasets ?? Array.Empty<RasterDatasetDefinition>();
        this.Styles = styles ?? Array.Empty<StyleDefinition>();
        this.LayerGroups = layerGroups ?? Array.Empty<LayerGroupDefinition>();
        this.Server = server ?? ServerDefinition.Default;

        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Validate all metadata using the orchestrator
        MetadataValidator.Validate(this.Catalog, this.Server, this.Folders, this.DataSources, services, this.Layers, this.RasterDatasets, this.Styles, this.LayerGroups, _logger);

        _indexes = MetadataIndexBuilder.Build(services, this.Layers, this.Styles, this.LayerGroups);
        this.Services = new ReadOnlyCollection<ServiceDefinition>(_indexes.ServiceIndex.Values.ToList());
    }

    public CatalogDefinition Catalog { get; }
    public IReadOnlyList<FolderDefinition> Folders { get; }
    public IReadOnlyList<DataSourceDefinition> DataSources { get; }
    public IReadOnlyList<ServiceDefinition> Services { get; }
    public IReadOnlyList<LayerDefinition> Layers { get; }
    public IReadOnlyList<RasterDatasetDefinition> RasterDatasets { get; }
    public IReadOnlyList<StyleDefinition> Styles { get; }
    public IReadOnlyList<LayerGroupDefinition> LayerGroups { get; }
    public ServerDefinition Server { get; }

    public ServiceDefinition GetService(string id) => _indexes.GetService(id);

    public bool TryGetService(string id, out ServiceDefinition service) => _indexes.TryGetService(id, out service);

    public bool TryGetLayer(string serviceId, string layerId, out LayerDefinition layer) => _indexes.TryGetLayer(serviceId, layerId, out layer);

    public bool TryGetStyle(string styleId, out StyleDefinition style) => _indexes.TryGetStyle(styleId, out style);

    public StyleDefinition GetStyle(string styleId) => _indexes.GetStyle(styleId);

    public bool TryGetLayerGroup(string serviceId, string layerGroupId, out LayerGroupDefinition layerGroup) => _indexes.TryGetLayerGroup(serviceId, layerGroupId, out layerGroup);

    public LayerGroupDefinition GetLayerGroup(string serviceId, string layerGroupId) => _indexes.GetLayerGroup(serviceId, layerGroupId);
}
