// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates service-level API configurations at startup.
/// Ensures that services don't enable APIs that are disabled globally.
/// </summary>
public sealed class ServiceApiValidationHostedService : IHostedService
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly ILogger<ServiceApiValidationHostedService> _logger;

    public ServiceApiValidationHostedService(
        IMetadataRegistry metadataRegistry,
        IHonuaConfigurationService configurationService,
        ILogger<ServiceApiValidationHostedService> logger)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Validating service API configurations...");

            // Ensure metadata is loaded
            await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            // Get global configuration
            var globalConfig = _configurationService.Current.Services;

            // Validate all services
            var result = ServiceApiConfigurationValidator.ValidateServices(snapshot.Services, globalConfig);

            if (!result.IsValid)
            {
                var errorMessage = string.Join(Environment.NewLine, result.Errors);
                _logger.LogError("Service API configuration validation failed:{NewLine}{Errors}", Environment.NewLine, errorMessage);

                throw new InvalidOperationException(
                    $"Service API configuration is invalid. One or more services have APIs enabled that are disabled globally:{Environment.NewLine}{errorMessage}");
            }

            _logger.LogInformation("Service API configuration validation completed successfully. All services have valid API configurations.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error during service API configuration validation");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
