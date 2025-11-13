// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Honua.Server.Integration.Tests.Fixtures;

/// <summary>
/// WebApplicationFactory for integration testing with Configuration V2 (HCL/.honua files).
/// This fixture provides an in-memory test server configured via declarative .honua configuration.
/// </summary>
/// <typeparam name="TProgram">The program entry point type.</typeparam>
public class ConfigurationV2TestFixture<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly string _honuaConfiguration;
    private readonly string _tempConfigFilePath;

    /// <summary>
    /// Creates a new Configuration V2 test fixture with inline HCL configuration.
    /// </summary>
    /// <param name="databaseFixture">Database fixture providing TestContainer connection strings.</param>
    /// <param name="honuaConfiguration">HCL configuration content as a string.</param>
    public ConfigurationV2TestFixture(DatabaseFixture databaseFixture, string honuaConfiguration)
    {
        _databaseFixture = databaseFixture ?? throw new ArgumentNullException(nameof(databaseFixture));
        _honuaConfiguration = honuaConfiguration ?? throw new ArgumentNullException(nameof(honuaConfiguration));

        // Create temporary config file
        _tempConfigFilePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.honua");
    }

    /// <summary>
    /// Creates a new Configuration V2 test fixture using a builder pattern.
    /// </summary>
    /// <param name="databaseFixture">Database fixture providing TestContainer connection strings.</param>
    /// <param name="configureBuilder">Action to configure the HCL configuration builder.</param>
    public ConfigurationV2TestFixture(DatabaseFixture databaseFixture, Action<TestConfigurationBuilder> configureBuilder)
        : this(databaseFixture, BuildConfiguration(databaseFixture, configureBuilder))
    {
    }

    /// <summary>
    /// Gets the database connection strings from the database fixture.
    /// </summary>
    public DatabaseFixture Database => _databaseFixture;

    /// <summary>
    /// Gets the loaded HonuaConfig instance.
    /// </summary>
    public HonuaConfig? LoadedConfig { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Create test appsettings.json to disable STAC (must be loaded early to override defaults)
            var testAppSettingsPath = Path.Combine(Path.GetTempPath(), $"test-appsettings-{Guid.NewGuid()}.json");
            var testAppSettings = """
            {
              "Honua": {
                "Services": {
                  "Stac": {
                    "Enabled": false,
                    "Provider": "memory"
                  }
                }
              }
            }
            """;
            File.WriteAllText(testAppSettingsPath, testAppSettings);
            config.AddJsonFile(testAppSettingsPath, optional: false, reloadOnChange: false);

            // Write the configuration to the temporary file
            var interpolatedConfig = InterpolateConnectionStrings(_honuaConfiguration);
            File.WriteAllText(_tempConfigFilePath, interpolatedConfig);

            // Load and register the Configuration V2
            try
            {
                LoadedConfig = HonuaConfigLoader.LoadAsync(_tempConfigFilePath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load Configuration V2 from {_tempConfigFilePath}", ex);
            }

            // Add environment variable overrides for testing
            var pluginsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../plugins"));
            var configOverrides = new Dictionary<string, string?>
            {
                ["HONUA_CONFIG_PATH"] = _tempConfigFilePath,
                ["Honua:ConfigurationV2:Path"] = _tempConfigFilePath,  // Required by ServiceCollectionExtensions
                ["HONUA_CONFIG_V2_ENABLED"] = "true",
                ["honua:plugins:paths:0"] = pluginsPath,  // Fix plugin discovery path
                ["DATABASE_URL"] = _databaseFixture.PostgresConnectionString,
                ["MYSQL_URL"] = _databaseFixture.MySqlConnectionString,
                ["REDIS_URL"] = _databaseFixture.RedisConnectionString,
                // Configure STAC to disable for tests (STAC is not configured in Configuration V2 HCL)
                ["Honua:Services:Stac:Enabled"] = "false",
                ["Honua:Services:Stac:Provider"] = "memory",
                // Provide legacy fallbacks for compatibility during migration
                ["ConnectionStrings:DefaultConnection"] = _databaseFixture.PostgresConnectionString,
                ["ConnectionStrings:MySql"] = _databaseFixture.MySqlConnectionString,
                ["ConnectionStrings:Redis"] = _databaseFixture.RedisConnectionString,
                // Authentication configuration - disable enforcement for integration tests
                ["honua:authentication:enforce"] = "false",
                ["honua:authentication:mode"] = "QuickStart",
                ["AllowedHosts"] = "*",
                ["honua:cors:allowAnyOrigin"] = "true"
            };

            config.AddInMemoryCollection(configOverrides);
        });

        builder.ConfigureTestServices(services =>
        {
            // Register the loaded Configuration V2 as a singleton
            if (LoadedConfig != null)
            {
                services.AddSingleton(LoadedConfig);

                // Note: Service registration is now handled by the plugin system
                // via AddHonuaConfigurationV2() in ConfigurationV2Extensions.cs
            }

            // Override STAC catalog store to use in-memory for tests
            services.AddSingleton<IStacCatalogStore>(new InMemoryStacCatalogStore());
        });

        builder.UseEnvironment("Test");
    }

    private string InterpolateConnectionStrings(string config)
    {
        // Replace environment variable placeholders with actual test connection strings
        var result = config;
        result = result.Replace("${env:DATABASE_URL}", _databaseFixture.PostgresConnectionString);
        result = result.Replace("env(\"DATABASE_URL\")", $"\"{_databaseFixture.PostgresConnectionString}\"");
        result = result.Replace("${env:MYSQL_URL}", _databaseFixture.MySqlConnectionString);
        result = result.Replace("env(\"MYSQL_URL\")", $"\"{_databaseFixture.MySqlConnectionString}\"");
        result = result.Replace("${env:REDIS_URL}", _databaseFixture.RedisConnectionString);
        result = result.Replace("env(\"REDIS_URL\")", $"\"{_databaseFixture.RedisConnectionString}\"");
        return result;
    }

    private static string BuildConfiguration(DatabaseFixture databaseFixture, Action<TestConfigurationBuilder> configureBuilder)
    {
        var builder = new TestConfigurationBuilder(databaseFixture);
        configureBuilder(builder);
        return builder.Build();
    }

    public Task InitializeAsync()
    {
        // Initialization is handled by DatabaseFixture
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Clean up temporary config file
        if (File.Exists(_tempConfigFilePath))
        {
            try
            {
                File.Delete(_tempConfigFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await base.DisposeAsync();
    }
}

/// <summary>
/// Builder for creating HCL test configurations programmatically.
/// </summary>
public class TestConfigurationBuilder
{
    private readonly StringBuilder _config = new();
    private readonly DatabaseFixture _databaseFixture;
    private readonly List<string> _addedServices = new();

    public TestConfigurationBuilder(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture ?? throw new ArgumentNullException(nameof(databaseFixture));

        // Default honua block
        _config.AppendLine("honua {");
        _config.AppendLine("  version     = \"1.0\"");
        _config.AppendLine("  environment = \"test\"");
        _config.AppendLine("  log_level   = \"debug\"");
        _config.AppendLine("}");
        _config.AppendLine();
    }

    /// <summary>
    /// Adds a data source configuration.
    /// </summary>
    /// <param name="id">The data source identifier.</param>
    /// <param name="provider">The provider type (postgresql, mysql, etc.).</param>
    /// <param name="connectionString">Optional connection string. If not provided, uses the default connection for the provider.</param>
    public TestConfigurationBuilder AddDataSource(string id, string provider, string? connectionString = null)
    {
        // Determine which connection string to use based on provider
        var actualConnectionString = connectionString ?? provider.ToLowerInvariant() switch
        {
            "postgresql" => _databaseFixture.PostgresConnectionString,
            "mysql" => _databaseFixture.MySqlConnectionString,
            _ => _databaseFixture.PostgresConnectionString // Default to PostgreSQL
        };

        _config.AppendLine($"data_source \"{id}\" {{");
        _config.AppendLine($"  provider   = \"{provider}\"");
        _config.AppendLine($"  connection = \"{actualConnectionString}\"");
        _config.AppendLine("  pool = {");
        _config.AppendLine("    min_size = 1");
        _config.AppendLine("    max_size = 5");
        _config.AppendLine("  }");
        _config.AppendLine("}");
        _config.AppendLine();
        return this;
    }

    /// <summary>
    /// Adds a service configuration.
    /// </summary>
    public TestConfigurationBuilder AddService(string serviceId, Dictionary<string, object>? settings = null)
    {
        _addedServices.Add(serviceId);

        _config.AppendLine($"service \"{serviceId}\" {{");
        _config.AppendLine("  enabled = true");

        if (settings != null)
        {
            foreach (var (key, value) in settings)
            {
                var formattedValue = value switch
                {
                    string s => $"\"{s}\"",
                    bool b => b ? "true" : "false",
                    _ => value.ToString()
                };
                _config.AppendLine($"  {key} = {formattedValue}");
            }
        }

        _config.AppendLine("}");
        _config.AppendLine();
        return this;
    }

    /// <summary>
    /// Adds a layer configuration.
    /// </summary>
    /// <param name="id">The layer identifier.</param>
    /// <param name="dataSourceRef">Reference to the data source (e.g., "gis_db").</param>
    /// <param name="table">The database table name.</param>
    /// <param name="geometryColumn">The geometry column name (default: "geom").</param>
    /// <param name="geometryType">The geometry type (default: "Polygon").</param>
    /// <param name="srid">The SRID (default: 4326).</param>
    /// <param name="serviceRefs">Optional service references (e.g., ["wfs", "ogc_api"]). If not provided, assigns to all previously added services.</param>
    public TestConfigurationBuilder AddLayer(string id, string dataSourceRef, string table, string geometryColumn = "geom", string geometryType = "Polygon", int srid = 4326, string[]? serviceRefs = null)
    {
        _config.AppendLine($"layer \"{id}\" {{");
        _config.AppendLine($"  title       = \"{id}\"");
        _config.AppendLine($"  data_source = data_source.{dataSourceRef}");
        _config.AppendLine($"  table       = \"{table}\"");
        _config.AppendLine($"  id_field    = \"id\"");
        _config.AppendLine("  introspect_fields = true");
        _config.AppendLine("  geometry = {");
        _config.AppendLine($"    column = \"{geometryColumn}\"");
        _config.AppendLine($"    type   = \"{geometryType}\"");
        _config.AppendLine($"    srid   = {srid}");
        _config.AppendLine("  }");

        // Add services if specified, or default to all added services
        var servicesToAssign = serviceRefs ?? _addedServices.ToArray();
        if (servicesToAssign.Length > 0)
        {
            var servicesList = string.Join(", ", servicesToAssign.Select(s => $"\"{s}\""));
            _config.AppendLine($"  services = [{servicesList}]");
        }

        _config.AppendLine("}");
        _config.AppendLine();
        return this;
    }

    /// <summary>
    /// Adds a Redis cache configuration.
    /// </summary>
    /// <param name="id">The cache identifier.</param>
    /// <param name="connectionString">Optional connection string. If not provided, uses the default Redis connection from the fixture.</param>
    public TestConfigurationBuilder AddRedisCache(string id = "redis_test", string? connectionString = null)
    {
        var actualConnectionString = connectionString ?? _databaseFixture.RedisConnectionString;

        _config.AppendLine($"cache \"{id}\" {{");
        _config.AppendLine("  enabled    = true");
        _config.AppendLine($"  connection = \"{actualConnectionString}\"");
        _config.AppendLine("  prefix     = \"test:\"");
        _config.AppendLine("  ttl        = 60");
        _config.AppendLine("}");
        _config.AppendLine();
        return this;
    }

    /// <summary>
    /// Adds raw HCL configuration.
    /// </summary>
    public TestConfigurationBuilder AddRaw(string hclConfig)
    {
        _config.AppendLine(hclConfig);
        _config.AppendLine();
        return this;
    }

    /// <summary>
    /// Builds the final HCL configuration string.
    /// </summary>
    public string Build() => _config.ToString();
}
