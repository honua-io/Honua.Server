// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Cli.AI.Configuration;
using Honua.Cli.AI.HealthChecks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Azure;
using Honua.Cli.AI.Services;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Telemetry;
using System.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Services.Discovery;

namespace Honua.Cli.AI.Extensions;

/// <summary>
/// Extension methods for configuring Azure AI services in dependency injection.
/// </summary>
public static class AzureAIServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure AI services to the service collection.
    /// Registers LLM provider, embedding provider, knowledge store, and approval service.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">Configuration containing Azure AI settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate configuration
        ValidateConfiguration(configuration);

        // Register LlmProviderOptions
        services.Configure<LlmProviderOptions>(configuration.GetSection("LlmProvider"));

        // Register HTTP clients for LLM providers
        RegisterHttpClients(services, configuration);

        // Register LLM provider factory
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        // Register embedding provider
        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var options = new LlmProviderOptions();
            configuration.GetSection("LlmProvider").Bind(options);

            return new AzureOpenAIEmbeddingProvider(options);
        });

        // Register memory cache for pattern search caching
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100; // Limit cache to 100 entries
        });

        // Register knowledge store (will be decorated with caching)
        services.AddSingleton<IDeploymentPatternKnowledgeStore, AzureAISearchKnowledgeStore>();

        // Decorate with caching layer
        services.Decorate<IDeploymentPatternKnowledgeStore>((inner, sp) =>
        {
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<CachedDeploymentPatternKnowledgeStore>>();
            return new CachedDeploymentPatternKnowledgeStore(inner, cache, logger);
        });

        // Register pattern explainer
        services.AddSingleton<PatternExplainer>();

        // Register pattern usage telemetry
        services.AddSingleton<IPatternUsageTelemetry, PostgresPatternUsageTelemetry>();

        // Register pattern approval service
        services.AddScoped<PatternApprovalService>();

        // Agent capability configuration and selector
        services.Configure<AgentCapabilityOptions>(configuration.GetSection("AgentCapabilities"));
        services.AddSingleton<AgentCapabilityRegistry>();
        services.AddSingleton<IntelligentAgentSelector>();
        services.AddSingleton<IAgentCritic, PlanSafetyCritic>();

        // Intelligent agent selection (P1 #15)
        services.Configure<AgentSelectionOptions>(configuration.GetSection("AgentSelection"));
        services.AddSingleton<IAgentSelectionService, LlmAgentSelectionService>();

        // Register agent history store
        services.AddSingleton<IAgentHistoryStore, PostgresAgentHistoryStore>();

        // Ensure telemetry service is available (defaults to no-op if not configured)
        services.TryAddSingleton<ITelemetryService, NullTelemetryService>();
        services.AddHttpClient("GuardrailMetrics");

        // Guardrail services (resource envelopes, validation, telemetry feedback)
        services.AddSingleton<IResourceEnvelopeCatalog, ResourceEnvelopeCatalog>();
        services.AddSingleton<IDeploymentGuardrailValidator, DeploymentGuardrailValidator>();
        services.AddSingleton<IDeploymentGuardrailMonitor, PostDeployGuardrailMonitor>();
        services.AddSingleton<IDeploymentMetricsProvider, HttpDeploymentMetricsProvider>();
        services.TryAddSingleton<IAwsCli>(_ => DefaultAwsCli.Shared);
        services.TryAddSingleton<IAzureCli>(_ => DefaultAzureCli.Shared);
        services.TryAddSingleton<IGcloudCli>(_ => DefaultGcloudCli.Shared);
        services.TryAddSingleton<ICloudDiscoveryService, CloudDiscoveryService>();

        // Register parameter extraction service for Process Framework
        services.AddScoped<Services.Processes.ParameterExtractionService>();

        // Register DNS control validator for certificate renewal
        RegisterDnsControlValidator(services, configuration);

        // Register Process Framework steps (22 steps total)
        RegisterProcessSteps(services);

        // Register Process State Store (Redis with in-memory fallback)
        RegisterProcessStateStore(services, configuration);

        return services;
    }

    /// <summary>
    /// Adds Azure AI services with custom LlmProviderOptions configuration.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptions">Action to configure LlmProviderOptions</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureAI(
        this IServiceCollection services,
        Action<LlmProviderOptions> configureOptions)
    {
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var options = new LlmProviderOptions();
        configureOptions(options);

        // Validate options
        ValidateOptions(options);

        // Register configured options
        services.AddSingleton(options);

        // Register LLM provider factory
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        // Register embedding provider
        services.AddSingleton<IEmbeddingProvider>(sp =>
            new AzureOpenAIEmbeddingProvider(options));

        // Register knowledge store
        services.AddSingleton<AzureAISearchKnowledgeStore>();

        // Register pattern approval service
        services.AddScoped<PatternApprovalService>();

        return services;
    }

    /// <summary>
    /// Adds only the Azure OpenAI LLM provider (without embedding, search, or approval services).
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">Configuration containing Azure OpenAI settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureOpenAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LlmProviderOptions>(configuration.GetSection("LlmProvider"));
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        return services;
    }

    /// <summary>
    /// Adds only the embedding provider (for vector search scenarios).
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">Configuration containing embedding settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var options = new LlmProviderOptions();
            configuration.GetSection("LlmProvider").Bind(options);

            return new AzureOpenAIEmbeddingProvider(options);
        });

        return services;
    }

    /// <summary>
    /// Adds the Azure AI Search knowledge store for pattern search.
    /// Requires IEmbeddingProvider to be registered first.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureKnowledgeStore(
        this IServiceCollection services)
    {
        services.AddSingleton<AzureAISearchKnowledgeStore>();
        return services;
    }

    /// <summary>
    /// Adds the pattern approval service for managing deployment patterns.
    /// Requires AzureAISearchKnowledgeStore to be registered first.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPatternApprovalService(
        this IServiceCollection services)
    {
        services.AddScoped<PatternApprovalService>();
        return services;
    }

    /// <summary>
    /// Adds health checks for all Azure AI services.
    /// </summary>
    /// <param name="builder">The health checks builder</param>
    /// <returns>The health checks builder for chaining</returns>
    public static IHealthChecksBuilder AddAzureAIHealthChecks(
        this IHealthChecksBuilder builder)
    {
        builder.AddCheck<AzureOpenAIHealthCheck>(
            "azure_openai",
            HealthStatus.Degraded,
            tags: new[] { "azure", "ai", "llm" });

        builder.AddCheck<AzureEmbeddingHealthCheck>(
            "azure_embeddings",
            HealthStatus.Degraded,
            tags: new[] { "azure", "ai", "embeddings" });

        builder.AddCheck<AzureAISearchHealthCheck>(
            "azure_ai_search",
            HealthStatus.Degraded,
            tags: new[] { "azure", "search", "knowledge" });

        builder.AddCheck<OllamaHealthCheck>(
            "ollama",
            HealthStatus.Degraded,
            tags: new[] { "ollama", "ai", "llm", "local" });

        return builder;
    }

    private static void ValidateConfiguration(IConfiguration configuration)
    {
        var llmSection = configuration.GetSection("LlmProvider");
        if (!llmSection.Exists())
        {
            throw new InvalidOperationException(
                "Configuration section 'LlmProvider' is missing. " +
                "Please configure Azure OpenAI settings in appsettings.json.");
        }

        var azureSection = llmSection.GetSection("Azure");
        if (!azureSection.Exists())
        {
            throw new InvalidOperationException(
                "Configuration section 'LlmProvider:Azure' is missing. " +
                "Please configure Azure OpenAI endpoint and API key.");
        }

        var endpoint = azureSection["EndpointUrl"];
        if (endpoint.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint URL is not configured. " +
                "Set 'LlmProvider:Azure:EndpointUrl' in appsettings.json.");
        }

        var apiKey = azureSection["ApiKey"];
        if (apiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "Azure OpenAI API key is not configured. " +
                "Set 'LlmProvider:Azure:ApiKey' in appsettings.json or environment variables.");
        }
    }

    private static void ValidateOptions(LlmProviderOptions options)
    {
        if (options.Azure == null)
        {
            throw new InvalidOperationException(
                "Azure OpenAI options are not configured. " +
                "Please provide AzureOpenAIOptions in the configuration.");
        }

        if (options.Azure.EndpointUrl.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint URL is required.");
        }

        if (options.Azure.ApiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "Azure OpenAI API key is required.");
        }
    }

    /// <summary>
    /// Registers DNS control validator with optional provider configurations.
    /// Supports Cloudflare, Azure DNS, and AWS Route53.
    /// </summary>
    private static void RegisterDnsControlValidator(IServiceCollection services, IConfiguration configuration)
    {
        // Register DNS provider options from configuration
        var cloudflareSection = configuration.GetSection("DnsProviders:Cloudflare");
        var azureSection = configuration.GetSection("DnsProviders:Azure");
        var route53Section = configuration.GetSection("DnsProviders:Route53");

        Services.Certificates.DnsChallenge.CloudflareDnsProviderOptions? cloudflareOptions = null;
        Services.Certificates.AzureDnsOptions? azureOptions = null;
        Services.Certificates.Route53DnsOptions? route53Options = null;

        if (cloudflareSection.Exists())
        {
            cloudflareOptions = new Services.Certificates.DnsChallenge.CloudflareDnsProviderOptions();
            cloudflareSection.Bind(cloudflareOptions);
        }

        if (azureSection.Exists())
        {
            azureOptions = new Services.Certificates.AzureDnsOptions();
            azureSection.Bind(azureOptions);
        }

        if (route53Section.Exists())
        {
            route53Options = new Services.Certificates.Route53DnsOptions();
            route53Section.Bind(route53Options);
        }

        // Register the DNS control validator
        services.AddSingleton<Services.Certificates.IDnsControlValidator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Services.Certificates.DnsControlValidator>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            // Try to get Azure credentials if available
            Azure.Core.TokenCredential? azureCredential = null;
            try
            {
                azureCredential = sp.GetService<Azure.Core.TokenCredential>();
            }
            catch
            {
                // Azure credentials not available, that's okay
            }

            return new Services.Certificates.DnsControlValidator(
                logger,
                httpClientFactory,
                cloudflareOptions,
                azureOptions,
                route53Options,
                azureCredential);
        });
    }

    /// <summary>
    /// Registers all Process Framework steps for dependency injection.
    /// This includes all 41 steps across 7 workflows: Deployment, Upgrade, Metadata, GitOps, Benchmark, Certificate Renewal, and Network Diagnostics.
    /// </summary>
    private static void RegisterProcessSteps(IServiceCollection services)
    {
        // Deployment process steps (8 steps)
        services.AddTransient<Services.Processes.Steps.Deployment.ValidateDeploymentRequirementsStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.GenerateInfrastructureCodeStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.ReviewInfrastructureStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.DeployInfrastructureStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.ConfigureServicesStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.DeployHonuaApplicationStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.ValidateDeploymentStep>();
        services.AddTransient<Services.Processes.Steps.Deployment.ConfigureObservabilityStep>();

        // Upgrade process steps (4 steps)
        services.AddTransient<Services.Processes.Steps.Upgrade.DetectCurrentVersionStep>();
        services.AddTransient<Services.Processes.Steps.Upgrade.BackupDatabaseStep>();
        services.AddTransient<Services.Processes.Steps.Upgrade.CreateBlueEnvironmentStep>();
        services.AddTransient<Services.Processes.Steps.Upgrade.SwitchTrafficStep>();

        // Metadata process steps (3 steps)
        services.AddTransient<Services.Processes.Steps.Metadata.ExtractMetadataStep>();
        services.AddTransient<Services.Processes.Steps.Metadata.GenerateStacItemStep>();
        services.AddTransient<Services.Processes.Steps.Metadata.PublishStacStep>();

        // GitOps process steps (3 steps)
        services.AddTransient<Services.Processes.Steps.GitOps.ValidateGitConfigStep>();
        services.AddTransient<Services.Processes.Steps.GitOps.SyncConfigStep>();
        services.AddTransient<Services.Processes.Steps.GitOps.MonitorDriftStep>();

        // Benchmark process steps (4 steps)
        services.AddTransient<Services.Processes.Steps.Benchmark.SetupBenchmarkStep>();
        services.AddTransient<Services.Processes.Steps.Benchmark.RunBenchmarkStep>();
        services.AddTransient<Services.Processes.Steps.Benchmark.AnalyzeResultsStep>();
        services.AddTransient<Services.Processes.Steps.Benchmark.GenerateReportStep>();

        // Certificate Renewal process steps (9 steps)
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.ScanCertificatesStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.CheckExpirationStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.RequestRenewalStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.ValidateDomainStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.IssueCertificateStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.DeployCertificateStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.ValidateCertificateDeploymentStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.UpdateMonitoringStep>();
        services.AddTransient<Services.Processes.Steps.CertificateRenewal.NotifyCompletionStep>();

        // Network Diagnostics process steps (10 steps)
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.CollectSymptomsStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.TestDNSResolutionStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.TestConnectivityStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.CheckFirewallRulesStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.TestPortAccessStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.CheckCertificateStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.AnalyzeLatencyStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.TraceRouteStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.IdentifyRootCauseStep>();
        services.AddTransient<Services.Processes.Steps.NetworkDiagnostics.GenerateReportStep>();
    }

    /// <summary>
    /// Registers the process state store with Redis support and in-memory fallback.
    /// If Redis is configured and enabled, uses RedisProcessStateStore.
    /// Otherwise, falls back to InMemoryProcessStateStore.
    /// </summary>
    private static void RegisterProcessStateStore(IServiceCollection services, IConfiguration configuration)
    {
        // Register RedisOptions configuration with validation
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));
        services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();

        services.AddSingleton<RedisProcessStateStoreHolder>(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptions<RedisOptions>>();
            var options = optionsMonitor.Value;
            var logger = sp.GetRequiredService<ILogger<RedisProcessStateStore>>();

            if (!options.Enabled || options.ConnectionString.IsNullOrWhiteSpace())
            {
                logger.LogInformation("Redis is not configured for process state storage; using in-memory store.");
                return new RedisProcessStateStoreHolder(null, null);
            }

            var connection = TryConnectToRedis(options, logger);
            if (connection is null)
            {
                logger.LogWarning(
                    "Failed to establish Redis connection for process state storage. Falling back to in-memory store.");
                return new RedisProcessStateStoreHolder(null, null);
            }

            var store = new RedisProcessStateStore(connection, optionsMonitor, logger);
            logger.LogInformation("Using Redis-backed process state store");
            return new RedisProcessStateStoreHolder(connection, store);
        });

        services.AddSingleton<IProcessStateStore>(sp =>
        {
            var holder = sp.GetRequiredService<RedisProcessStateStoreHolder>();
            if (holder.Store is not null)
            {
                return holder.Store;
            }

            var memoryLogger = sp.GetRequiredService<ILogger<InMemoryProcessStateStore>>();
            memoryLogger.LogWarning(
                "Redis is not available. Using in-memory process state store. " +
                "This is not suitable for production or multi-instance deployments.");
            return new InMemoryProcessStateStore(memoryLogger);
        });

        services.AddSingleton<ProcessStateStoreHealthCheck>();

        services.AddHealthChecks()
            .AddCheck<ProcessStateStoreHealthCheck>("process_state_store");
    }

    private static IConnectionMultiplexer? TryConnectToRedis(RedisOptions options, ILogger logger)
    {
        try
        {
            var redisOptions = ConfigurationOptions.Parse(options.ConnectionString);
            redisOptions.ConnectTimeout = options.ConnectTimeoutMs;
            redisOptions.SyncTimeout = options.SyncTimeoutMs;
            redisOptions.AbortOnConnectFail = false;

            var connectTask = ConnectionMultiplexer.ConnectAsync(redisOptions);
            return connectTask.WaitAsync(TimeSpan.FromMilliseconds(options.ConnectTimeoutMs))
                .GetAwaiter()
                .GetResult();
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Timed out while connecting to Redis process state store.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to Redis process state store.");
            return null;
        }
    }

    private sealed class RedisProcessStateStoreHolder : IDisposable, IAsyncDisposable
    {
        public RedisProcessStateStoreHolder(IConnectionMultiplexer? connection, RedisProcessStateStore? store)
        {
            Connection = connection;
            Store = store;
        }

        public IConnectionMultiplexer? Connection { get; }

        public RedisProcessStateStore? Store { get; }

        public void Dispose()
        {
            Store?.Dispose();
            Connection?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Registers HTTP clients for LLM providers that require HTTP communication.
    /// This includes Ollama, LocalAI, and other HTTP-based providers.
    /// </summary>
    private static void RegisterHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        // Register Ollama HTTP client
        services.AddHttpClient("Ollama", (sp, client) =>
        {
            var options = new LlmProviderOptions();
            configuration.GetSection("LlmProvider").Bind(options);

            var endpoint = options.Ollama?.EndpointUrl ?? "http://localhost:11434";
            client.BaseAddress = new Uri(endpoint.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register LocalAI HTTP client
        services.AddHttpClient("LocalAI", (sp, client) =>
        {
            var options = new LlmProviderOptions();
            configuration.GetSection("LlmProvider").Bind(options);

            var endpoint = options.LocalAI?.EndpointUrl ?? "http://localhost:8080";
            client.BaseAddress = new Uri(endpoint.TrimEnd('/'));
            client.Timeout = TimeSpan.FromMinutes(5); // LocalAI can be slow for local inference
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
    }
}
