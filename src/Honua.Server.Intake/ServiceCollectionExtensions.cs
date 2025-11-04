// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Intake.BackgroundServices;
using Honua.Server.Intake.Configuration;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.Server.Intake;

/// <summary>
/// Extension methods for configuring container registry intake services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds container registry intake services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for registry provisioning options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContainerRegistryIntake(
        this IServiceCollection services,
        Action<RegistryProvisioningOptions>? configure = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }

        // Register core services
        services.TryAddSingleton<IRegistryProvisioner, RegistryProvisioner>();
        services.TryAddSingleton<IRegistryCacheChecker, RegistryCacheChecker>();
        services.TryAddSingleton<IRegistryAccessManager, RegistryAccessManager>();
        services.TryAddSingleton<IBuildDeliveryService, BuildDeliveryService>();

        // Register HTTP client for registry operations
        services.AddHttpClient("RegistryClient")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
            });

        return services;
    }

    /// <summary>
    /// Adds container registry intake services with build delivery configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureProvisioning">Configuration action for registry provisioning options.</param>
    /// <param name="configureBuildDelivery">Configuration action for build delivery options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContainerRegistryIntakeWithBuildDelivery(
        this IServiceCollection services,
        Action<RegistryProvisioningOptions>? configureProvisioning = null,
        Action<BuildDeliveryOptions>? configureBuildDelivery = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Add base intake services
        services.AddContainerRegistryIntake(configureProvisioning);

        // Configure build delivery options
        if (configureBuildDelivery != null)
        {
            services.Configure(configureBuildDelivery);
        }

        return services;
    }

    /// <summary>
    /// Adds build queue processing services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration containing BuildQueue settings.</param>
    /// <param name="connectionString">Optional database connection string. If not provided, uses configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBuildQueueServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionString = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Configure options from configuration
        services.Configure<BuildQueueOptions>(configuration.GetSection(BuildQueueOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection("Email"));

        // Register build queue services
        services.TryAddSingleton<IBuildQueueManager>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BuildQueueOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BuildQueueManager>>();
            return new BuildQueueManager(options, logger, connectionString);
        });

        services.TryAddSingleton<IBuildNotificationService, BuildNotificationService>();

        // Register background service
        services.AddHostedService<BuildQueueProcessor>();

        return services;
    }

    /// <summary>
    /// Adds build queue processing services with explicit options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureBuildQueue">Action to configure build queue options.</param>
    /// <param name="configureEmail">Action to configure email options.</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBuildQueueServices(
        this IServiceCollection services,
        Action<BuildQueueOptions> configureBuildQueue,
        Action<EmailOptions> configureEmail,
        string connectionString)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configureBuildQueue == null)
        {
            throw new ArgumentNullException(nameof(configureBuildQueue));
        }

        if (configureEmail == null)
        {
            throw new ArgumentNullException(nameof(configureEmail));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required", nameof(connectionString));
        }

        // Configure options
        services.Configure(configureBuildQueue);
        services.Configure(configureEmail);

        // Register services
        services.TryAddSingleton<IBuildQueueManager>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BuildQueueOptions>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BuildQueueManager>>();
            return new BuildQueueManager(options, logger, connectionString);
        });

        services.TryAddSingleton<IBuildNotificationService, BuildNotificationService>();

        // Register background service
        services.AddHostedService<BuildQueueProcessor>();

        return services;
    }

    /// <summary>
    /// Adds AI-powered intake agent services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration containing IntakeAgent settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAIIntakeAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Configure options from configuration
        services.Configure<IntakeAgentOptions>(configuration.GetSection("IntakeAgent"));

        // Register core AI intake services
        services.TryAddSingleton<IConversationStore, ConversationStore>();
        services.TryAddSingleton<IIntakeAgent, IntakeAgent>();
        services.TryAddSingleton<IManifestGenerator, ManifestGenerator>();

        // Register HTTP client for AI API calls
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Adds AI-powered intake agent services with explicit options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure intake agent options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAIIntakeAgent(
        this IServiceCollection services,
        Action<IntakeAgentOptions> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        // Configure options
        services.Configure(configure);

        // Register core AI intake services
        services.TryAddSingleton<IConversationStore, ConversationStore>();
        services.TryAddSingleton<IIntakeAgent, IntakeAgent>();
        services.TryAddSingleton<IManifestGenerator, ManifestGenerator>();

        // Register HTTP client for AI API calls
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Adds the complete intake system including container registry, build queue, and AI agent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration containing all intake settings.</param>
    /// <param name="connectionString">Optional database connection string. If not provided, uses configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompleteIntakeSystem(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionString = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Add container registry services
        services.AddContainerRegistryIntake();

        // Add build queue services
        services.AddBuildQueueServices(configuration, connectionString);

        // Add AI intake agent
        services.AddAIIntakeAgent(configuration);

        // Add controllers for API endpoints
        services.AddControllers()
            .AddApplicationPart(typeof(Controllers.IntakeController).Assembly);

        return services;
    }
}

