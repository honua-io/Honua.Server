// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Deployment;
using Honua.Server.Core.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Extension methods for registering GitOps services in the dependency injection container.
/// </summary>
public static class GitOpsServiceCollectionExtensions
{
    /// <summary>
    /// Adds GitOps services to the service collection for declarative configuration management.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns">The service collection for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing or invalid.</exception>
    public static IServiceCollection AddGitOps(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var gitOpsSection = configuration.GetSection("GitOps");

        // Validate required configuration
        var repositoryPath = gitOpsSection.GetValue<string>("RepositoryPath");
        if (repositoryPath.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "GitOps:RepositoryPath configuration is required when GitOps is enabled. " +
                "Please provide a valid path to the Git repository.");
        }

        var stateDirectory = gitOpsSection.GetValue<string>("StateDirectory");
        if (stateDirectory.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "GitOps:StateDirectory configuration is required when GitOps is enabled. " +
                "Please provide a valid path for storing deployment state.");
        }

        // Validate repository path exists and is a valid Git repository
        if (!Directory.Exists(repositoryPath))
        {
            throw new InvalidOperationException(
                $"GitOps repository path does not exist: {repositoryPath}. " +
                "Please ensure the Git repository has been cloned to this location.");
        }

        if (!LibGit2Sharp.Repository.IsValid(repositoryPath))
        {
            throw new InvalidOperationException(
                $"GitOps repository path is not a valid Git repository: {repositoryPath}. " +
                "Please ensure the path points to a properly initialized Git repository.");
        }

        // Get optional credentials
        var username = gitOpsSection.GetValue<string>("Credentials:Username");
        var password = gitOpsSection.GetValue<string>("Credentials:Password");

        // Register IGitRepository with factory pattern to inject configuration and credentials
        services.AddSingleton<IGitRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LibGit2SharpRepository>>();

            if (!username.IsNullOrEmpty() && !password.IsNullOrEmpty())
            {
                logger.LogInformation(
                    "Initializing Git repository at '{RepositoryPath}' with authentication",
                    repositoryPath);
            }
            else
            {
                logger.LogInformation(
                    "Initializing Git repository at '{RepositoryPath}' without authentication",
                    repositoryPath);
            }

            return new LibGit2SharpRepository(repositoryPath, username, password);
        });

        // Register IDeploymentStateStore with state directory from configuration
        services.AddSingleton<IDeploymentStateStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<FileStateStore>>();
            logger.LogInformation("Initializing deployment state store at '{StateDirectory}'", stateDirectory);
            return new FileStateStore(stateDirectory);
        });

        // Register notification services
        var notificationsSection = gitOpsSection.GetSection("Notifications");
        if (notificationsSection.Exists())
        {
            services.Configure<NotificationOptions>(notificationsSection);

            var notificationsEnabled = notificationsSection.GetValue<bool>("Enabled", false);
            if (notificationsEnabled)
            {
                var slackEnabled = notificationsSection.GetValue<bool>("Slack:Enabled", false);
                var emailEnabled = notificationsSection.GetValue<bool>("Email:Enabled", false);

                if (slackEnabled || emailEnabled)
                {
                    // Register HttpClient for Slack notifications
                    if (slackEnabled)
                    {
                        services.AddHttpClient<SlackNotificationService>();
                        services.AddSingleton<INotificationService, SlackNotificationService>(sp =>
                        {
                            var options = Microsoft.Extensions.Options.Options.Create(
                                notificationsSection.Get<NotificationOptions>() ?? new NotificationOptions());
                            var logger = sp.GetRequiredService<ILogger<SlackNotificationService>>();
                            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                            var httpClient = httpClientFactory.CreateClient(nameof(SlackNotificationService));
                            return new SlackNotificationService(options, logger, httpClient);
                        });
                    }

                    // Register Email notification service
                    if (emailEnabled)
                    {
                        services.AddSingleton<INotificationService, EmailNotificationService>(sp =>
                        {
                            var options = Microsoft.Extensions.Options.Options.Create(
                                notificationsSection.Get<NotificationOptions>() ?? new NotificationOptions());
                            var logger = sp.GetRequiredService<ILogger<EmailNotificationService>>();
                            return new EmailNotificationService(options, logger);
                        });
                    }

                    // If both enabled, use composite service; otherwise use the single enabled service
                    if (slackEnabled && emailEnabled)
                    {
                        services.AddSingleton<INotificationService>(sp =>
                        {
                            var notificationServices = sp.GetServices<INotificationService>();
                            var logger = sp.GetRequiredService<ILogger<CompositeNotificationService>>();
                            return new CompositeNotificationService(notificationServices, logger);
                        });
                    }
                }
            }
        }

        // Register IReconciler with all optional dependencies
        services.AddSingleton<IReconciler>(sp =>
        {
            var repository = sp.GetRequiredService<IGitRepository>();
            var logger = sp.GetRequiredService<ILogger<HonuaReconciler>>();
            var metadataRegistry = sp.GetService<Honua.Server.Core.Metadata.IMetadataRegistry>();
            var stacCatalogStore = sp.GetService<Honua.Server.Core.Stac.IStacCatalogStore>();
            var databaseMigrationService = sp.GetService<IDatabaseMigrationService>();
            var certificateRenewalService = sp.GetService<ICertificateRenewalService>();
            var deploymentStateStore = sp.GetRequiredService<IDeploymentStateStore>();
            var approvalService = sp.GetService<IApprovalService>();
            var notificationService = sp.GetService<INotificationService>();

            // Determine dry run mode (can be overridden in configuration)
            var dryRun = gitOpsSection.GetValue<bool>("DryRun", false);
            if (dryRun)
            {
                logger.LogWarning("GitOps reconciler is running in DRY RUN mode - no actual changes will be applied");
            }

            return new HonuaReconciler(
                repository,
                logger,
                repositoryPath, // Use repository path as git working directory
                metadataRegistry,
                stacCatalogStore,
                databaseMigrationService,
                certificateRenewalService,
                deploymentStateStore,
                approvalService,
                notificationService,
                dryRun);
        });

        // Bind GitWatcherOptions from configuration
        services.Configure<GitWatcherOptions>(gitOpsSection.GetSection("Watcher"));

        // Register GitWatcher as hosted service with all dependencies
        services.AddHostedService(sp =>
        {
            var repository = sp.GetRequiredService<IGitRepository>();
            var reconciler = sp.GetRequiredService<IReconciler>();
            var options = Microsoft.Extensions.Options.Options.Create(
                gitOpsSection.GetSection("Watcher").Get<GitWatcherOptions>() ?? new GitWatcherOptions());
            var logger = sp.GetRequiredService<ILogger<GitWatcher>>();

            return new GitWatcher(repository, reconciler, options.Value, logger);
        });

        return services;
    }
}
