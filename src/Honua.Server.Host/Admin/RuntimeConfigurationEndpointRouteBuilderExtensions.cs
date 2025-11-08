// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.OData;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Provides API endpoints for runtime configuration queries and service-level metadata toggle.
/// Global protocol settings are read-only and must be configured in appsettings.json.
/// Service-level API flags can be toggled at runtime.
/// </summary>
internal static class RuntimeConfigurationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all runtime configuration management endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The route group builder for additional configuration.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Viewing overall configuration status across all services
    /// - Listing global service protocol states
    /// - Toggling global protocol enablement (master kill switch)
    /// - Viewing service-level API configuration
    /// - Toggling service-level protocol enablement
    ///
    /// Note: Global settings configured in appsettings.json act as master switches.
    /// Service-level flags are overridden when global flags are disabled.
    /// </remarks>
    /// <example>
    /// Example request to toggle global WFS:
    /// <code>
    /// PATCH /admin/config/services/wfs
    /// {
    ///   "enabled": false
    /// }
    /// </code>
    /// </example>
    public static RouteGroupBuilder MapRuntimeConfiguration(this WebApplication app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/config");
        return MapRuntimeConfigurationCore(group, app.Services);
    }

    public static RouteGroupBuilder MapRuntimeConfiguration(this RouteGroupBuilder group)
    {
        Guard.NotNull(group);

        var services = ((IEndpointRouteBuilder)group).ServiceProvider;
        return MapRuntimeConfigurationCore(group.MapGroup("/admin/config"), services);
    }

    private static RouteGroupBuilder MapRuntimeConfigurationCore(RouteGroupBuilder group, IServiceProvider services)
    {
        var authOptions = services.GetRequiredService<IOptions<HonuaAuthenticationOptions>>().Value;
        var quickStartMode = authOptions.Mode == HonuaAuthenticationOptions.AuthenticationMode.QuickStart;
        if (!quickStartMode)
        {
            group.RequireAuthorization("RequireAdministrator");
        }

        // GET /admin/config/status - Overall configuration status
        group.MapGet("/status", async (
            HonuaConfigurationService configService,
            IMetadataRegistry metadataRegistry) =>
        {
            await metadataRegistry.EnsureInitializedAsync(default).ConfigureAwait(false);
            var snapshot = await metadataRegistry.GetSnapshotAsync(default).ConfigureAwait(false);
            var globalConfig = configService.Current.Services;

            var serviceStatus = snapshot.Services.Select(service =>
            {
                var ogc = service.Ogc;
                return new
                {
                    serviceId = service.Id,
                    apis = new
                    {
                        collections = new
                        {
                            serviceLevel = ogc.CollectionsEnabled,
                            globalLevel = true, // Collections don't have global toggle
                            effective = ogc.CollectionsEnabled
                        },
                        wfs = new
                        {
                            serviceLevel = ogc.WfsEnabled,
                            globalLevel = globalConfig.Wfs.Enabled,
                            effective = ogc.WfsEnabled && globalConfig.Wfs.Enabled
                        },
                        wms = new
                        {
                            serviceLevel = ogc.WmsEnabled,
                            globalLevel = globalConfig.Wms.Enabled,
                            effective = ogc.WmsEnabled && globalConfig.Wms.Enabled
                        },
                        wmts = new
                        {
                            serviceLevel = ogc.WmtsEnabled,
                            globalLevel = globalConfig.Wmts.Enabled,
                            effective = ogc.WmtsEnabled && globalConfig.Wmts.Enabled
                        },
                        csw = new
                        {
                            serviceLevel = ogc.CswEnabled,
                            globalLevel = globalConfig.Csw.Enabled,
                            effective = ogc.CswEnabled && globalConfig.Csw.Enabled
                        },
                        wcs = new
                        {
                            serviceLevel = ogc.WcsEnabled,
                            globalLevel = globalConfig.Wcs.Enabled,
                            effective = ogc.WcsEnabled && globalConfig.Wcs.Enabled
                        }
                    }
                };
            }).ToList();

            return Results.Ok(new
            {
                global = new
                {
                    wfs = globalConfig.Wfs.Enabled,
                    wms = globalConfig.Wms.Enabled,
                    wmts = globalConfig.Wmts.Enabled,
                    csw = globalConfig.Csw.Enabled,
                    wcs = globalConfig.Wcs.Enabled,
                    stac = globalConfig.Stac.Enabled,
                    geometry = globalConfig.Geometry.Enabled,
                    rasterTiles = globalConfig.RasterTiles.Enabled,
                    note = "Global settings are configured in appsettings.json and are read-only at runtime."
                },
                services = serviceStatus
            });
        });

        // GET /admin/config/services - List all global service states
        group.MapGet("/services", ([FromServices] HonuaConfigurationService configService) =>
        {
            var config = configService.Current.Services;
            return Results.Ok(new
            {
                wfs = new { enabled = config.Wfs.Enabled },
                wms = new { enabled = config.Wms.Enabled },
                wmts = new { enabled = config.Wmts.Enabled },
                csw = new { enabled = config.Csw.Enabled },
                wcs = new { enabled = config.Wcs.Enabled },
                stac = new { enabled = config.Stac.Enabled },
                geometry = new { enabled = config.Geometry.Enabled },
                rasterTiles = new { enabled = config.RasterTiles.Enabled },
                note = "These global settings can be toggled at runtime. When disabled, the protocol is disabled for ALL services regardless of service-level settings."
            });
        });

        // PATCH /admin/config/services/{protocol} - Toggle global protocol (master kill switch)
        group.MapPatch("/services/{protocol}", async (
            string protocol,
            ToggleRequest request,
            [FromServices] HonuaConfigurationService configService,
            [FromServices] IMetadataRegistry metadataRegistry) =>
        {
            if (quickStartMode)
            {
                return ApiErrorResponse.Json.Forbidden("Global service configuration changes are disabled in QuickStart mode.");
            }

            var normalizedProtocol = protocol.ToLowerInvariant();
            var currentConfig = configService.Current;
            var currentServices = currentConfig.Services;

            // Create new configuration with updated protocol
            ServicesConfiguration? newServices = normalizedProtocol switch
            {
                "wfs" => CloneServicesConfiguration(currentServices, wfs: new WfsConfiguration { Enabled = request.Enabled }),
                "wms" => CloneServicesConfiguration(currentServices, wms: new WmsConfiguration { Enabled = request.Enabled }),
                "wmts" => CloneServicesConfiguration(currentServices, wmts: new WmtsConfiguration { Enabled = request.Enabled }),
                "csw" => CloneServicesConfiguration(currentServices, csw: new CswConfiguration { Enabled = request.Enabled }),
                "wcs" => CloneServicesConfiguration(currentServices, wcs: new WcsConfiguration { Enabled = request.Enabled }),
                "stac" => CloneServicesConfiguration(currentServices, stac: new StacCatalogConfiguration { Enabled = request.Enabled }),
                "geometry" => CloneServicesConfiguration(currentServices, geometry: new GeometryServiceConfiguration
                {
                    Enabled = request.Enabled,
                    MaxGeometries = currentServices.Geometry.MaxGeometries,
                    MaxCoordinateCount = currentServices.Geometry.MaxCoordinateCount,
                    AllowedSrids = currentServices.Geometry.AllowedSrids,
                    EnableGdalOperations = currentServices.Geometry.EnableGdalOperations
                }),
                "rastertiles" or "raster-tiles" => CloneServicesConfiguration(currentServices, rasterTiles: new RasterTileCacheConfiguration
                {
                    Enabled = request.Enabled,
                    Provider = currentServices.RasterTiles.Provider,
                    FileSystem = currentServices.RasterTiles.FileSystem,
                    S3 = currentServices.RasterTiles.S3,
                    Azure = currentServices.RasterTiles.Azure,
                    Preseed = currentServices.RasterTiles.Preseed
                }),
                _ => null
            };

            if (newServices is null)
            {
                return ApiErrorResponse.Json.BadRequestResult($"Unknown protocol: {protocol}. Valid values: wfs, wms, wmts, csw, wcs, stac, geometry, rasterTiles");
            }

            var newConfig = CloneConfiguration(currentConfig, newServices);

            // When disabling globally, validate we're not breaking service-level promises
            List<string>? affectedServices = null;
            if (!request.Enabled)
            {
                await metadataRegistry.EnsureInitializedAsync(default).ConfigureAwait(false);
                var snapshot = await metadataRegistry.GetSnapshotAsync(default).ConfigureAwait(false);
                affectedServices = snapshot.Services.Where(s =>
                {
                    var ogc = s.Ogc;
                    return normalizedProtocol switch
                    {
                        "wfs" => ogc.WfsEnabled,
                        "wms" => ogc.WmsEnabled,
                        "wmts" => ogc.WmtsEnabled,
                        "csw" => ogc.CswEnabled,
                        "wcs" => ogc.WcsEnabled,
                        _ => false
                    };
                }).Select(s => s.Id).ToList();
            }

            configService.Update(newConfig);

            if (affectedServices is { Count: > 0 })
            {
                return Results.Ok(new
                {
                    status = "updated",
                    protocol = normalizedProtocol,
                    enabled = request.Enabled,
                    message = $"{protocol.ToUpperInvariant()} globally disabled. This will block {affectedServices.Count} service(s) that have {protocol} enabled at service-level.",
                    affectedServices,
                    note = "The protocol is now disabled globally. Service-level flags remain unchanged but are overridden by this global setting."
                });
            }

            return Results.Ok(new
            {
                status = "updated",
                protocol = normalizedProtocol,
                enabled = request.Enabled,
                message = $"{protocol.ToUpperInvariant()} {(request.Enabled ? "enabled" : "disabled")} globally.",
                note = request.Enabled
                    ? "Services with this protocol enabled at service-level can now serve requests."
                    : "ALL services are now blocked from serving this protocol, regardless of service-level settings."
            });
        });

        // GET /admin/config/services/{serviceId} - Get service-level API configuration
        group.MapGet("/services/{serviceId}", async (
            string serviceId,
            [FromServices] IMetadataRegistry metadataRegistry,
            [FromServices] HonuaConfigurationService configService) =>
        {
            await metadataRegistry.EnsureInitializedAsync(default).ConfigureAwait(false);
            var snapshot = await metadataRegistry.GetSnapshotAsync(default).ConfigureAwait(false);
            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase));

            if (service == null)
            {
                return GeoservicesRESTErrorHelper.NotFound("Service", serviceId);
            }

            var globalConfig = configService.Current.Services;
            var ogc = service.Ogc;

            return Results.Ok(new
            {
                serviceId = service.Id,
                apis = new
                {
                    collections = new
                    {
                        enabled = ogc.CollectionsEnabled,
                        effective = ogc.CollectionsEnabled,
                        note = "Collections (OGC API Features) don't have a global toggle"
                    },
                    wfs = new
                    {
                        enabled = ogc.WfsEnabled,
                        globalEnabled = globalConfig.Wfs.Enabled,
                        effective = ogc.WfsEnabled && globalConfig.Wfs.Enabled
                    },
                    wms = new
                    {
                        enabled = ogc.WmsEnabled,
                        globalEnabled = globalConfig.Wms.Enabled,
                        effective = ogc.WmsEnabled && globalConfig.Wms.Enabled
                    },
                    wmts = new
                    {
                        enabled = ogc.WmtsEnabled,
                        globalEnabled = globalConfig.Wmts.Enabled,
                        effective = ogc.WmtsEnabled && globalConfig.Wmts.Enabled
                    },
                    csw = new
                    {
                        enabled = ogc.CswEnabled,
                        globalEnabled = globalConfig.Csw.Enabled,
                        effective = ogc.CswEnabled && globalConfig.Csw.Enabled
                    },
                    wcs = new
                    {
                        enabled = ogc.WcsEnabled,
                        globalEnabled = globalConfig.Wcs.Enabled,
                        effective = ogc.WcsEnabled && globalConfig.Wcs.Enabled
                    }
                }
            });
        });

        // PATCH /admin/config/services/{serviceId}/{protocol} - Toggle service-level protocol
        group.MapPatch("/services/{serviceId}/{protocol}", async (
            string serviceId,
            string protocol,
            ToggleRequest request,
            HonuaConfigurationService configService,
            IMetadataRegistry metadataRegistry,
            ODataModelCache odataCache) =>
        {
            if (quickStartMode)
            {
                return ApiErrorResponse.Json.Forbidden("Service configuration changes are disabled in QuickStart mode.");
            }

            await metadataRegistry.EnsureInitializedAsync(default).ConfigureAwait(false);
            var snapshot = await metadataRegistry.GetSnapshotAsync(default).ConfigureAwait(false);
            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase));

            if (service == null)
            {
                return GeoservicesRESTErrorHelper.NotFound("Service", serviceId);
            }

            var normalizedProtocol = protocol.ToLowerInvariant();
            var currentOgc = service.Ogc;

            // Create new OgcServiceDefinition with the toggled protocol using 'with' expression
            var newOgc = normalizedProtocol switch
            {
                "collections" or "ogc-api-features" => currentOgc with { CollectionsEnabled = request.Enabled },
                "wfs" => currentOgc with { WfsEnabled = request.Enabled },
                "wms" => currentOgc with { WmsEnabled = request.Enabled },
                "wmts" => currentOgc with { WmtsEnabled = request.Enabled },
                "csw" => currentOgc with { CswEnabled = request.Enabled },
                "wcs" => currentOgc with { WcsEnabled = request.Enabled },
                _ => null
            };

            if (newOgc == null)
            {
                return ApiErrorResponse.Json.BadRequestResult($"Unknown protocol: {protocol}. Valid values: collections, wfs, wms, wmts, csw, wcs");
            }

            // Create new ServiceDefinition using 'with' expression
            var newService = service with { Ogc = newOgc };

            // Validate against global configuration
            var globalConfig = configService.Current.Services;
            var validation = ServiceApiConfigurationValidator.ValidateService(newService, globalConfig);
            if (!validation.IsValid)
            {
                return Results.UnprocessableEntity(new
                {
                    error = $"Cannot enable {protocol} for service '{serviceId}'. {protocol.ToUpperInvariant()} is disabled globally in appsettings.json.",
                    details = validation.Errors,
                    note = "To enable this protocol, update appsettings.json (honua:services:{protocol}:enabled: true) and restart the server."
                });
            }

            // Create new snapshot with updated service
            var newServices = snapshot.Services.Select(s => s.Id == serviceId ? newService : s).ToList();
            var newSnapshot = new MetadataSnapshot(
                catalog: snapshot.Catalog,
                folders: snapshot.Folders,
                dataSources: snapshot.DataSources,
                services: newServices,
                layers: snapshot.Layers,
                rasterDatasets: snapshot.RasterDatasets,
                styles: snapshot.Styles,
                server: snapshot.Server
            );

            // Update metadata registry
            await metadataRegistry.UpdateAsync(newSnapshot, default);

            // Reset OData cache if needed
            odataCache.Reset();

            return Results.Ok(new
            {
                status = "updated",
                serviceId,
                protocol = normalizedProtocol,
                enabled = request.Enabled,
                message = $"{protocol.ToUpperInvariant()} {(request.Enabled ? "enabled" : "disabled")} for service '{serviceId}'.",
                note = "This change is in-memory only. To persist, update metadata.json and reload or restart."
            });
        });

        return group;
    }

    /// <summary>
    /// Request model for toggling service or protocol enablement.
    /// </summary>
    /// <param name="Enabled">Indicates whether the service or protocol should be enabled.</param>
    private sealed record ToggleRequest(bool Enabled);

    private static ServicesConfiguration CloneServicesConfiguration(
        ServicesConfiguration source,
        WfsConfiguration? wfs = null,
        WmsConfiguration? wms = null,
        WmtsConfiguration? wmts = null,
        CswConfiguration? csw = null,
        WcsConfiguration? wcs = null,
        StacCatalogConfiguration? stac = null,
        GeometryServiceConfiguration? geometry = null,
        RasterTileCacheConfiguration? rasterTiles = null,
        PrintServiceConfiguration? print = null)
    {
        return new ServicesConfiguration
        {
            Wfs = wfs ?? source.Wfs,
            Wms = wms ?? source.Wms,
            Wmts = wmts ?? source.Wmts,
            Csw = csw ?? source.Csw,
            Wcs = wcs ?? source.Wcs,
            Print = print ?? source.Print,
            RasterTiles = rasterTiles ?? source.RasterTiles,
            Stac = stac ?? source.Stac,
            Geometry = geometry ?? source.Geometry
        };
    }

    private static HonuaConfiguration CloneConfiguration(HonuaConfiguration current, ServicesConfiguration services)
    {
        return new HonuaConfiguration
        {
            Metadata = current.Metadata,
            Services = services,
            Attachments = current.Attachments,
            ExternalServiceSecurity = current.ExternalServiceSecurity,
            RasterCache = current.RasterCache
        };
    }
}
