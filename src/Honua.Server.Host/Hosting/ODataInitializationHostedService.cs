// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.OData;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Hosting;

/// <summary>
/// Hosted service that ensures OData routes are initialized before the application starts serving requests.
/// </summary>
public sealed class ODataInitializationHostedService : IHostedService
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ODataModelCache _modelCache;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly IOptions<ODataOptions> _odataOptions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ODataInitializationHostedService> _logger;
    private Task? _initializationTask;

    public ODataInitializationHostedService(
        IMetadataRegistry metadataRegistry,
        ODataModelCache modelCache,
        IHonuaConfigurationService configurationService,
        IOptions<ODataOptions> odataOptions,
        IHostApplicationLifetime lifetime,
        ILogger<ODataInitializationHostedService> logger)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _modelCache = Guard.NotNull(modelCache);
        _configurationService = Guard.NotNull(configurationService);
        _odataOptions = Guard.NotNull(odataOptions);
        _lifetime = Guard.NotNull(lifetime);
        _logger = Guard.NotNull(logger);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _initializationTask = InitializeAsync(cancellationToken);
        return _initializationTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _initializationTask ?? Task.CompletedTask;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.ApplicationStopping);
        var token = linkedCts.Token;

        try
        {
            _logger.LogInformation("Initializing OData routes");

            await _metadataRegistry.EnsureInitializedAsync(token).ConfigureAwait(false);

            var descriptor = await _modelCache.GetOrCreateAsync(token).ConfigureAwait(false);
            var options = _odataOptions.Value;

            if (!options.RouteComponents.ContainsKey("odata"))
            {
                options.AddRouteComponents("odata", descriptor.Model);
                var entityTypeCount = descriptor.Model.SchemaElements.Count();
                _logger.LogInformation("OData route 'odata' registered with {EntityTypeCount} entity types", entityTypeCount);
            }

            var odataConfig = _configurationService.Current.Services.OData;
            if (odataConfig.MaxPageSize > 0)
            {
                options.SetMaxTop(odataConfig.MaxPageSize);
                _logger.LogInformation("OData max page size set to {MaxPageSize}", odataConfig.MaxPageSize);
            }

            _logger.LogInformation("OData initialization completed successfully");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _logger.LogDebug("OData initialization cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "OData initialization failed.");
            throw;
        }
    }
}
