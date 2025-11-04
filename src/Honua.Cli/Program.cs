// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using Honua.Cli.AI.Secrets;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Honua.Cli.AI.Services.Telemetry;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.Commands;
using Honua.Cli.Commands.GitOps;
using Honua.Cli.Infrastructure;
using Honua.Cli.Services;
using Honua.Cli.Services.Consultant;
using Honua.Cli.Services.Configuration;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Services.Metadata;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.AI.Services.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton<IHonuaCliEnvironment, HonuaCliEnvironment>();
        builder.Services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
        builder.Services.AddSingleton<ISystemClock, SystemClock>();
        // Consultant planner will be registered in ConfigureAiServices
        builder.Services.AddSingleton<IConsultantPlanFormatter, TableConsultantPlanFormatter>();
        builder.Services.AddSingleton<IConsultantExecutor, ConsultantExecutor>();
        builder.Services.AddSingleton<IConsultantContextBuilder, ConsultantContextBuilder>();
        builder.Services.AddSingleton<IConsultantSessionStore, FileConsultantSessionStore>();
        builder.Services.AddSingleton<IConsultantWorkflow, ConsultantWorkflow>();

        // Add memory cache support
        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<IMetadataSchemaValidator>(sp =>
        {
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            return MetadataSchemaValidator.CreateDefault(cache);
        });
        builder.Services.AddSingleton<IMetadataSnapshotService, FileMetadataSnapshotService>();
        builder.Services.AddSingleton<ISessionLogWriter, SessionLogWriter>();
        builder.Services.AddSingleton<IHonuaCliConfigStore, HonuaCliConfigStore>();
        builder.Services.AddSingleton<IControlPlaneConnectionResolver, ControlPlaneConnectionResolver>();
        builder.Services.AddSingleton<IDataIngestionApiClient, DataIngestionApiClient>();
        builder.Services.AddSingleton<IMigrationApiClient, MigrationApiClient>();
        builder.Services.AddSingleton<IRasterTileCacheApiClient, RasterTileCacheApiClient>();
        builder.Services.AddSingleton<RasterStacCatalogBuilder>();
        builder.Services.AddSingleton<VectorStacCatalogBuilder>();

        // Register HttpClient factory for dependency injection
        builder.Services.AddHttpClient();

        builder.Services.AddHttpClient("honua-control-plane", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });
        builder.Services.AddHttpClient("SemanticKernel-OpenAI", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        builder.Services.AddHttpClient("SemanticKernel-Azure", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        builder.Services.AddHttpClient("TestConnection", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient("ImportWizard", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        builder.Services.AddSingleton<IValidateOptions<HonuaAuthenticationOptions>, HonuaAuthenticationOptionsValidator>();
        builder.Services.AddOptions<HonuaAuthenticationOptions>()
            .Bind(builder.Configuration.GetSection(HonuaAuthenticationOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddOptions<ConnectionStringOptions>()
            .Bind(builder.Configuration.GetSection(ConnectionStringOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<IAuthRepository>(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<HonuaAuthenticationOptions>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var basePath = builder.Environment.ContentRootPath ?? Directory.GetCurrentDirectory();
            var dataAccess = sp.GetService<IOptions<DataAccessOptions>>();
            var connectionStrings = sp.GetService<IOptions<ConnectionStringOptions>>();
            return AuthRepositoryFactory.CreateRepository(basePath, optionsMonitor, loggerFactory, metrics: null, dataAccessOptions: dataAccess, connectionStrings: connectionStrings);
        });

        builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

        builder.Services.AddSingleton<IAuthBootstrapService, AuthBootstrapService>();

        // AI Services Configuration
        ConfigureAiServices(builder.Services, builder.Configuration);

        var registrar = new TypeRegistrar(builder.Services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("honua");
            config.PropagateExceptions();
            config.AddCommand<SetupWizardCommand>("setup")
                .WithDescription("Interactive setup wizard for first-time Honua configuration.")
                .WithExample("honua", "setup")
                .WithExample("honua", "setup", "--deployment-target", "development", "--database-type", "postgis");

            config.AddCommand<ConsultantCommand>("consultant")
                .WithDescription("Launch the Honua AI consultant (plan-only preview).")
                .WithExample("honua", "consultant", "--dry-run", "--prompt", "connect my PostGIS database");

            config.AddCommand<ConsultantRefineCommand>("consultant-refine")
                .WithDescription("Refine a previous consultant plan based on feedback.")
                .WithExample("honua", "consultant-refine", "--session", "20250110-123456-ABCD1234", "--adjustment", "make it more secure")
                .WithExample("honua", "consultant-refine");

            config.AddCommand<ConsultantPatternsIngestCommand>("consultant-patterns")
                .WithDescription("Ingest deployment patterns into the consultant knowledge base.")
                .WithExample("honua", "consultant-patterns", "--file", "patterns.json");

            config.AddCommand<StatusCommand>("status")
                .WithDescription("Check Honua host health and authentication status using the configured defaults.")
                .WithExample("honua", "status");

            config.AddCommand<InitProjectCommand>("init")
                .WithDescription("Initialize a new Honua project with scaffolded configuration files.")
                .WithExample("honua", "init")
                .WithExample("honua", "init", "my-project", "--name", "My Project", "--database", "PostgreSQL", "--docker");

            config.AddCommand<TestConnectionCommand>("test")
                .WithDescription("Test connectivity to Honua server, database, and cloud storage.")
                .WithExample("honua", "test")
                .WithExample("honua", "test", "--connection-string", "Host=localhost;Database=honua;Username=honua", "--provider", "postgis");

            config.AddBranch("metadata", metadata =>
            {
                metadata.SetDescription("Manage Honua metadata safety snapshots and validation.");

                metadata.AddCommand<MetadataSnapshotCommand>("snapshot")
                    .WithDescription("Create a metadata snapshot for the configured workspace.")
                    .WithExample("honua", "metadata", "snapshot", "--label", "pre-upgrade");

                metadata.AddCommand<MetadataSnapshotsListCommand>("snapshots")
                    .WithDescription("List available metadata snapshots.")
                    .WithExample("honua", "metadata", "snapshots");

                metadata.AddCommand<MetadataRestoreCommand>("restore")
                    .WithDescription("Restore metadata from a snapshot.")
                    .WithExample("honua", "metadata", "restore", "pre-upgrade");

                metadata.AddCommand<MetadataValidateCommand>("validate")
                    .WithDescription("Run metadata validation preflight checks.")
                    .WithExample("honua", "metadata", "validate", "--workspace", ".");

                metadata.AddCommand<MetadataSyncSchemaCommand>("sync-schema")
                    .WithDescription("Synchronize layer metadata with current database schemas.")
                    .WithExample("honua", "metadata", "sync-schema")
                    .WithExample("honua", "metadata", "sync-schema", "--dry-run")
                    .WithExample("honua", "metadata", "sync-schema", "--add-fields", "--yes");
            });

            config.AddBranch("sandbox", sandbox =>
            {
                sandbox.SetDescription("Manage local Honua sandbox environments.");

                sandbox.AddCommand<SandboxUpCommand>("up")
                    .WithDescription("Start the local Docker Compose sandbox stack.")
                    .WithExample("honua", "sandbox", "up");
            });

            config.AddBranch("config", configuration =>
            {
                configuration.SetDescription("Manage Honua CLI defaults such as host and bearer tokens.");

                configuration.AddCommand<ConfigInitCommand>("init")
                    .WithDescription("Initialize or update the stored Honua CLI configuration.")
                    .WithExample("honua", "config", "init", "--host", "http://localhost:5000");
            });

            config.AddBranch("auth", auth =>
            {
                auth.SetDescription("Manage Honua authentication bootstrap and user workflows.");

                auth.AddCommand<AuthBootstrapCommand>("bootstrap")
                    .WithDescription("Initialize the Honua authentication store and seed the first administrator (placeholder).")
                    .WithExample("honua", "auth", "bootstrap", "--mode", "Local");

                auth.AddCommand<AuthCreateUserCommand>("create-user")
                    .WithDescription("Create a local Honua user with specified roles.")
                    .WithExample("honua", "auth", "create-user", "--username", "publisher", "--role", "datapublisher", "--generate-password");
            });

            config.AddBranch("data", data =>
            {
                data.SetDescription("Manage Honua data ingestion workflows via the control plane API.");

                data.AddCommand<ImportWizardCommand>("import")
                    .WithDescription("Interactive wizard for importing geospatial data into Honua.")
                    .WithExample("honua", "data", "import")
                    .WithExample("honua", "data", "import", "--source", "./roads.geojson", "--service", "transport", "--layer", "roads");

                data.AddCommand<DataIngestionCommand>("ingest")
                    .WithDescription("Upload a dataset to the control plane and track ingestion progress.")
                    .WithExample("honua", "data", "ingest", "--service-id", "transport", "--layer-id", "roads", "./roads.gpkg");

                data.AddCommand<DataIngestionJobsCommand>("jobs")
                    .WithDescription("List recent data ingestion jobs on the control plane.")
                    .WithExample("honua", "data", "jobs");

                data.AddCommand<DataIngestionStatusCommand>("status")
                    .WithDescription("Show the status of a specific ingestion job.")
                    .WithExample("honua", "data", "status", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");

                data.AddCommand<DataIngestionCancelCommand>("cancel")
                    .WithDescription("Cancel an ingestion job in progress.")
                    .WithExample("honua", "data", "cancel", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");
            });

            config.AddBranch("migrate", migrate =>
            {
                migrate.SetDescription("Migrate Esri/ArcGIS services to Honua.");

                migrate.AddCommand<MigrateEsriServiceCommand>("arcgis")
                    .WithDescription("Migrate an Esri/ArcGIS REST service to Honua.")
                    .WithExample("honua", "migrate", "arcgis", "--source", "https://gis.example.com/arcgis/rest/services/Planning/Zoning/FeatureServer", "--target-service", "planning-zoning", "--target-folder", "services", "--target-datasource", "postgres-prod");

                migrate.AddCommand<MigrationJobsCommand>("jobs")
                    .WithDescription("List recent migration jobs on the control plane.")
                    .WithExample("honua", "migrate", "jobs");

                migrate.AddCommand<MigrationStatusCommand>("status")
                    .WithDescription("Show the status of a specific migration job.")
                    .WithExample("honua", "migrate", "status", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");

                migrate.AddCommand<MigrationCancelCommand>("cancel")
                    .WithDescription("Cancel a migration job in progress.")
                    .WithExample("honua", "migrate", "cancel", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");
            });

            config.AddBranch("raster-cache", raster =>
            {
                raster.SetDescription("Manage raster tile cache preseed and maintenance workflows.");

                raster.AddCommand<RasterCachePreseedCommand>("preseed")
                    .WithDescription("Enqueue a raster preseed job and stream progress.")
                    .WithExample("honua", "raster-cache", "preseed", "--dataset-id", "basemap", "--matrix-set", "WorldWebMercatorQuad");

                raster.AddCommand<RasterCachePurgeCommand>("purge")
                    .WithDescription("Purge cached tiles for one or more raster datasets.")
                    .WithExample("honua", "raster-cache", "purge", "--dataset-id", "basemap");

                raster.AddCommand<RasterCacheJobsCommand>("jobs")
                    .WithDescription("List recent raster preseed jobs.")
                    .WithExample("honua", "raster-cache", "jobs");

                raster.AddCommand<RasterCacheStatusCommand>("status")
                    .WithDescription("Show the status of a specific raster preseed job.")
                    .WithExample("honua", "raster-cache", "status", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");

                raster.AddCommand<RasterCacheCancelCommand>("cancel")
                    .WithDescription("Cancel a raster preseed job on the control plane.")
                    .WithExample("honua", "raster-cache", "cancel", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");
            });

            config.AddBranch("stac", stac =>
            {
                stac.SetDescription("Manage STAC catalog metadata derived from Honua workspaces.");

                stac.AddCommand<StacBackfillCommand>("backfill")
                    .WithDescription("Rebuild the STAC catalog from local metadata using RasterStacCatalogBuilder.")
                    .WithExample("honua", "stac", "backfill", "--workspace", ".");
            });

            config.AddBranch("secrets", secrets =>
            {
                secrets.SetDescription("Manage encrypted secrets for AI and database connections.");

                secrets.AddCommand<SecretsSetCommand>("set")
                    .WithDescription("Store a secret securely (AES-256 encrypted).")
                    .WithExample("honua", "secrets", "set", "POSTGRES_PASSWORD")
                    .WithExample("honua", "secrets", "set", "OPENAI_API_KEY", "--value", "sk-...");

                secrets.AddCommand<SecretsListCommand>("list")
                    .WithDescription("List all stored secrets (names only, not values).")
                    .WithExample("honua", "secrets", "list");
            });

            config.AddBranch("telemetry", telemetry =>
            {
                telemetry.SetDescription("Manage privacy-first telemetry settings.");

                telemetry.AddCommand<TelemetryEnableCommand>("enable")
                    .WithDescription("Enable opt-in telemetry with privacy consent.")
                    .WithExample("honua", "telemetry", "enable");

                telemetry.AddCommand<TelemetryStatusCommand>("status")
                    .WithDescription("Show telemetry status and recent activity.")
                    .WithExample("honua", "telemetry", "status");
            });

            config.AddBranch("gitops", gitops =>
            {
                gitops.SetDescription("Manage GitOps-based deployments and configuration.");

                gitops.AddCommand<GitOpsInitCommand>("init")
                    .WithDescription("Initialize GitOps configuration for a Honua environment.")
                    .WithExample("honua", "gitops", "init", "--repo", "https://github.com/org/honua-config", "--branch", "main")
                    .WithExample("honua", "gitops", "init", "--repo", "git@github.com:org/honua-config.git", "--environment", "production", "--auto-reconcile");

                gitops.AddCommand<GitOpsStatusCommand>("status")
                    .WithDescription("Display GitOps configuration and deployment status.")
                    .WithExample("honua", "gitops", "status")
                    .WithExample("honua", "gitops", "status", "--environment", "production")
                    .WithExample("honua", "gitops", "status", "--verbose");

                gitops.AddCommand<GitOpsSyncCommand>("sync")
                    .WithDescription("Manually trigger GitOps synchronization.")
                    .WithExample("honua", "gitops", "sync");

                gitops.AddCommand<GitOpsConfigCommand>("config")
                    .WithDescription("View or update GitOps configuration.")
                    .WithExample("honua", "gitops", "config");

                gitops.AddCommand<GitOpsDeploymentsCommand>("deployments")
                    .WithDescription("List recent GitOps deployments.")
                    .WithExample("honua", "gitops", "deployments")
                    .WithExample("honua", "gitops", "deployments", "--environment", "production")
                    .WithExample("honua", "gitops", "deployments", "--limit", "20");

                gitops.AddCommand<GitOpsDeploymentCommand>("deployment")
                    .WithDescription("Show detailed deployment information.")
                    .WithExample("honua", "gitops", "deployment", "production-20250123-143022")
                    .WithExample("honua", "gitops", "deployment", "production-20250123-143022", "--verbose");

                gitops.AddCommand<GitOpsHistoryCommand>("history")
                    .WithDescription("Show deployment history for an environment.")
                    .WithExample("honua", "gitops", "history", "production")
                    .WithExample("honua", "gitops", "history", "production", "--limit", "20")
                    .WithExample("honua", "gitops", "history", "production", "--timeline", "--verbose");

                gitops.AddCommand<GitOpsApproveCommand>("approve")
                    .WithDescription("Approve a deployment awaiting approval.")
                    .WithExample("honua", "gitops", "approve", "production-20250123-143022")
                    .WithExample("honua", "gitops", "approve", "production-20250123-143022", "--approver", "john.doe");

                gitops.AddCommand<GitOpsRejectCommand>("reject")
                    .WithDescription("Reject a deployment awaiting approval.")
                    .WithExample("honua", "gitops", "reject", "production-20250123-143022", "--reason", "Missing migration script")
                    .WithExample("honua", "gitops", "reject", "production-20250123-143022");

                gitops.AddCommand<GitOpsRollbackCommand>("rollback")
                    .WithDescription("Rollback environment to last successful deployment.")
                    .WithExample("honua", "gitops", "rollback", "production")
                    .WithExample("honua", "gitops", "rollback", "production", "--yes");
            });

            config.AddBranch("process", process =>
            {
                process.SetDescription("Manage Semantic Kernel Process Framework workflows.");

                // Process invocation commands
                process.AddCommand<ProcessDeployCommand>("deploy")
                    .WithDescription("Deploy Honua infrastructure to cloud provider.")
                    .WithExample("honua", "process", "deploy", "--name", "my-deployment", "--provider", "AWS", "--region", "us-west-2");

                process.AddCommand<ProcessUpgradeCommand>("upgrade")
                    .WithDescription("Upgrade Honua deployment using blue-green strategy.")
                    .WithExample("honua", "process", "upgrade", "--deployment-name", "my-deployment", "--target-version", "2.0.0");

                process.AddCommand<ProcessMetadataCommand>("metadata")
                    .WithDescription("Extract and publish geospatial dataset metadata.")
                    .WithExample("honua", "process", "metadata", "--dataset-path", "./data/raster.tif", "--publish-stac");

                process.AddCommand<ProcessGitOpsCommand>("gitops")
                    .WithDescription("Synchronize GitOps configuration from Git repository.")
                    .WithExample("honua", "process", "gitops", "--repo-url", "https://github.com/org/config", "--config-path", "config/", "--branch", "main");

                process.AddCommand<ProcessBenchmarkCommand>("benchmark")
                    .WithDescription("Run performance benchmark on Honua deployment.")
                    .WithExample("honua", "process", "benchmark", "--target-endpoint", "http://localhost:5000", "--type", "Load", "--concurrency", "10");

                // Process control commands
                process.AddCommand<ProcessStatusCommand>("status")
                    .WithDescription("Check status of a running process.")
                    .WithExample("honua", "process", "status", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");

                process.AddCommand<ProcessPauseCommand>("pause")
                    .WithDescription("Pause a running process.")
                    .WithExample("honua", "process", "pause", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");

                process.AddCommand<ProcessResumeCommand>("resume")
                    .WithDescription("Resume a paused process.")
                    .WithExample("honua", "process", "resume", "8f8a9b3d-0d8e-4f4a-9f30-1b0c4c6d9adb");

                process.AddCommand<ProcessListCommand>("list")
                    .WithDescription("List all running processes.")
                    .WithExample("honua", "process", "list")
                    .WithExample("honua", "process", "list", "--all")
                    .WithExample("honua", "process", "list", "--type", "deployment");
            });

        });

        try
        {
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return -1;
        }
    }

    private static void ConfigureAiServices(IServiceCollection services, IConfiguration configuration)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var honuaDir = Path.Combine(userHome, ".honua");

        services.Configure<VectorSearchOptions>(configuration.GetSection(VectorSearchOptions.SectionName));

        // Configure LLM Provider Options
        services.AddOptions<LlmProviderOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                // First, check user secrets (preferred for local dev)
                var openAiKey = config["OpenAI:ApiKey"];
                var anthropicKey = config["Anthropic:ApiKey"];
                var azureOpenAiKey = config["Azure:OpenAI:ApiKey"];
                var azureOpenAiEndpoint = config["Azure:OpenAI:Endpoint"];

                // Fall back to environment variables if secrets not found
                if (openAiKey.IsNullOrWhiteSpace())
                {
                    openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                }
                if (anthropicKey.IsNullOrWhiteSpace())
                {
                    anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                }
                if (azureOpenAiKey.IsNullOrWhiteSpace())
                {
                    azureOpenAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                }
                if (azureOpenAiEndpoint.IsNullOrWhiteSpace())
                {
                    azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                }

                // Prefer Anthropic (Claude) > Azure OpenAI > OpenAI > Mock
                if (anthropicKey.HasValue())
                {
                    options.Provider = "Anthropic";
                    options.Anthropic.ApiKey = anthropicKey;
                }
                else if (azureOpenAiKey.HasValue() && azureOpenAiEndpoint.HasValue())
                {
                    options.Provider = "AzureOpenAI";
                    options.Azure.ApiKey = azureOpenAiKey;
                    options.Azure.EndpointUrl = azureOpenAiEndpoint;
                }
                else if (openAiKey.HasValue())
                {
                    options.Provider = "OpenAI";
                    options.OpenAI.ApiKey = openAiKey;
                }
                else
                {
                    // Default to Mock if no keys available
                    options.Provider = "Mock";
                }
            });

        // Register LLM Provider Factory and Provider
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var factory = sp.GetRequiredService<ILlmProviderFactory>();
            var primary = factory.CreatePrimary();
            var fallback = factory.CreateFallback();
            return new ResilientLlmProvider(primary, fallback);
        });

        // Register Embedding Provider
        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value;

            // Create embedding provider based on the primary LLM provider
            return options.Provider.ToLowerInvariant() switch
            {
                "openai" => new OpenAIEmbeddingProvider(options),
                "azure" or "azureopenai" => new AzureOpenAIEmbeddingProvider(options),
                "anthropic" or "claude" => new AnthropicEmbeddingProvider(options),
                "mock" => new MockEmbeddingProvider(),
                _ => new MockEmbeddingProvider() // Fallback to mock if provider not recognized
            };
        });

        // Configure Secrets Manager
        services.AddSingleton<SecretsManagerOptions>(sp =>
        {
            return new SecretsManagerOptions
            {
                Backend = SecretsBackend.EncryptedFile,
                FilePath = Path.Combine(honuaDir, "secrets.enc"),
                RequireUserApproval = true,
                DefaultTokenDuration = TimeSpan.FromMinutes(10),
                MaxTokenDuration = TimeSpan.FromHours(1)
            };
        });

        services.AddSingleton<ISecretsManager, EncryptedFileSecretsManager>();

        // Configure Telemetry Service
        services.AddOptions<TelemetryOptions>()
            .Configure(options =>
            {
                var configPath = Path.Combine(honuaDir, "telemetry-config.json");

                if (File.Exists(configPath))
                {
                    try
                    {
                        var json = File.ReadAllText(configPath);
                        var config = System.Text.Json.JsonSerializer.Deserialize<TelemetryOptions>(json);
                        if (config != null)
                        {
                            options.Enabled = config.Enabled;
                            options.ConsentTimestamp = config.ConsentTimestamp;
                            options.UserId = config.UserId ?? Guid.NewGuid().ToString("N");
                            options.Backend = config.Backend;
                            options.LocalFilePath = config.LocalFilePath ?? Path.Combine(honuaDir, "telemetry");
                        }
                    }
                    catch
                    {
                        // If config is corrupted, use defaults
                        SetTelemetryDefaults(options, honuaDir);
                    }
                }
                else
                {
                    SetTelemetryDefaults(options, honuaDir);
                }
            });

        services.AddSingleton<ITelemetryService, LocalFileTelemetryService>();

        // Configure Semantic Kernel with plugins and AI services
        services.AddSingleton(sp =>
        {
            // Build kernel without ChatCompletion for now
            // ChatCompletion will be added when Process Framework is fully configured
            var kernel = new Microsoft.SemanticKernel.Kernel();

            // Default execution context (will be replaced per-request with actual settings)
            var executionContext = new Honua.Cli.AI.Services.Execution.PluginExecutionContext(
                workspacePath: Directory.GetCurrentDirectory(),
                requireApproval: true,
                dryRun: false);

            // Register all AI assistant plugins
            var plugins = new (string, object)[]
            {
                ("SelfDocumentation", new Honua.Cli.AI.Services.Plugins.SelfDocumentationPlugin()),
                ("DocumentationSearch", new Honua.Cli.AI.Services.Plugins.DocumentationSearchPlugin()),
                ("Workspace", new Honua.Cli.AI.Services.Plugins.WorkspacePlugin()),
                ("SetupWizard", new Honua.Cli.AI.Services.Plugins.SetupWizardPlugin()),
                ("DataIngestion", new Honua.Cli.AI.Services.Plugins.DataIngestionPlugin()),
                ("Migration", new Honua.Cli.AI.Services.Plugins.MigrationPlugin()),
                ("Metadata", new Honua.Cli.AI.Services.Plugins.MetadataPlugin()),
                ("Performance", new Honua.Cli.AI.Services.Plugins.PerformancePlugin()),
                ("OptimizationEnhancements", new Honua.Cli.AI.Services.Plugins.OptimizationEnhancementsPlugin()),
                ("SpatialAnalysis", new Honua.Cli.AI.Services.Plugins.SpatialAnalysisPlugin()),
                ("Diagnostics", new Honua.Cli.AI.Services.Plugins.DiagnosticsPlugin()),
                ("Security", new Honua.Cli.AI.Services.Plugins.SecurityPlugin()),
                ("Testing", new Honua.Cli.AI.Services.Plugins.TestingPlugin()),
                ("Documentation", new Honua.Cli.AI.Services.Plugins.DocumentationPlugin()),
                ("Monitoring", new Honua.Cli.AI.Services.Plugins.MonitoringPlugin()),
                ("Compliance", new Honua.Cli.AI.Services.Plugins.CompliancePlugin()),
                ("Integration", new Honua.Cli.AI.Services.Plugins.IntegrationPlugin()),
                ("CloudDeployment", new Honua.Cli.AI.Services.Plugins.CloudDeploymentPlugin()),
                // Execution plugins
                ("FileSystem", new Honua.Cli.AI.Services.Execution.FileSystemExecutionPlugin(executionContext)),
                ("Docker", new Honua.Cli.AI.Services.Execution.DockerExecutionPlugin(executionContext)),
                ("Database", new Honua.Cli.AI.Services.Execution.DatabaseExecutionPlugin(executionContext)),
                ("Terraform", new Honua.Cli.AI.Services.Execution.TerraformExecutionPlugin(executionContext)),
                ("Validation", new Honua.Cli.AI.Services.Execution.ValidationPlugin(executionContext, sp.GetRequiredService<System.Net.Http.IHttpClientFactory>()))
            };

            foreach (var (name, instance) in plugins)
            {
                var plugin = Microsoft.SemanticKernel.KernelPluginFactory.CreateFromObject(instance, name);
                kernel.Plugins.Add(plugin);
            }

            return kernel;
        });

        // Register IChatCompletionService for Agent Framework
        // Note: Anthropic is supported via ILlmProvider; for SK Agent Framework we use OpenAI/Azure
        services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            // Add appropriate connector based on provider
            // Note: If Anthropic is the primary provider, we'll use OpenAI as fallback for SK services
            // The main coordination logic uses ILlmProvider which supports Anthropic natively
            return options.Provider.ToLowerInvariant() switch
            {
                "openai" => new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatCompletionService(
                    modelId: options.OpenAI.DefaultModel,
                    apiKey: options.OpenAI.ApiKey,
                    httpClient: httpClientFactory.CreateClient("SemanticKernel-OpenAI")), // LLM API calls

                "azure" or "azureopenai" => new Microsoft.SemanticKernel.Connectors.AzureOpenAI.AzureOpenAIChatCompletionService(
                    deploymentName: options.Azure.DeploymentName ?? options.Azure.DefaultModel,
                    endpoint: options.Azure.EndpointUrl,
                    apiKey: options.Azure.ApiKey,
                    httpClient: httpClientFactory.CreateClient("SemanticKernel-Azure")), // LLM API calls

                // For Anthropic, use OpenAI as fallback if available
                "anthropic" or "claude" => options.OpenAI.ApiKey.IsNullOrWhiteSpace()
                    ? throw new InvalidOperationException(
                        "When using Anthropic as primary provider, you must also configure OpenAI API key " +
                        "for Semantic Kernel Agent Framework services. Set OpenAI:ApiKey in user secrets.")
                    : new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatCompletionService(
                        modelId: options.OpenAI.DefaultModel,
                        apiKey: options.OpenAI.ApiKey,
                        httpClient: httpClientFactory.CreateClient("SemanticKernel-OpenAI")), // LLM API calls

                _ => throw new InvalidOperationException(
                    $"No valid LLM provider configured. Please set API keys in user secrets or environment variables. " +
                    $"Current provider: {options.Provider}")
            };
        });

        // Register Agent Guards, Factory, and Coordinator
        services.AddSingleton<Honua.Cli.AI.Services.Guards.IInputGuard, Honua.Cli.AI.Services.Guards.LlmInputGuard>();
        services.AddSingleton<Honua.Cli.AI.Services.Guards.IOutputGuard, Honua.Cli.AI.Services.Guards.LlmOutputGuard>();
        services.AddSingleton<Honua.Cli.AI.Configuration.AgentActivitySource>();
        services.AddSingleton<Honua.Cli.AI.Services.Agents.HonuaAgentFactory>();
        services.AddSingleton<Honua.Cli.AI.Services.Processes.ParameterExtractionService>();
        services.AddSingleton<Honua.Cli.AI.Services.Processes.IProcessStateStore, Honua.Cli.AI.Services.Processes.InMemoryProcessStateStore>();
        services.AddSingleton<Honua.Cli.AI.Services.Agents.HonuaMagenticCoordinator>();

        // Register optional telemetry and history services (may be null)
        services.AddSingleton<Honua.Cli.AI.Services.VectorSearch.IPatternUsageTelemetry?>(sp => null);
        services.AddSingleton<Honua.Cli.AI.Services.Agents.IAgentHistoryStore?>(sp => null);

        services.AddSingleton<IVectorSearchProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VectorSearchOptions>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var provider = options.Value.Provider ?? VectorSearchProviders.InMemory;

            if (string.Equals(provider, VectorSearchProviders.AzureAiSearch, StringComparison.OrdinalIgnoreCase))
            {
                return new AzureVectorSearchProvider(
                    options,
                    loggerFactory.CreateLogger<AzureVectorSearchProvider>());
            }

            return new InMemoryVectorSearchProvider();
        });

        services.AddSingleton<IDeploymentPatternKnowledgeStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VectorSearchOptions>>();
            var embeddingProvider = sp.GetRequiredService<IEmbeddingProvider>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            if (string.Equals(options.Value.Provider, VectorSearchProviders.AzureAiSearch, StringComparison.OrdinalIgnoreCase))
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                return new AzureAISearchKnowledgeStore(
                    configuration,
                    embeddingProvider,
                    loggerFactory.CreateLogger<AzureAISearchKnowledgeStore>());
            }

            var vectorProvider = sp.GetRequiredService<IVectorSearchProvider>();
            return new VectorDeploymentPatternKnowledgeStore(
                vectorProvider,
                embeddingProvider,
                options,
                loggerFactory.CreateLogger<VectorDeploymentPatternKnowledgeStore>());
        });

        // Agent capability metadata
        services.Configure<AgentCapabilityOptions>(configuration.GetSection("AgentCapabilities"));
        services.AddSingleton<AgentCapabilityRegistry>();
        services.AddSingleton<Honua.Cli.AI.Services.Agents.IntelligentAgentSelector>(sp =>
        {
            var llmProvider = sp.GetRequiredService<ILlmProvider>();
            var capabilities = sp.GetRequiredService<AgentCapabilityRegistry>();
            var logger = sp.GetRequiredService<ILogger<Honua.Cli.AI.Services.Agents.IntelligentAgentSelector>>();

            return new Honua.Cli.AI.Services.Agents.IntelligentAgentSelector(llmProvider, capabilities, logger);
        });
        services.AddSingleton<IAgentCritic, PlanSafetyCritic>();

        // Register AI-powered consultant planner (falls back to Bootstrap if no API keys)
        services.AddSingleton<Honua.Cli.Services.Consultant.IConsultantPlanner>(sp =>
        {
            var llmProvider = sp.GetRequiredService<ILlmProvider>();
            var clock = sp.GetRequiredService<Honua.Cli.Services.ISystemClock>();
            var sk = sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>();
            var patternStore = sp.GetRequiredService<IDeploymentPatternKnowledgeStore>();
            var logger = sp.GetRequiredService<ILogger<Honua.Cli.Services.Consultant.SemanticConsultantPlanner>>();

            // Check if we have a real LLM provider (not Mock)
            if (llmProvider is Honua.Cli.AI.Services.AI.Providers.MockLlmProvider)
            {
                // Fall back to bootstrap planner if no API keys configured
                return new Honua.Cli.Services.Consultant.BootstrapConsultantPlanner(clock);
            }

            return new Honua.Cli.Services.Consultant.SemanticConsultantPlanner(llmProvider, clock, sk, patternStore, logger);
        });

        // Register AI Agent Coordinator for multi-agent orchestration
        services.AddSingleton<Honua.Cli.AI.Services.Agents.IAgentCoordinator>(sp =>
        {
            var llmProvider = sp.GetRequiredService<ILlmProvider>();
            var kernel = sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>();
            var agentSelector = sp.GetRequiredService<Honua.Cli.AI.Services.Agents.IntelligentAgentSelector>();
            var logger = sp.GetRequiredService<ILogger<Honua.Cli.AI.Services.Agents.SemanticAgentCoordinator>>();

            // GetService returns nullable types - these are optional dependencies
            var telemetry = sp.GetService<Honua.Cli.AI.Services.VectorSearch.IPatternUsageTelemetry?>();
            var historyStore = sp.GetService<Honua.Cli.AI.Services.Agents.IAgentHistoryStore?>();

            return new Honua.Cli.AI.Services.Agents.SemanticAgentCoordinator(
                llmProvider,
                kernel,
                agentSelector,
                logger,
                telemetry,
                historyStore);
        });
    }

    private static void SetTelemetryDefaults(TelemetryOptions options, string honuaDir)
    {
        options.Enabled = false; // Opt-in only
        options.UserId = Guid.NewGuid().ToString("N");
        options.Backend = TelemetryBackend.LocalFile;
        options.LocalFilePath = Path.Combine(honuaDir, "telemetry");
    }
}
