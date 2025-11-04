using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Honua.Server.Core.Versioning;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Honua.Server.Host.Tests.Versioning;

/// <summary>
/// Tests for API versioning middleware and functionality.
/// </summary>
public class ApiVersioningTests
{
    [Fact]
    public void ApiVersioning_SupportedVersions_ContainsV1()
    {
        // Assert
        Assert.Contains("v1", ApiVersioning.SupportedVersions);
    }

    [Fact]
    public void ApiVersioning_CurrentVersion_IsV1()
    {
        // Assert
        Assert.Equal("v1", ApiVersioning.CurrentVersion);
    }

    [Theory]
    [InlineData("v1", true)]
    [InlineData("V1", true)]
    [InlineData("v2", false)]
    [InlineData("v0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ApiVersioning_IsVersionSupported_ReturnsExpectedResult(string? version, bool expected)
    {
        // Act
        var result = ApiVersioning.IsVersionSupported(version);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("v1", 1)]
    [InlineData("V1", 1)]
    [InlineData("v2", 2)]
    [InlineData("1", 1)]
    public void ApiVersioning_TryParseVersion_ValidVersions_ReturnsTrue(string version, int expectedMajor)
    {
        // Act
        var result = ApiVersioning.TryParseVersion(version, out var major);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedMajor, major);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("vX")]
    [InlineData("abc")]
    public void ApiVersioning_TryParseVersion_InvalidVersions_ReturnsFalse(string? version)
    {
        // Act
        var result = ApiVersioning.TryParseVersion(version, out var major);

        // Assert
        Assert.False(result);
        Assert.Equal(0, major);
    }

    [Theory]
    [InlineData(1, "v1")]
    [InlineData(2, "v2")]
    [InlineData(10, "v10")]
    public void ApiVersioning_FormatVersion_ReturnsExpectedFormat(int major, string expected)
    {
        // Act
        var result = ApiVersioning.FormatVersion(major);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApiVersion_Constructor_ValidVersion_CreatesInstance()
    {
        // Act
        var version = new ApiVersion(1, 0);

        // Assert
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.Equal("v1.0", version.VersionString);
    }

    [Fact]
    public void ApiVersion_Constructor_MajorOnly_CreatesInstance()
    {
        // Act
        var version = new ApiVersion(1);

        // Assert
        Assert.Equal(1, version.Major);
        Assert.Null(version.Minor);
        Assert.Equal("v1", version.VersionString);
    }

    [Fact]
    public void ApiVersion_Constructor_InvalidMajor_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiVersion(-1));
    }

    [Fact]
    public void ApiVersion_Constructor_InvalidMinor_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiVersion(1, -1));
    }

    [Theory]
    [InlineData("v1", 1, null)]
    [InlineData("V1", 1, null)]
    [InlineData("v1.0", 1, 0)]
    [InlineData("v2.5", 2, 5)]
    [InlineData("1", 1, null)]
    [InlineData("2.3", 2, 3)]
    public void ApiVersion_TryParse_ValidVersions_ReturnsTrue(string version, int expectedMajor, int? expectedMinor)
    {
        // Act
        var result = ApiVersion.TryParse(version, out var apiVersion);

        // Assert
        Assert.True(result);
        Assert.NotNull(apiVersion);
        Assert.Equal(expectedMajor, apiVersion.Major);
        Assert.Equal(expectedMinor, apiVersion.Minor);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("vX")]
    [InlineData("v1.2.3")]
    [InlineData("abc")]
    public void ApiVersion_TryParse_InvalidVersions_ReturnsFalse(string? version)
    {
        // Act
        var result = ApiVersion.TryParse(version, out var apiVersion);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ApiVersion_Parse_ValidVersion_ReturnsInstance()
    {
        // Act
        var version = ApiVersion.Parse("v1.0");

        // Assert
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [Fact]
    public void ApiVersion_Parse_InvalidVersion_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => ApiVersion.Parse("invalid"));
    }

    [Fact]
    public void ApiVersion_Equals_SameVersions_ReturnsTrue()
    {
        // Arrange
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(1, 0);

        // Act & Assert
        Assert.True(v1.Equals(v2));
        Assert.True(v1 == v2);
        Assert.False(v1 != v2);
    }

    [Fact]
    public void ApiVersion_Equals_DifferentVersions_ReturnsFalse()
    {
        // Arrange
        var v1 = new ApiVersion(1, 0);
        var v2 = new ApiVersion(2, 0);

        // Act & Assert
        Assert.False(v1.Equals(v2));
        Assert.False(v1 == v2);
        Assert.True(v1 != v2);
    }

    [Fact]
    public async Task ApiVersionMiddleware_ValidVersion_AddsVersionToContext()
    {
        // Arrange
        var builder = CreateTestHostBuilder(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiVersioning:AllowLegacyUrls"] = "false"
            });
        });

        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-API-Version"));
        Assert.Equal("v1", response.Headers.GetValues("X-API-Version").First());
    }

    [Fact]
    public async Task ApiVersionMiddleware_UnsupportedVersion_Returns400()
    {
        // Arrange
        var builder = CreateTestHostBuilder();
        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/v99/test");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("not supported", content);
    }

    [Fact]
    public async Task LegacyApiRedirectMiddleware_Enabled_RedirectsToVersioned()
    {
        // Arrange
        var builder = CreateTestHostBuilder(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiVersioning:AllowLegacyUrls"] = "true",
                ["ApiVersioning:LegacyRedirectVersion"] = "v1"
            });
        });

        using var server = new TestServer(builder);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Don't follow redirects automatically
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var noRedirectClient = new HttpClient(handler) { BaseAddress = server.BaseAddress };

        // Act
        var response = await noRedirectClient.GetAsync("/ogc/test");

        // Assert
        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.True(response.Headers.Location != null);
        Assert.Contains("/v1/ogc/test", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task LegacyApiRedirectMiddleware_Disabled_PassesThrough()
    {
        // Arrange
        var builder = CreateTestHostBuilder(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiVersioning:AllowLegacyUrls"] = "false"
            });
        });

        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/test");

        // Assert - Will get 404 as there's no non-versioned endpoint
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeprecationWarningMiddleware_DeprecatedVersion_AddsHeaders()
    {
        // Arrange
        var builder = CreateTestHostBuilder(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiVersioning:AllowLegacyUrls"] = "false",
                ["ApiVersioning:DeprecationWarnings:v1"] = "2026-12-31T23:59:59Z",
                ["ApiVersioning:DeprecationDocumentationUrl"] = "https://docs.example.com/deprecation"
            });
        });

        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.True(response.Headers.Contains("Sunset"));
        Assert.True(response.Headers.Contains("Link"));
        Assert.Equal("true", response.Headers.GetValues("Deprecation").First());
        Assert.Equal("2026-12-31T23:59:59Z", response.Headers.GetValues("Sunset").First());
    }

    [Fact]
    public async Task DeprecationWarningMiddleware_NonDeprecatedVersion_NoHeaders()
    {
        // Arrange
        var builder = CreateTestHostBuilder(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiVersioning:AllowLegacyUrls"] = "false"
                // No deprecation warnings configured
            });
        });

        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Sunset"));
    }

    [Theory]
    [InlineData("/v1/ogc/collections")]
    [InlineData("/v1/stac")]
    [InlineData("/v1/api/admin/ingestion")]
    [InlineData("/v1/records")]
    public async Task VersionedEndpoints_ValidPaths_ReturnVersionHeader(string path)
    {
        // Arrange
        var builder = CreateTestHostBuilder();
        using var server = new TestServer(builder);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert - May be 404 as we're not registering real endpoints, but should have version header
        Assert.True(response.Headers.Contains("X-API-Version"));
        Assert.Equal("v1", response.Headers.GetValues("X-API-Version").First());
    }

    private static IWebHostBuilder CreateTestHostBuilder(Action<IConfigurationBuilder>? configureAppConfiguration = null)
    {
        return new WebHostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                configureAppConfiguration?.Invoke(config);
            })
            .ConfigureServices(services =>
            {
                services.AddRouting();
            })
            .Configure(app =>
            {
                // Register versioning middleware
                app.UseLegacyApiRedirect();
                app.UseRouting();
                app.UseApiVersioning();
                app.UseDeprecationWarnings();

                // Add test endpoint
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/v1/test", () => Results.Ok(new { message = "test" }));
                    endpoints.MapGet("/v1/{*path}", () => Results.Ok(new { message = "catch-all" }));
                });
            });
    }
}
