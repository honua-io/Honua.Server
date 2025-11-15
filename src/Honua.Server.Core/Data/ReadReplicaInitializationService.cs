// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data;

/// <summary>
/// Background service that initializes read replica routing by registering replicas from metadata.
/// Runs once at startup to configure the replica router based on data source definitions.
/// </summary>
public sealed class ReadReplicaInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ReadReplicaOptions _options;
    private readonly ILogger<ReadReplicaInitializationService> _logger;

    public ReadReplicaInitializationService(
        IServiceProvider serviceProvider,
        IOptions<ReadReplicaOptions> options,
        ILogger<ReadReplicaInitializationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableReadReplicaRouting)
        {
            _logger.LogInformation("Read replica routing is disabled");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var metadataRegistry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
            var dataSourceRouter = scope.ServiceProvider.GetRequiredService<IDataSourceRouter>();

            var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            // Group data sources by primary (non-read-only) and replicas (read-only)
            var primaryDataSources = snapshot.DataSources.Where(ds => !ds.ReadOnly).ToList();
            var replicaDataSources = snapshot.DataSources.Where(ds => ds.ReadOnly).ToList();

            if (replicaDataSources.Count == 0)
            {
                _logger.LogInformation("No read replicas configured in metadata");
                return;
            }

            _logger.LogInformation(
                "Initializing read replica routing: {PrimaryCount} primary data sources, {ReplicaCount} read replicas",
                primaryDataSources.Count,
                replicaDataSources.Count);

            // For each primary data source, register its replicas
            // Convention: Replicas are associated with primaries by naming pattern or explicit configuration
            // For now, we'll associate replicas with the first primary data source
            // In a real implementation, you'd have explicit configuration for replica associations

            foreach (var primary in primaryDataSources)
            {
                // Find replicas for this primary (could be based on naming convention or explicit config)
                // For now, associate all replicas with each primary for maximum flexibility
                var associatedReplicas = replicaDataSources
                    .Where(r => r.Provider == primary.Provider) // Same provider type
                    .ToList();

                if (associatedReplicas.Any())
                {
                    dataSourceRouter.RegisterReplicas(primary.Id, associatedReplicas);

                    _logger.LogInformation(
                        "Registered {Count} read replicas for primary data source '{PrimaryId}': {ReplicaIds}",
                        associatedReplicas.Count,
                        primary.Id,
                        string.Join(", ", associatedReplicas.Select(r => r.Id)));
                }
            }

            _logger.LogInformation("Read replica routing initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize read replica routing");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}
