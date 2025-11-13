// <copyright file="CompositeAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Models;
using Honua.Server.Core.Utilities;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to multiple providers simultaneously with throttling.
/// Limits concurrent publisher executions to prevent overwhelming downstream services.
/// </summary>
public sealed class CompositeAlertPublisher : DisposableBase, IAlertPublisher
{
    private readonly IEnumerable<IAlertPublisher> publishers;
    private readonly ILogger<CompositeAlertPublisher> logger;

    // CONCURRENCY FIX: Throttle concurrent publishing to prevent overwhelming downstream services
    // Allows max 10 concurrent publisher executions
    private readonly SemaphoreSlim concurrencyThrottle = new(10, 10);

    public CompositeAlertPublisher(
        IEnumerable<IAlertPublisher> publishers,
        ILogger<CompositeAlertPublisher> logger)
    {
        this.publishers = publishers ?? throw new ArgumentNullException(nameof(publishers));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        var errors = new List<Exception>();

        foreach (var publisher in this.publishers)
        {
            // Fire-and-forget pattern with error capture
        var task = this.PublishWithErrorHandling(publisher, webhook, severity, errors, cancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (errors.Count > 0)
        {
            this.logger.LogWarning(
                "Published to {SuccessCount}/{TotalCount} providers, {ErrorCount} failed",
                this.publishers.Count() - errors.Count,
                this.publishers.Count(),
                errors.Count);

            // Re-throw if ALL publishers failed
            if (errors.Count == this.publishers.Count())
            {
                throw new AggregateException("All alert publishers failed", errors);
            }
        }
        else
        {
            this.logger.LogInformation(
                "Successfully published alert to {Count} providers",
                this.publishers.Count());
        }
    }

    private async Task PublishWithErrorHandling(
        IAlertPublisher publisher,
        AlertManagerWebhook webhook,
        string severity,
        List<Exception> errors,
        CancellationToken cancellationToken)
    {
        // RESOURCE LEAK FIX: Track semaphore acquisition to ensure proper release even on cancellation
        var semaphoreAcquired = false;
        try
        {
            // CONCURRENCY FIX: Throttle concurrent publishing
            await this.concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            semaphoreAcquired = true;

            await publisher.PublishAsync(webhook, severity, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, don't log as error
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Publisher {Publisher} failed to publish alert", publisher.GetType().Name);
            lock (errors)
            {
                errors.Add(ex);
            }
        }
        finally
        {
            // RESOURCE LEAK FIX: Only release if we actually acquired the semaphore
            if (semaphoreAcquired)
            {
                this.concurrencyThrottle.Release();
            }
        }
    }

    protected override void DisposeCore()
    {
        this.concurrencyThrottle.Dispose();
    }
}
