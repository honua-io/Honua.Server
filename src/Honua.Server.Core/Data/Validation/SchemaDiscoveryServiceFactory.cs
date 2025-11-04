// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// Factory for creating database-specific schema discovery services.
/// Routes to the appropriate service based on data source provider.
/// </summary>
public interface ISchemaDiscoveryServiceFactory
{
    /// <summary>
    /// Gets the appropriate schema discovery service for the given data source.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns>Schema discovery service, or null if provider doesn't support discovery.</returns>
    ISchemaDiscoveryService? GetService(DataSourceDefinition dataSource);

    /// <summary>
    /// Checks if schema discovery is supported for the given provider.
    /// </summary>
    bool IsSupported(string? provider);
}

/// <summary>
/// Default implementation of schema discovery service factory.
/// </summary>
public sealed class SchemaDiscoveryServiceFactory : ISchemaDiscoveryServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchemaDiscoveryServiceFactory> _logger;
    private readonly Dictionary<string, Type> _serviceTypes;

    public SchemaDiscoveryServiceFactory(
        IServiceProvider serviceProvider,
        ILogger<SchemaDiscoveryServiceFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register service types by provider key
        _serviceTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["postgis"] = typeof(PostgresSchemaDiscoveryService),
            ["postgres"] = typeof(PostgresSchemaDiscoveryService),
            ["postgresql"] = typeof(PostgresSchemaDiscoveryService),
            ["mysql"] = typeof(MySqlSchemaDiscoveryService),
            ["sqlserver"] = typeof(SqlServerSchemaDiscoveryService),
            ["sqlite"] = typeof(SqliteSchemaDiscoveryService)
        };
    }

    public ISchemaDiscoveryService? GetService(DataSourceDefinition dataSource)
    {
        if (dataSource == null || string.IsNullOrWhiteSpace(dataSource.Provider))
        {
            return null;
        }

        if (!_serviceTypes.TryGetValue(dataSource.Provider, out var serviceType))
        {
            _logger.LogDebug(
                "No schema discovery service available for provider '{Provider}'. Schema sync will be skipped.",
                dataSource.Provider);
            return null;
        }

        try
        {
            return (ISchemaDiscoveryService)_serviceProvider.GetRequiredService(serviceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create schema discovery service for provider '{Provider}'",
                dataSource.Provider);
            return null;
        }
    }

    public bool IsSupported(string? provider)
    {
        return !string.IsNullOrWhiteSpace(provider) &&
               _serviceTypes.ContainsKey(provider);
    }
}
