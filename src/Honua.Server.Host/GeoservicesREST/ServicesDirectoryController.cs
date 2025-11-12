// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Configuration.V2;
using System.Collections.ObjectModel;
using Honua.Server.Core.Configuration.V2;
using System.Linq;
using Honua.Server.Core.Configuration.V2;
using System.Text;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Host.Extensions;
using Honua.Server.Core.Configuration.V2;
using Microsoft.AspNetCore.Authorization;
using Honua.Server.Core.Configuration.V2;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Configuration.V2;

namespace Honua.Server.Host.GeoservicesREST;

[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("rest/services")]
public sealed class ServicesDirectoryController : ControllerBase
{
    private const double GeoServicesVersion = 10.81;
    private const int DefaultMaxRecordCount = 1000;
    private readonly ICatalogProjectionService _catalog;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly HonuaConfig? _honuaConfig;

    public ServicesDirectoryController(ICatalogProjectionService catalog, IMetadataRegistry metadataRegistry, HonuaConfig? honuaConfig = null)
    {
        _catalog = Guard.NotNull(catalog);
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _honuaConfig = honuaConfig;
    }

    [HttpGet]
    public async Task<ActionResult<ServicesDirectoryResponse>> GetRoot(CancellationToken cancellationToken = default)
    {
        return await ActivityScope.ExecuteAsync<ActionResult<ServicesDirectoryResponse>>(
            HonuaTelemetry.Metadata,
            "ArcGIS ServicesDirectory GetRoot",
            [("arcgis.operation", "GetRoot")],
            async activity =>
            {
                var snapshot = _catalog.GetSnapshot();
                var rasterServices = await ResolveRasterServicesAsync(cancellationToken).ConfigureAwait(false);
                var urlFactory = new Func<string?, string, string, string>(BuildServiceUrl);
                var folders = snapshot.Groups.Select(group => group.Id).ToArray();
                var services = snapshot.Groups
                    .SelectMany(group => group.Services.SelectMany(service => CreateRootEntries(service, rasterServices, urlFactory)))
                    .ToList();

                var geometryEnabled = _honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
                    ? geometryService.Enabled
                    : true;
                if (geometryEnabled)
                {
                    services.Add(new ServiceDirectoryEntry
                    {
                        Name = "Geometry",
                        Type = "GeometryServer",
                        Title = "Geometry",
                        Url = BuildGeometryServiceUrl()
                    });
                }

                return Ok(new ServicesDirectoryResponse
                {
                    CurrentVersion = GeoServicesVersion,
                    Folders = folders,
                    Services = services
                });
            }).ConfigureAwait(false);
    }

    [HttpGet("{folderId}")]
    public async Task<ActionResult<FolderDirectoryResponse>> GetFolder(string folderId, CancellationToken cancellationToken = default)
    {
        return await ActivityScope.ExecuteAsync<ActionResult<FolderDirectoryResponse>>(
            HonuaTelemetry.Metadata,
            "ArcGIS ServicesDirectory GetFolder",
            [
                ("arcgis.operation", "GetFolder"),
                ("arcgis.folder_id", folderId)
            ],
            async activity =>
            {
                var group = _catalog.GetGroup(folderId);
                if (group is null)
                {
                    return NotFound();
                }

                var rasterServices = await ResolveRasterServicesAsync(cancellationToken).ConfigureAwait(false);
                var urlFactory = new Func<string?, string, string, string>(BuildServiceUrl);
                var services = group.Services
                    .SelectMany(service => CreateFolderEntries(service, rasterServices, urlFactory))
                    .ToArray();

                return Ok(new FolderDirectoryResponse
                {
                    CurrentVersion = GeoServicesVersion,
                    FolderName = group.Title,
                    Services = services
                });
            }).ConfigureAwait(false);
    }

    private static IEnumerable<ServiceDirectoryEntry> CreateRootEntries(
        CatalogServiceView serviceView,
        ISet<string> rasterServices,
        Func<string?, string, string, string> urlFactory)
    {
        if (SupportsFeatureServer(serviceView.Service))
        {
            yield return new ServiceDirectoryEntry
            {
                Name = $"{serviceView.Service.FolderId}/{serviceView.Service.Id}",
                Type = "FeatureServer",
                Title = serviceView.Service.Title,
                Url = urlFactory(serviceView.Service.FolderId, serviceView.Service.Id, "FeatureServer")
            };
        }

        if (SupportsMapServer(serviceView.Service))
        {
            yield return new ServiceDirectoryEntry
            {
                Name = $"{serviceView.Service.FolderId}/{serviceView.Service.Id}",
                Type = "MapServer",
                Title = serviceView.Service.Title,
                Url = urlFactory(serviceView.Service.FolderId, serviceView.Service.Id, "MapServer")
            };
        }

        if (rasterServices.Contains(serviceView.Service.Id))
        {
            yield return new ServiceDirectoryEntry
            {
                Name = $"{serviceView.Service.FolderId}/{serviceView.Service.Id}",
                Type = "ImageServer",
                Title = serviceView.Service.Title,
                Url = urlFactory(serviceView.Service.FolderId, serviceView.Service.Id, "ImageServer")
            };
        }
    }

    private static IEnumerable<ServiceDirectoryEntry> CreateFolderEntries(
        CatalogServiceView serviceView,
        ISet<string> rasterServices,
        Func<string?, string, string, string> urlFactory)
    {
        if (SupportsFeatureServer(serviceView.Service))
        {
            yield return new ServiceDirectoryEntry
            {
                Name = serviceView.Service.Id,
                Type = "FeatureServer",
                Title = serviceView.Service.Title,
                Url = urlFactory(serviceView.Service.FolderId, serviceView.Service.Id, "FeatureServer")
            };
        }

        if (SupportsMapServer(serviceView.Service))
        {
            yield return new ServiceDirectoryEntry
            {
                Name = serviceView.Service.Id,
                Type = "MapServer",
                Title = serviceView.Service.Title,
                Url = urlFactory(serviceView.Service.FolderId, serviceView.Service.Id, "MapServer")
            };
        }

        if (rasterServices.Contains(serviceView.Service.Id))
        {
            yield return new ServiceDirectoryEntry
            {
                Name = serviceView.Service.Id,
                Type = "ImageServer",
                Title = serviceView.Service.Title,
                Url = urlFactory(serviceView.Service.FolderId, serviceView.Service.Id, "ImageServer")
            };
        }
    }

    private string BuildServiceUrl(string? folderId, string serviceId, string serviceType)
    {
        var builder = new StringBuilder();
        builder.Append(Request.Scheme)
            .Append("://")
            .Append(Request.Host);

        if (Request.PathBase.HasValue)
        {
            builder.Append(Request.PathBase.Value);
        }

        builder.Append("/rest/services");

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            builder.Append('/')
                .Append(Uri.EscapeDataString(folderId));
        }

        builder.Append('/')
            .Append(Uri.EscapeDataString(serviceId))
            .Append('/')
            .Append(serviceType);

        return builder.ToString();
    }

    private string BuildGeometryServiceUrl()
    {
        var builder = new StringBuilder();
        builder.Append(Request.Scheme)
            .Append("://")
            .Append(Request.Host);

        if (Request.PathBase.HasValue)
        {
            builder.Append(Request.PathBase.Value);
        }

        builder.Append("/rest/services/Geometry/GeometryServer");
        return builder.ToString();
    }

    private async Task<HashSet<string>> ResolveRasterServicesAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataset in snapshot.RasterDatasets)
        {
            if (!string.IsNullOrWhiteSpace(dataset.ServiceId))
            {
                services.Add(dataset.ServiceId);
            }
        }

        return services;
    }

    private static bool SupportsFeatureServer(ServiceDefinition service)
    {
        return service.ServiceType.EqualsIgnoreCase("FeatureServer")
            || service.ServiceType.EqualsIgnoreCase("feature");
    }

    private static bool SupportsMapServer(ServiceDefinition service)
    {
        return service.ServiceType.EqualsIgnoreCase("FeatureServer")
            || service.ServiceType.EqualsIgnoreCase("feature")
            || service.ServiceType.EqualsIgnoreCase("MapServer")
            || service.ServiceType.EqualsIgnoreCase("map");
    }

    private static int ResolveMaxRecordCount(CatalogServiceView service)
    {
        var serviceLimit = service.Service.Ogc.ItemLimit;
        var layerLimit = service.Layers
            .Select(l => l.Layer.Query.MaxRecordCount)
            .Where(limit => limit.HasValue)
            .Select(limit => limit!.Value)
            .DefaultIfEmpty()
            .Max();

        if (layerLimit > 0 && serviceLimit is null)
        {
            return layerLimit;
        }

        if (serviceLimit is null)
        {
            return layerLimit > 0 ? layerLimit : DefaultMaxRecordCount;
        }

        return layerLimit > 0 ? Math.Min(serviceLimit.Value, layerLimit) : serviceLimit.Value;
    }
}
