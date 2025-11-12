// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
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
            // Create minimal HCL configuration for Configuration V2
            var tempConfigPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.honua");
            var minimalConfig = $$"""
            honua {
                version     = "2.0"
                environment = "test"
                log_level   = "information"
            }

            data_source "test_db" {
                provider   = "postgresql"
                connection = env("DATABASE_URL")

                pool {
                    min_size = 1
                    max_size = 5
                }
            }

            data_source "test_mysql" {
                provider   = "mysql"
                connection = env("MYSQL_URL")

                pool {
                    min_size = 1
                    max_size = 5
                }
            }

            cache "redis_test" {
                enabled    = false
                connection = env("REDIS_URL")
            }
            """;

            // Write configuration file
            File.WriteAllText(tempConfigPath, minimalConfig);

            // Override configuration with TestContainers values via environment variables
            var configOverrides = new Dictionary<string, string?>
            {
                ["HONUA_CONFIG_PATH"] = tempConfigPath,
                ["HONUA_CONFIG_V2_ENABLED"] = "true",
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
                ["Features:EnableGeoservicesREST"] = "true"
            };

            config.AddInMemoryCollection(configOverrides);
        });

        builder.ConfigureTestServices(services =>
        {
            // Override services for testing if needed
            // Example: services.AddSingleton<IEmailService, MockEmailService>();
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
