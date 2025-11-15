// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// Service collection extensions for background jobs infrastructure
/// </summary>
public static class BackgroundJobsServiceCollectionExtensions
{
    /// <summary>
    /// Adds background jobs infrastructure to the service collection.
    /// Configures queue provider and idempotency store based on options.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<BackgroundJobsOptions>(
            configuration.GetSection(BackgroundJobsOptions.SectionName));

        // Validate configuration on startup
        services.AddSingleton<IValidateOptions<BackgroundJobsOptions>, BackgroundJobsOptionsValidator>();

        // Register metrics
        services.AddSingleton<BackgroundJobMetrics>();

        // Register queue provider based on mode
        services.AddSingleton<IBackgroundJobQueue>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<IBackgroundJobQueue>>();

            return options.Mode switch
            {
                BackgroundJobMode.Polling => CreatePostgresQueue(sp, options),
                BackgroundJobMode.MessageQueue => CreateMessageQueue(sp, options),
                _ => throw new InvalidOperationException($"Unsupported background job mode: {options.Mode}")
            };
        });

        // Register idempotency store (Redis-based)
        services.AddSingleton<IIdempotencyStore>(sp =>
        {
            var redis = sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisIdempotencyStore>>();
            return new RedisIdempotencyStore(redis, logger);
        });

        return services;
    }

    /// <summary>
    /// Creates PostgreSQL-based job queue
    /// </summary>
    private static IBackgroundJobQueue CreatePostgresQueue(
        IServiceProvider serviceProvider,
        BackgroundJobsOptions options)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string not found");

        var logger = serviceProvider.GetRequiredService<ILogger<PostgresBackgroundJobQueue>>();
        var optionsWrapper = serviceProvider.GetRequiredService<IOptions<BackgroundJobsOptions>>();

        return new PostgresBackgroundJobQueue(connectionString, logger, optionsWrapper);
    }

    /// <summary>
    /// Creates message queue based on provider
    /// </summary>
    private static IBackgroundJobQueue CreateMessageQueue(
        IServiceProvider serviceProvider,
        BackgroundJobsOptions options)
    {
        return options.Provider switch
        {
            MessageQueueProvider.AwsSqs => CreateAwsSqsQueue(serviceProvider, options),
            MessageQueueProvider.AzureServiceBus => throw new NotImplementedException("Azure Service Bus not yet implemented"),
            MessageQueueProvider.RabbitMq => throw new NotImplementedException("RabbitMQ not yet implemented"),
            _ => throw new InvalidOperationException($"Unsupported message queue provider: {options.Provider}")
        };
    }

    /// <summary>
    /// Creates AWS SQS queue
    /// </summary>
    private static IBackgroundJobQueue CreateAwsSqsQueue(
        IServiceProvider serviceProvider,
        BackgroundJobsOptions options)
    {
        if (options.AwsSqs == null)
            throw new InvalidOperationException("AwsSqs configuration is required");

        // Create SQS client
        var sqsConfig = new AmazonSQSConfig();

        if (!string.IsNullOrWhiteSpace(options.AwsSqs.Region))
        {
            sqsConfig.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.AwsSqs.Region);
        }

        IAmazonSQS sqs;
        if (!string.IsNullOrWhiteSpace(options.AwsSqs.AccessKeyId) &&
            !string.IsNullOrWhiteSpace(options.AwsSqs.SecretAccessKey))
        {
            // Use explicit credentials
            sqs = new AmazonSQSClient(
                options.AwsSqs.AccessKeyId,
                options.AwsSqs.SecretAccessKey,
                sqsConfig);
        }
        else
        {
            // Use IAM role / default credential chain
            sqs = new AmazonSQSClient(sqsConfig);
        }

        var logger = serviceProvider.GetRequiredService<ILogger<AwsSqsBackgroundJobQueue>>();
        var optionsWrapper = serviceProvider.GetRequiredService<IOptions<BackgroundJobsOptions>>();

        return new AwsSqsBackgroundJobQueue(sqs, logger, optionsWrapper);
    }
}

/// <summary>
/// Validates BackgroundJobsOptions at startup
/// </summary>
internal sealed class BackgroundJobsOptionsValidator : IValidateOptions<BackgroundJobsOptions>
{
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
