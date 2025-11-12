// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Plugins.Database.PostgreSQL;

/// <summary>
/// PostgreSQL/PostGIS database plugin.
/// Provides full-featured spatial database support with native geometry types, MVT tile generation, and advanced querying.
/// </summary>
public class PostgreSQLDatabasePlugin : IDatabasePlugin
{
    private ILogger<PostgreSQLDatabasePlugin>? _logger;

    // IHonuaPlugin properties
    public string Id => "honua.plugins.database.postgresql";
    public string Name => "PostgreSQL/PostGIS Database Plugin";
    public string Version => "1.0.0";
    public string Description => "Full-featured PostgreSQL/PostGIS database provider with spatial extensions, MVT tile generation, and advanced geospatial capabilities.";
    public string Author => "HonuaIO";
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public string? MinimumHonuaVersion => "1.0.0";

    // IDatabasePlugin properties
    public string ProviderKey => PostgresDataStoreProvider.ProviderKey; // "postgis"
    public DatabaseProviderType ProviderType => DatabaseProviderType.Relational;
    public string DisplayName => "PostgreSQL with PostGIS";

    public IDataStoreCapabilities Capabilities => new PostgresDataStoreCapabilities();

    public Task OnLoadAsync(PluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.ServiceProvider?.GetService<ILogger<PostgreSQLDatabasePlugin>>();
        _logger?.LogInformation(
            "Loading PostgreSQL database plugin: {Name} v{Version}",
            Name,
            Version
        );

        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Unloading PostgreSQL database plugin");
        return Task.CompletedTask;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context)
    {
        _logger?.LogDebug("Registering PostgreSQL database provider with key: {ProviderKey}", ProviderKey);

        // Register the PostgreSQL data store provider as a keyed singleton
        services.AddKeyedSingleton<IDataStoreProvider>(
            ProviderKey,
            (serviceProvider, key) =>
            {
                var logger = serviceProvider.GetService<ILogger<PostgresDataStoreProvider>>();
                return new PostgresDataStoreProvider(logger);
            }
        );

        // Register schema discovery and validation services
        services.AddSingleton<PostgresSchemaDiscoveryService>();
        services.AddSingleton<PostgresSchemaValidator>();

        _logger?.LogInformation("PostgreSQL database provider registered successfully");
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        // Check if Npgsql package is available
        try
        {
            var npgsqlAssembly = typeof(Npgsql.NpgsqlConnection).Assembly;
            _logger?.LogDebug("Npgsql version: {Version}", npgsqlAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"Npgsql package not found: {ex.Message}");
            return result;
        }

        // Check if NetTopologySuite.IO.PostGis is available
        try
        {
            var postgisAssembly = typeof(NetTopologySuite.IO.PostGisReader).Assembly;
            _logger?.LogDebug("PostGIS IO version: {Version}", postgisAssembly.GetName().Version);
        }
        catch (Exception ex)
        {
            result.AddError($"NetTopologySuite.IO.PostGis package not found: {ex.Message}");
            return result;
        }

        // Check Configuration V2 for any PostgreSQL data sources
        var honuaConfig = configuration.Get<Core.Configuration.V2.HonuaConfig>();
        if (honuaConfig?.DataSources != null)
        {
            var postgresDataSources = honuaConfig.DataSources
                .Where(ds => ds.Value.Provider?.Equals("postgresql", StringComparison.OrdinalIgnoreCase) == true ||
                             ds.Value.Provider?.Equals("postgis", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (postgresDataSources.Any())
            {
                _logger?.LogDebug("Found {Count} PostgreSQL data sources in configuration", postgresDataSources.Count);

                foreach (var ds in postgresDataSources)
                {
                    // Validate connection string format
                    if (string.IsNullOrEmpty(ds.Value.ConnectionString))
                    {
                        result.AddWarning($"Data source '{ds.Key}' has no connection string configured");
                    }
                    else
                    {
                        // Basic PostgreSQL connection string validation
                        if (!ds.Value.ConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
                            !ds.Value.ConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
                        {
                            result.AddWarning($"Data source '{ds.Key}' connection string may be invalid (missing Host/Server)");
                        }
                    }
                }
            }
            else
            {
                result.AddInfo("No PostgreSQL data sources found in configuration (plugin will be available but unused)");
            }
        }

        return result;
    }

    public IDataStoreProvider CreateProvider()
    {
        return new PostgresDataStoreProvider(_logger as ILogger<PostgresDataStoreProvider>);
    }

    private class PostgresDataStoreCapabilities : IDataStoreCapabilities
    {
        public bool SupportsNativeGeometry => true;
        public bool SupportsNativeMvt => true;
        public bool SupportsTransactions => true;
        public bool SupportsSpatialIndexes => true;
        public bool SupportsServerSideGeometryOperations => true;
        public bool SupportsCrsTransformations => true;
        public bool SupportsBulkOperations => true;
        public bool SupportsSoftDelete => true;
        public bool SupportsFullTextSearch => true;
        public bool SupportsJsonQueries => true;
        public IReadOnlyList<string> SupportedGeometryTypes => new[]
        {
            "Point", "LineString", "Polygon",
            "MultiPoint", "MultiLineString", "MultiPolygon",
            "GeometryCollection", "CircularString", "CompoundCurve",
            "CurvePolygon", "MultiCurve", "MultiSurface", "PolyhedralSurface", "TIN", "Triangle"
        };
        public IReadOnlyList<int> SupportedSrids => Array.Empty<int>(); // All SRIDs supported via PostGIS
        public long? MaxFeatureCount => null; // No hard limit
        public long? MaxGeometrySize => 1_073_741_824; // 1GB max for geometry column
    }
}
