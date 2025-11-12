// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Integration.Tests.Fixtures;

/// <summary>
/// Base class for integration tests using Configuration V2 (HCL-based configuration).
/// Provides common functionality and patterns for testing with .honua configuration files.
/// </summary>
/// <remarks>
/// This base class simplifies Configuration V2 testing by:
/// - Managing factory lifecycle
/// - Providing helper methods for common assertions
/// - Exposing pre-configured HTTP client
/// - Handling database fixture integration
/// </remarks>
public abstract class ConfigurationV2IntegrationTestBase : IDisposable
{
    private ConfigurationV2TestFixture<Program>? _factory;
    private HttpClient? _client;

    /// <summary>
    /// Gets the database fixture providing TestContainer connection strings.
    /// </summary>
    protected DatabaseFixture DatabaseFixture { get; }

    /// <summary>
    /// Gets the test factory for the current test.
    /// Factory is created lazily on first access via CreateFactory().
    /// </summary>
    protected ConfigurationV2TestFixture<Program> Factory
    {
        get
        {
            if (_factory == null)
            {
                _factory = CreateFactory();
            }
            return _factory;
        }
    }

    /// <summary>
    /// Gets the HTTP client for making requests to the test server.
    /// Client is configured with the test server's base address.
    /// </summary>
    protected HttpClient Client
    {
        get
        {
            if (_client == null)
            {
                _client = Factory.CreateClient();
            }
            return _client;
        }
    }

    /// <summary>
    /// Initializes a new instance of the base test class.
    /// </summary>
    /// <param name="databaseFixture">Database fixture for test containers.</param>
    protected ConfigurationV2IntegrationTestBase(DatabaseFixture databaseFixture)
    {
        DatabaseFixture = databaseFixture ?? throw new ArgumentNullException(nameof(databaseFixture));
    }

    /// <summary>
    /// Creates the test factory with HCL configuration.
    /// Override this method to provide custom configuration for each test class.
    /// </summary>
    /// <returns>Configured test fixture instance.</returns>
    protected abstract ConfigurationV2TestFixture<Program> CreateFactory();

    /// <summary>
    /// Helper method to make a GET request and deserialize the JSON response.
    /// </summary>
    /// <typeparam name="T">Type to deserialize response to.</typeparam>
    /// <param name="url">Request URL.</param>
    /// <returns>Deserialized response object.</returns>
    protected async Task<T?> GetJsonAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Helper method to assert a response is successful and deserialize it.
    /// </summary>
    /// <typeparam name="T">Type to deserialize response to.</typeparam>
    /// <param name="response">HTTP response to validate and deserialize.</param>
    /// <returns>Deserialized response object.</returns>
    protected async Task<T> AssertSuccessAndDeserialize<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue($"Expected successful response but got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result.Should().NotBeNull();
        return result!;
    }

    /// <summary>
    /// Helper method to create HCL configuration using the builder pattern.
    /// </summary>
    /// <param name="configureBuilder">Action to configure the builder.</param>
    /// <returns>Test fixture configured with the builder.</returns>
    protected ConfigurationV2TestFixture<Program> CreateFactoryWithBuilder(Action<TestConfigurationBuilder> configureBuilder)
    {
        return new ConfigurationV2TestFixture<Program>(DatabaseFixture, configureBuilder);
    }

    /// <summary>
    /// Helper method to create HCL configuration from inline HCL string.
    /// </summary>
    /// <param name="hclConfig">HCL configuration string.</param>
    /// <returns>Test fixture configured with the HCL string.</returns>
    protected ConfigurationV2TestFixture<Program> CreateFactoryWithHcl(string hclConfig)
    {
        return new ConfigurationV2TestFixture<Program>(DatabaseFixture, hclConfig);
    }

    /// <summary>
    /// Helper method to create a minimal HCL configuration with STAC service.
    /// </summary>
    protected string CreateStacConfiguration(string layerName = "test_features", string tableName = "features")
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
            log_level   = "debug"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = env("DATABASE_URL")

            pool {
                min_size = 1
                max_size = 5
            }
        }

        service "stac" {
            enabled = true
        }

        layer "{{layerName}}" {
            title       = "{{layerName}}"
            data_source = data_source.test_db
            table       = "{{tableName}}"
            id_field    = "id"
            introspect_fields = true

            geometry {
                column = "geom"
                type   = "Polygon"
                srid   = 4326
            }

            services = [service.stac]
        }
        """;
    }

    /// <summary>
    /// Helper method to create a minimal HCL configuration with OGC API service.
    /// </summary>
    protected string CreateOgcApiConfiguration(string layerName = "test_features", string tableName = "features")
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
            log_level   = "debug"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = env("DATABASE_URL")

            pool {
                min_size = 1
                max_size = 5
            }
        }

        service "ogc_api" {
            enabled     = true
            item_limit  = 1000
            default_crs = "EPSG:4326"
        }

        layer "{{layerName}}" {
            title       = "{{layerName}}"
            data_source = data_source.test_db
            table       = "{{tableName}}"
            id_field    = "id"
            introspect_fields = true

            geometry {
                column = "geom"
                type   = "Polygon"
                srid   = 4326
            }

            services = [service.ogc_api]
        }
        """;
    }

    /// <summary>
    /// Helper method to create a minimal HCL configuration with WFS service.
    /// </summary>
    protected string CreateWfsConfiguration(string layerName = "test_features", string tableName = "features")
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
            log_level   = "debug"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = env("DATABASE_URL")

            pool {
                min_size = 1
                max_size = 5
            }
        }

        service "wfs" {
            enabled                    = true
            version                    = "2.0.0"
            capabilities_cache_duration = 3600
            default_count              = 100
            max_features               = 10000
        }

        layer "{{layerName}}" {
            title       = "{{layerName}}"
            data_source = data_source.test_db
            table       = "{{tableName}}"
            id_field    = "id"
            introspect_fields = true

            geometry {
                column = "geom"
                type   = "Polygon"
                srid   = 4326
            }

            services = [service.wfs]
        }
        """;
    }

    /// <summary>
    /// Disposes the test factory and HTTP client.
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
