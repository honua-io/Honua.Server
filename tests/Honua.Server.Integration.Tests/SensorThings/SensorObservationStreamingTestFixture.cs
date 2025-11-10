// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Honua.Server.Integration.Tests.SensorThings;

/// <summary>
/// Test fixture for SensorThings SignalR streaming integration tests.
/// Configures test server with WebSocket streaming enabled.
/// </summary>
public class SensorObservationStreamingTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Task InitializeAsync()
    {
        // No async initialization needed for SignalR tests
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Clean up resources
        await Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["honua:authentication:mode"] = "Local",
                ["honua:authentication:enforce"] = "false", // Disable auth for tests
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = "../../samples/metadata/sample-metadata.json",

                // Enable SensorThings API
                ["SensorThings:Enabled"] = "true",
                ["SensorThings:BasePath"] = "/sta/v1.1",

                // Enable WebSocket streaming for tests
                ["SensorThings:WebSocketStreamingEnabled"] = "true",

                // Configure streaming options
                ["SensorThings:Streaming:Enabled"] = "true",
                ["SensorThings:Streaming:RateLimitingEnabled"] = "true",
                ["SensorThings:Streaming:RateLimitPerSecond"] = "100",
                ["SensorThings:Streaming:BatchingEnabled"] = "true",
                ["SensorThings:Streaming:BatchingThreshold"] = "100"
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.UseEnvironment("Test");
    }
}
