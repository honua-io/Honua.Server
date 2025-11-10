// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimiting;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Provides bulkhead policies for database and external API operations.
/// Bulkheads prevent resource exhaustion by limiting concurrent operations.
/// </summary>
public class BulkheadPolicyProvider
{
    private readonly BulkheadOptions _options;
    private readonly ILogger<BulkheadPolicyProvider> _logger;

    /// <summary>
    /// Gets the bulkhead policy for database operations.
    /// Limits concurrent database connections to prevent connection pool exhaustion.
    /// </summary>
    public ResiliencePipeline DatabaseBulkhead { get; }

    /// <summary>
    /// Gets the bulkhead policy for external API calls.
    /// Limits concurrent external requests to prevent overwhelming external services.
    /// </summary>
    public ResiliencePipeline ExternalApiBulkhead { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkheadPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">Bulkhead configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public BulkheadPolicyProvider(
        IOptions<BulkheadOptions> options,
        ILogger<BulkheadPolicyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        // Validate configuration on startup
        _options.Validate();

        // Create database bulkhead policy
        DatabaseBulkhead = CreateDatabaseBulkhead();

        // Create external API bulkhead policy
        ExternalApiBulkhead = CreateExternalApiBulkhead();

        _logger.LogInformation(
            "Bulkhead policies initialized. Database: {DbMax}/{DbQueue}, ExternalAPI: {ApiMax}/{ApiQueue}",
            _options.DatabaseMaxParallelization,
            _options.DatabaseMaxQueuedActions,
            _options.ExternalApiMaxParallelization,
            _options.ExternalApiMaxQueuedActions);
    }

    /// <summary>
    /// Creates a bulkhead policy for database operations.
    /// </summary>
    private ResiliencePipeline CreateDatabaseBulkhead()
    {
        if (!_options.DatabaseEnabled)
        {
            _logger.LogInformation("Database bulkhead is disabled");
            return ResiliencePipeline.Empty;
        }

        return new ResiliencePipelineBuilder()
            .AddConcurrencyLimiter(permitLimit: _options.DatabaseMaxParallelization, queueLimit: _options.DatabaseMaxQueuedActions)
            .Build();
    }

    /// <summary>
    /// Creates a bulkhead policy for external API operations.
    /// </summary>
    private ResiliencePipeline CreateExternalApiBulkhead()
    {
        if (!_options.ExternalApiEnabled)
        {
            _logger.LogInformation("External API bulkhead is disabled");
            return ResiliencePipeline.Empty;
        }

        return new ResiliencePipelineBuilder()
            .AddConcurrencyLimiter(permitLimit: _options.ExternalApiMaxParallelization, queueLimit: _options.ExternalApiMaxQueuedActions)
            .Build();
    }

    /// <summary>
    /// Executes an operation with database bulkhead protection.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="BulkheadRejectedException">Thrown when the bulkhead rejects the operation.</exception>
    public async Task<T> ExecuteDatabaseOperationAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await DatabaseBulkhead.ExecuteAsync(async token => await operation(), default);
        }
        catch (Polly.RateLimiting.RateLimiterRejectedException ex)
        {
            _logger.LogWarning(
                "Database bulkhead rejected operation. Max parallelization: {Max}, Queue: {Queue}",
                _options.DatabaseMaxParallelization,
                _options.DatabaseMaxQueuedActions);

            throw new BulkheadRejectedException("Database", ex);
        }
    }

    /// <summary>
    /// Executes an operation with external API bulkhead protection.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="BulkheadRejectedException">Thrown when the bulkhead rejects the operation.</exception>
    public async Task<T> ExecuteExternalApiOperationAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await ExternalApiBulkhead.ExecuteAsync(async token => await operation(), default);
        }
        catch (Polly.RateLimiting.RateLimiterRejectedException ex)
        {
            _logger.LogWarning(
                "External API bulkhead rejected operation. Max parallelization: {Max}, Queue: {Queue}",
                _options.ExternalApiMaxParallelization,
                _options.ExternalApiMaxQueuedActions);

            throw new BulkheadRejectedException("ExternalApi", ex);
        }
    }

    /// <summary>
    /// Executes a void operation with database bulkhead protection.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <exception cref="BulkheadRejectedException">Thrown when the bulkhead rejects the operation.</exception>
    public async Task ExecuteDatabaseOperationAsync(Func<Task> operation)
    {
        try
        {
            await DatabaseBulkhead.ExecuteAsync(async token => { await operation(); return 0; }, default);
        }
        catch (Polly.RateLimiting.RateLimiterRejectedException ex)
        {
            _logger.LogWarning(
                "Database bulkhead rejected operation. Max parallelization: {Max}, Queue: {Queue}",
                _options.DatabaseMaxParallelization,
                _options.DatabaseMaxQueuedActions);

            throw new BulkheadRejectedException("Database", ex);
        }
    }

    /// <summary>
    /// Executes a void operation with external API bulkhead protection.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <exception cref="BulkheadRejectedException">Thrown when the bulkhead rejects the operation.</exception>
    public async Task ExecuteExternalApiOperationAsync(Func<Task> operation)
    {
        try
        {
            await ExternalApiBulkhead.ExecuteAsync(async token => { await operation(); return 0; }, default);
        }
        catch (Polly.RateLimiting.RateLimiterRejectedException ex)
        {
            _logger.LogWarning(
                "External API bulkhead rejected operation. Max parallelization: {Max}, Queue: {Queue}",
                _options.ExternalApiMaxParallelization,
                _options.ExternalApiMaxQueuedActions);

            throw new BulkheadRejectedException("ExternalApi", ex);
        }
    }
}
