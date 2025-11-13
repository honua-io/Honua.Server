// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
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
/// Custom WebApplicationFactory for integration testing the Honua.Server API.
/// This fixture provides an in-memory test server with configurable test dependencies.
/// </summary>
/// <typeparam name="TProgram">The program entry point type.</typeparam>
public class WebApplicationFactoryFixture<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private readonly DatabaseFixture _databaseFixture;

    public WebApplicationFactoryFixture(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    /// <summary>
    /// Gets the database connection strings from the database fixture.
    /// </summary>
    public DatabaseFixture Database => _databaseFixture;

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

            // Create minimal HCL configuration for Configuration V2
            var tempConfigPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.honua");
            // Escape connection strings for HCL by replacing backslashes and quotes
            var postgresConnEscaped = _databaseFixture.PostgresConnectionString.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var mysqlConnEscaped = _databaseFixture.MySqlConnectionString.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var redisConnEscaped = _databaseFixture.RedisConnectionString.Replace("\\", "\\\\").Replace("\"", "\\\"");

            var minimalConfig = $$"""
            honua {
                version     = "2.0"
                environment = "test"
                log_level   = "information"
            }

            data_source "test_db" {
                provider   = "postgresql"
                connection = "{{postgresConnEscaped}}"

                pool = {
                    min_size = 1
                    max_size = 5
                }
            }

            data_source "test_mysql" {
                provider   = "mysql"
                connection = "{{mysqlConnEscaped}}"

                pool = {
                    min_size = 1
                    max_size = 5
                }
            }

            cache "redis_test" {
                enabled    = false
                connection = "{{redisConnEscaped}}"
            }

            # OGC API Features Service
            service "ogc_api" {
                enabled     = true
                item_limit  = 5000
                default_crs = "EPSG:4326"
            }

            # WFS Service
            service "wfs" {
                enabled                     = true
                version                     = "2.0.0"
                capabilities_cache_duration = 3600
                default_count               = 100
                max_features                = 10000
            }

            # WMS Service
            service "wms" {
                enabled               = true
                version               = "1.3.0"
                max_width             = 4096
                max_height            = 4096
                render_timeout_seconds = 60
            }

            # WMTS Service
            service "wmts" {
                enabled           = true
                version           = "1.0.0"
                tile_size         = 256
                supported_formats = ["image/png", "image/jpeg"]
            }

            # STAC Service
            service "stac" {
                enabled     = true
                version     = "1.0.0"
            }

            # OData Service
            service "odata" {
                enabled           = true
                allow_writes      = false
                max_page_size     = 1000
                default_page_size = 100
            }

            # GeoServices REST (Esri-compatible)
            service "geoservices_rest" {
                enabled                 = true
                version                 = "10.81"
                default_max_record_count = 1000
                max_record_count        = 10000
            }

            # Test layer for OGC API Features
            layer "test_features" {
                title       = "Test Features"
                description = "Test layer for integration tests"
                data_source = "test_db"
                table       = "features"
                id_field    = "id"
                display_field = "name"
                introspect_fields = true

                geometry = {
                    column = "geom"
                    type   = "Geometry"
                    srid   = 4326
                }

                services = ["ogc_api"]
            }

            # Test layer for WFS
            layer "test_features_wfs" {
                title       = "Test Features WFS"
                description = "Test layer for WFS"
                data_source = "test_db"
                table       = "features"
                id_field    = "id"
                display_field = "name"
                introspect_fields = true

                geometry = {
                    column = "geom"
                    type   = "Geometry"
                    srid   = 4326
                }

                services = ["wfs"]
            }

            # Test layer for WMS
            layer "test_features_wms" {
                title       = "Test Features WMS"
                description = "Test layer for WMS"
                data_source = "test_db"
                table       = "features"
                id_field    = "id"
                display_field = "name"
                introspect_fields = true

                geometry = {
                    column = "geom"
                    type   = "Geometry"
                    srid   = 4326
                }

                services = ["wms"]
            }

            # Test layer for WMTS
            layer "test_features_wmts" {
                title       = "Test Features WMTS"
                description = "Test layer for WMTS"
                data_source = "test_db"
                table       = "features"
                id_field    = "id"
                display_field = "name"
                introspect_fields = true

                geometry = {
                    column = "geom"
                    type   = "Geometry"
                    srid   = 4326
                }

                services = ["wmts"]
            }

            # Test layer for OData
            layer "test_features_odata" {
                title       = "Test Features OData"
                description = "Test layer for OData"
                data_source = "test_db"
                table       = "features"
                id_field    = "id"
                display_field = "name"
                introspect_fields = true

                geometry = {
                    column = "geom"
                    type   = "Geometry"
                    srid   = 4326
                }

                services = ["odata"]
            }

            # Test layer for GeoServices REST
            layer "test_features_geoservices" {
                title       = "Test Features GeoServices"
                description = "Test layer for GeoServices REST"
                data_source = "test_db"
                table       = "features"
                id_field    = "id"
                display_field = "name"
                introspect_fields = true

                geometry = {
                    column = "geom"
                    type   = "Geometry"
                    srid   = 4326
                }

                services = ["geoservices_rest"]
            }
            """;

            // Write configuration file
            File.WriteAllText(tempConfigPath, minimalConfig);

            // Override configuration with TestContainers values
            var pluginsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../plugins"));
            var configOverrides = new Dictionary<string, string?>
            {
                ["HONUA_CONFIG_PATH"] = tempConfigPath,
                ["Honua:ConfigurationV2:Path"] = tempConfigPath,  // Required by ServiceCollectionExtensions
                ["HONUA_CONFIG_V2_ENABLED"] = "true",
                ["honua:plugins:paths:0"] = pluginsPath,  // Fix plugin discovery path
                ["DATABASE_URL"] = _databaseFixture.PostgresConnectionString,
                ["MYSQL_URL"] = _databaseFixture.MySqlConnectionString,
                ["REDIS_URL"] = _databaseFixture.RedisConnectionString,
                ["ConnectionStrings:DefaultConnection"] = _databaseFixture.PostgresConnectionString,
                ["ConnectionStrings:MySql"] = _databaseFixture.MySqlConnectionString,
                ["ConnectionStrings:Redis"] = _databaseFixture.RedisConnectionString,
                ["Features:EnableCaching"] = "false", // Disable caching for predictable tests
                ["Features:EnableStac"] = "true",
                ["Features:EnableOgcFeatures"] = "true",
                ["Features:EnableWfs"] = "true",
                ["Features:EnableWms"] = "true",
                ["Features:EnableWmts"] = "true",
                ["Features:EnableGeoservicesREST"] = "true",
                // STAC configuration - use in-memory provider for tests
                ["Honua:Services:Stac:Enabled"] = "true",  // Enable STAC to match HCL configuration
                ["Honua:Services:Stac:Provider"] = "memory",
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
            // Override STAC catalog store to use in-memory for tests
            services.AddSingleton<IStacCatalogStore>(new InMemoryStacCatalogStore());
        });

        builder.UseEnvironment("Test");
    }

    public Task InitializeAsync()
    {
        // Initialization is handled by DatabaseFixture
        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        // Cleanup is handled by base class and DatabaseFixture
        return Task.CompletedTask;
    }
}
