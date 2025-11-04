// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// Factory for creating database-specific schema validators.
/// Routes to the appropriate validator based on data source provider.
/// </summary>
public interface ISchemaValidatorFactory
{
    /// <summary>
    /// Gets the appropriate schema validator for the given data source.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns>Schema validator, or null if provider doesn't support validation.</returns>
    ISchemaValidator? GetValidator(DataSourceDefinition dataSource);

    /// <summary>
    /// Checks if schema validation is supported for the given provider.
    /// </summary>
    bool IsSupported(string? provider);
}

/// <summary>
/// Default implementation of schema validator factory.
/// </summary>
public sealed class SchemaValidatorFactory : ISchemaValidatorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchemaValidatorFactory> _logger;
    private readonly Dictionary<string, Type> _validatorTypes;

    public SchemaValidatorFactory(
        IServiceProvider serviceProvider,
        ILogger<SchemaValidatorFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register validator types by provider key
        _validatorTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["postgis"] = typeof(PostgresSchemaValidator),
            ["postgres"] = typeof(PostgresSchemaValidator),
            ["postgresql"] = typeof(PostgresSchemaValidator),
            ["mysql"] = typeof(MySqlSchemaValidator),
            ["sqlserver"] = typeof(SqlServerSchemaValidator),
            ["sqlite"] = typeof(SqliteSchemaValidator)
        };
    }

    public ISchemaValidator? GetValidator(DataSourceDefinition dataSource)
    {
        if (dataSource == null || string.IsNullOrWhiteSpace(dataSource.Provider))
        {
            return null;
        }

        if (!_validatorTypes.TryGetValue(dataSource.Provider, out var validatorType))
        {
            _logger.LogDebug(
                "No schema validator available for provider '{Provider}'. Schema validation will be skipped.",
                dataSource.Provider);
            return null;
        }

        try
        {
            return (ISchemaValidator)_serviceProvider.GetRequiredService(validatorType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create schema validator for provider '{Provider}'",
                dataSource.Provider);
            return null;
        }
    }

    public bool IsSupported(string? provider)
    {
        return !string.IsNullOrWhiteSpace(provider) &&
               _validatorTypes.ContainsKey(provider);
    }
}
