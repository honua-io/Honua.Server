// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
            // Add test configuration
            config.AddJsonFile("appsettings.Test.json", optional: false);

            // Override connection strings with TestContainers values
            var configOverrides = new Dictionary<string, string?>
            {
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
