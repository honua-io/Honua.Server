using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Tests.TestInfrastructure;

/// <summary>
/// Web application factory for Host.Tests integration tests.
/// Provides a lightweight test configuration for endpoint testing.
/// </summary>
public sealed class HonuaWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "TestAdmin123!";

    private readonly string _rootPath;
    private readonly string _metadataPath;
    private readonly string _authStorePath;
    private HttpClient? _defaultClient;

    public HonuaWebApplicationFactory()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"honua-host-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);

        _metadataPath = Path.Combine(_rootPath, "metadata.json");
        _authStorePath = Path.Combine(_rootPath, "auth.db");

        // Write minimal metadata file
        var metadataJson = """
        {
          "server": {
            "allowedHosts": ["*"]
          },
          "catalog": {
            "id": "test-catalog",
            "title": "Test Catalog",
            "description": "Test metadata catalog"
          },
          "services": [],
          "layers": [],
          "styles": [],
          "rasterDatasets": []
        }
        """;
        File.WriteAllText(_metadataPath, metadataJson);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();

            var settings = new Dictionary<string, string?>
            {
                // Metadata configuration
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = _metadataPath,

                // Authentication configuration - QuickStart mode for simplified testing
                ["honua:authentication:mode"] = "QuickStart",
                ["honua:authentication:enforce"] = "false",
                ["honua:authentication:quickStart:enabled"] = "true",
                ["honua:authentication:allowQuickStart"] = "true",
                ["honua:authentication:local:storePath"] = _authStorePath,
                ["honua:authentication:bootstrap:adminUsername"] = DefaultAdminUsername,
                ["honua:authentication:bootstrap:adminPassword"] = DefaultAdminPassword,

                // Disable features that complicate testing
                ["honua:rateLimiting:enabled"] = "false",
                ["honua:openApi:enabled"] = "false",
                ["honua:observability:metrics:enabled"] = "false",
                ["honua:security:enforcePolicies"] = "false",
                ["honua:services:odata:enabled"] = "false",
                ["AllowedHosts"] = "*",

                // STAC disabled by default
                ["honua:services:stac:enabled"] = "false",

                // Redis connection (won't be used in most tests)
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            // Add any service customization here if needed
        });
    }

    /// <summary>
    /// Creates an HTTP client configured for test requests.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        if (_defaultClient is not null)
        {
            return _defaultClient;
        }

        _defaultClient = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        _defaultClient.BaseAddress = new Uri("https://localhost");

        return _defaultClient;
    }

    public new void Dispose()
    {
        base.Dispose();
        _defaultClient?.Dispose();

        // Clean up temporary directory
        if (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath))
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        GC.SuppressFinalize(this);
    }
}
