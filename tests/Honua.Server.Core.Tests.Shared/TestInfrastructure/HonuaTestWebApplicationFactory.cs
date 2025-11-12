using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Analytics;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Tests.Shared;
using Honua.Server.Host;
using Honua.Server.Host.Hosting;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.OpenApi.Filters;
using Honua.Server.Host.Stac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Base WebApplicationFactory that consolidates common test configuration.
/// Provides a reusable foundation for integration tests with customization points
/// via virtual methods.
/// </summary>
/// <remarks>
/// <para>
/// This class eliminates ~500 lines of duplicate WebApplicationFactory configuration
/// across the test suite by providing common patterns:
/// <list type="bullet">
/// <item>Temporary file system setup for metadata, auth, and database files</item>
/// <item>In-memory configuration with sensible test defaults</item>
/// <item>Service removal and replacement for test doubles</item>
/// <item>Authentication bypass for test scenarios</item>
/// <item>Output caching disabled for predictable test behavior</item>
/// </list>
/// </para>
/// <para>
/// Derived classes can customize behavior by overriding:
/// <list type="bullet">
/// <item><see cref="ConfigureAppSettings"/> - Add/modify configuration values</item>
/// <item><see cref="ConfigureServices"/> - Replace services with test doubles</item>
/// <item><see cref="GetMetadataJson"/> - Provide custom metadata JSON</item>
/// <item><see cref="OnTempDirectoryCreated"/> - Initialize temp files after directory creation</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// public class MyTestFixture : HonuaTestWebApplicationFactory
/// {
///     protected override void ConfigureServices(IServiceCollection services)
///     {
///         base.ConfigureServices(services);
///         services.AddSingleton&lt;IFeatureRepository&gt;(_ => new MyTestRepository());
///     }
/// }
/// </code>
/// </example>
public class HonuaTestWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    /// <summary>
    /// Default admin username for test authentication.
    /// </summary>
    public const string DefaultAdminUsername = "admin";

    /// <summary>
    /// Default admin password for test authentication.
    /// </summary>
    public const string DefaultAdminPassword = "TestAdmin123!";
    private const string TestAuthenticationScheme = "HonuaTests";

    private readonly string _rootPath;
    private readonly string _metadataPath;
    private readonly string _authStorePath;
    private bool _bootstrapped;
    private HttpClient? _defaultClient;
    private bool _useTestAuthentication;

    /// <summary>
    /// Gets the root temporary directory path for this test instance.
    /// </summary>
    protected string RootPath => _rootPath;

    /// <summary>
    /// Gets the metadata JSON file path.
    /// </summary>
    protected string MetadataPath => _metadataPath;

    /// <summary>
    /// Gets the authentication store file path.
    /// </summary>
    protected string AuthStorePath => _authStorePath;

    /// <summary>
    /// Initializes a new instance of the test web application factory.
    /// Creates temporary directory structure and initializes test files.
    /// </summary>
    public HonuaTestWebApplicationFactory()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);

        _metadataPath = Path.Combine(_rootPath, "metadata.json");
        _authStorePath = Path.Combine(_rootPath, "auth.db");

        // Write metadata file
        var metadataJson = GetMetadataJson();
        File.WriteAllText(_metadataPath, metadataJson);

        // Allow derived classes to initialize additional files
        OnTempDirectoryCreated(_rootPath);
    }

    /// <summary>
    /// Gets the metadata JSON for this test instance.
    /// Override to provide custom metadata configuration.
    /// </summary>
    /// <returns>JSON string representing the service metadata.</returns>
    protected virtual string GetMetadataJson()
    {
        // Return minimal valid metadata by default
        return """
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
    }

    /// <summary>
    /// Called after the temporary directory is created.
    /// Override to initialize additional test files or resources.
    /// </summary>
    /// <param name="rootPath">The root temporary directory path.</param>
    protected virtual void OnTempDirectoryCreated(string rootPath)
    {
        // Default: no-op
    }

    /// <summary>
    /// Configures the web host for testing.
    /// </summary>
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
                ["honua:authentication:mode"] = "Local",
                ["honua:authentication:enforce"] = "true",
                ["honua:authentication:quickStart:enabled"] = "false",
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

                // STAC disabled by default (derived classes can enable)
                ["honua:services:stac:enabled"] = "false",

                // Redis connection (won't be used in most tests)
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            };

            // Allow derived classes to customize settings
            ConfigureAppSettings(settings);

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            // Remove and replace common services with test doubles
            RemoveDefaultServices(services);

            // Configure core test services
            ConfigureCoreTestServices(services);

            // Allow derived classes to customize services
            ConfigureServices(services);

            // Configure no-op implementations for features we don't need in tests
            ConfigureNoOpServices(services);
        });
    }

    /// <summary>
    /// Removes default services that will be replaced with test doubles.
    /// Override to customize which services are removed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void RemoveDefaultServices(IServiceCollection services)
    {
        services.RemoveAll<IHonuaConfigurationService>();
        services.RemoveAll<IMetadataProvider>();
        services.RemoveAll<IMetadataRegistry>();
        services.RemoveAll<IFeatureRepository>();
        services.RemoveAll<IOutputCacheInvalidationService>();
        services.RemoveAll<IOutputCacheStore>();
        services.RemoveAll<IStyleRepository>();

        // Remove STAC synchronization hosted service
        var hostedServices = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Name == "StacCatalogSynchronizationHostedService")
            .ToList();

        foreach (var descriptor in hostedServices)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Configures core test services (metadata, configuration, etc.).
    /// Override to customize core service implementations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureCoreTestServices(IServiceCollection services)
    {
        // Configuration service
        services.AddSingleton<IHonuaConfigurationService>(_ => new HonuaConfigurationService(new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = _metadataPath
            }
        }));

        // Metadata services
        services.AddSingleton<IMetadataProvider>(_ => new JsonMetadataProvider(_metadataPath));
        services.AddSingleton<IMetadataRegistry>(sp =>
        {
            var provider = sp.GetRequiredService<IMetadataProvider>();
            var logger = sp.GetRequiredService<ILogger<MetadataRegistry>>();
            return new MetadataRegistry(provider, logger);
        });
    }

    /// <summary>
    /// Configures no-op implementations for services not needed in tests.
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureNoOpServices(IServiceCollection services)
    {
        // No-op output caching
        services.AddSingleton<IOutputCacheInvalidationService, NoOpOutputCacheInvalidationService>();
        services.AddSingleton<IOutputCacheStore, NoOpOutputCacheStore>();
        services.AddOutputCache();

        // In-memory style repository
        services.AddSingleton<IStyleRepository, InMemoryStyleRepository>();
        services.AddSingleton<IFeatureRepository, StubFeatureRepository>();
        services.AddSingleton<IFeatureAttachmentOrchestrator, StubAttachmentOrchestrator>();
        services.AddSingleton<IRasterAnalyticsService, StubRasterAnalyticsService>();

        // Disable Swagger version info filter
        services.PostConfigure<SwaggerGenOptions>(options =>
        {
            options.DocumentFilterDescriptors.RemoveAll(descriptor =>
                descriptor.Type == typeof(VersionInfoDocumentFilter));
        });
    }

    /// <summary>
    /// Allows derived classes to modify app settings.
    /// Called during ConfigureWebHost.
    /// </summary>
    /// <param name="settings">Dictionary of configuration settings to modify.</param>
    protected virtual void ConfigureAppSettings(Dictionary<string, string?> settings)
    {
        // Default: no additional configuration
    }

    /// <summary>
    /// Allows derived classes to customize service registration.
    /// Called after core services are configured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthenticationScheme;
            options.DefaultChallengeScheme = TestAuthenticationScheme;
            options.DefaultScheme = TestAuthenticationScheme;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationScheme, _ => { });

        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthenticationScheme;
            options.DefaultChallengeScheme = TestAuthenticationScheme;
            options.DefaultScheme = TestAuthenticationScheme;
        });

        services.PostConfigure<AuthorizationOptions>(options =>
        {
            options.AddPolicy("RequireAdministrator", policy => policy.RequireAssertion(_ => true));
            options.AddPolicy("RequireDataPublisher", policy => policy.RequireAssertion(_ => true));
            options.AddPolicy("RequireViewer", policy => policy.RequireAssertion(_ => true));
        });

        _useTestAuthentication = true;
    }

    /// <summary>
    /// Creates an HTTP client configured for authenticated requests.
    /// Automatically handles authentication bootstrap and token retrieval.
    /// </summary>
    /// <returns>An authenticated HTTP client.</returns>
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
        EnsureCsrfTokenAsync(_defaultClient).GetAwaiter().GetResult();

        if (!_useTestAuthentication)
        {
            EnsureBootstrapAsync().GetAwaiter().GetResult();
            AuthenticateClientAsync(_defaultClient).GetAwaiter().GetResult();
        }

        return _defaultClient;
    }

    /// <summary>
    /// Ensures the authentication system is bootstrapped.
    /// </summary>
    protected async Task EnsureBootstrapAsync()
    {
        if (_bootstrapped)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IAuthBootstrapService>();
        var result = await bootstrap.BootstrapAsync().ConfigureAwait(false);

        if (result.Status == AuthBootstrapStatus.Failed)
        {
            throw new InvalidOperationException($"Bootstrap failed: {result.Message}");
        }

        _bootstrapped = true;
    }

    /// <summary>
    /// Authenticates an HTTP client using the default admin credentials.
    /// </summary>
    /// <param name="client">The HTTP client to authenticate.</param>
    protected static async Task AuthenticateClientAsync(HttpClient client)
    {
        if (client.DefaultRequestHeaders.Authorization is not null)
        {
            return;
        }

        await EnsureCsrfTokenAsync(client).ConfigureAwait(false);

        var response = await client.PostAsJsonAsync(
            "/api/auth/local/login",
            new { username = DefaultAdminUsername, password = DefaultAdminPassword }
        ).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        var token = payload.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        ClearCsrfHeaders(client);
        await EnsureCsrfTokenAsync(client).ConfigureAwait(false);
    }

    private static async Task EnsureCsrfTokenAsync(HttpClient client)
    {
        var baseAddress = client.BaseAddress ?? new Uri("https://localhost");
        var origin = baseAddress.GetLeftPart(UriPartial.Authority);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/security/csrf-token");
        request.Headers.Referrer = baseAddress;
        request.Headers.TryAddWithoutValidation("Origin", origin);

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("token", out var tokenElement))
        {
            return;
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        var headerName = document.RootElement.TryGetProperty("headerName", out var headerElement)
            ? headerElement.GetString() ?? "X-CSRF-Token"
            : "X-CSRF-Token";

        client.DefaultRequestHeaders.Remove(headerName);
        client.DefaultRequestHeaders.Add(headerName, token);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", origin);
    }

    private static void ClearCsrfHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Remove("__Host-X-CSRF-Token");
        client.DefaultRequestHeaders.Remove("__RequestVerificationToken");
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "honua-test-user"),
                new(ClaimTypes.NameIdentifier, "honua-test-user"),
                new(ClaimTypes.Role, "administrator"),
                new(ClaimTypes.Role, "datapublisher"),
                new(ClaimTypes.Role, "viewer")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    /// <summary>
    /// Disposes resources and cleans up temporary files.
    /// </summary>
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

    /// <summary>
    /// No-op implementation of IOutputCacheInvalidationService for testing.
    /// </summary>
    private sealed class NoOpOutputCacheInvalidationService : IOutputCacheInvalidationService
    {
        public Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// No-op implementation of IOutputCacheStore for testing.
    /// </summary>
    private sealed class NoOpOutputCacheStore : IOutputCacheStore
    {
        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask EvictByTagAsync(string[] tags, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Array.Empty<byte>());

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask EvictAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// In-memory implementation of IStyleRepository for testing.
    /// </summary>
    private sealed class InMemoryStyleRepository : IStyleRepository
    {
        private readonly Dictionary<string, StyleDefinition> _styles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<StyleVersion>> _versions = new(StringComparer.OrdinalIgnoreCase);

        public Task<StyleDefinition?> GetAsync(string styleId, CancellationToken cancellationToken = default)
        {
            _styles.TryGetValue(styleId, out var style);
            return Task.FromResult(style);
        }

        public Task<IReadOnlyList<StyleDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StyleDefinition>>(_styles.Values.ToList());
        }

        public Task<StyleDefinition> CreateAsync(StyleDefinition style, string? createdBy = null, CancellationToken cancellationToken = default)
        {
            _styles[style.Id] = style;

            var version = new StyleVersion
            {
                StyleId = style.Id,
                Version = 1,
                Definition = style,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = createdBy
            };

            _versions[style.Id] = new List<StyleVersion> { version };
            return Task.FromResult(style);
        }

        public Task<StyleDefinition> UpdateAsync(string styleId, StyleDefinition style, string? updatedBy = null, CancellationToken cancellationToken = default)
        {
            _styles[styleId] = style;

            if (!_versions.TryGetValue(styleId, out var versionList))
            {
                versionList = new List<StyleVersion>();
                _versions[styleId] = versionList;
            }

            var version = new StyleVersion
            {
                StyleId = styleId,
                Version = versionList.Count + 1,
                Definition = style,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = updatedBy
            };

            versionList.Add(version);
            return Task.FromResult(style);
        }

        public Task<bool> DeleteAsync(string styleId, string? deletedBy = null, CancellationToken cancellationToken = default)
        {
            var removed = _styles.Remove(styleId);
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<StyleVersion>> GetVersionHistoryAsync(string styleId, CancellationToken cancellationToken = default)
        {
            if (_versions.TryGetValue(styleId, out var versionList))
            {
                return Task.FromResult<IReadOnlyList<StyleVersion>>(versionList);
            }
            return Task.FromResult<IReadOnlyList<StyleVersion>>(Array.Empty<StyleVersion>());
        }

        public Task<StyleDefinition?> GetVersionAsync(string styleId, int version, CancellationToken cancellationToken = default)
        {
            if (_versions.TryGetValue(styleId, out var versionList))
            {
                var styleVersion = versionList.FirstOrDefault(v => v.Version == version);
                return Task.FromResult(styleVersion?.Definition);
            }
            return Task.FromResult<StyleDefinition?>(null);
        }

        public Task<bool> ExistsAsync(string styleId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_styles.ContainsKey(styleId));
        }
    }

    private sealed class StubRasterAnalyticsService : IRasterAnalyticsService
    {
        public Task<RasterStatistics> CalculateStatisticsAsync(RasterStatisticsRequest request, CancellationToken cancellationToken = default)
        {
            var stats = new RasterStatistics(
                request.Dataset.Id,
                1,
                new[]
                {
                    new BandStatistics(0, 0, 0, 0, 0, 0, 0, 0, null)
                },
                request.BoundingBox);
            return Task.FromResult(stats);
        }

        public Task<RasterAlgebraResult> CalculateAlgebraAsync(RasterAlgebraRequest request, CancellationToken cancellationToken = default)
        {
            var statistics = new RasterStatistics(
                request.Datasets.FirstOrDefault()?.Id ?? "algebra",
                request.Datasets.Count,
                Array.Empty<BandStatistics>(),
                request.BoundingBox);

            return Task.FromResult(new RasterAlgebraResult(
                Array.Empty<byte>(),
                "application/octet-stream",
                request.Width,
                request.Height,
                statistics));
        }

        public Task<RasterValueExtractionResult> ExtractValuesAsync(RasterValueExtractionRequest request, CancellationToken cancellationToken = default)
        {
            var values = request.Points
                .Select(point => new PointValue(point.X, point.Y, null, request.BandIndex ?? 0))
                .ToList();

            return Task.FromResult(new RasterValueExtractionResult(
                request.Dataset.Id,
                values));
        }

        public Task<RasterHistogram> CalculateHistogramAsync(RasterHistogramRequest request, CancellationToken cancellationToken = default)
        {
            var bins = Enumerable.Range(0, request.BinCount)
                .Select(index => new HistogramBin(index, index + 1, 0))
                .ToList();

            return Task.FromResult(new RasterHistogram(
                request.Dataset.Id,
                request.BandIndex ?? 0,
                bins,
                0,
                0));
        }

        public Task<ZonalStatisticsResult> CalculateZonalStatisticsAsync(ZonalStatisticsRequest request, CancellationToken cancellationToken = default)
        {
            var zones = request.Zones
                .Select((zone, index) => new ZoneStatistics(
                    zone.ZoneId ?? $"zone-{index}",
                    request.BandIndex ?? 0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    zone.Coordinates.Count,
                    null,
                    zone.Properties))
                .ToList();

            return Task.FromResult(new ZonalStatisticsResult(request.Dataset.Id, zones));
        }

        public Task<TerrainAnalysisResult> CalculateTerrainAsync(TerrainAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            var stats = new TerrainAnalysisStatistics(0, 0, 0, 0, "unitless");
            return Task.FromResult(new TerrainAnalysisResult(
                Array.Empty<byte>(),
                "application/octet-stream",
                request.Width,
                request.Height,
                request.AnalysisType,
                stats));
        }

        public RasterAnalyticsCapabilities GetCapabilities()
        {
            return new RasterAnalyticsCapabilities(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Enum.GetNames<TerrainAnalysisType>(),
                MaxAlgebraDatasets: 4,
                MaxExtractionPoints: 100,
                MaxHistogramBins: 256);
        }
    }
}
