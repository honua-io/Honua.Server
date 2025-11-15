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
        var result = await this.PublishWithResultAsync(webhook, severity, cancellationToken).ConfigureAwait(false);

        // Maintain backward compatibility - throw if all failed
        if (result.AllFailed)
        {
            throw new InvalidOperationException($"All alert publishers failed. Failed channels: {string.Join(", ", result.FailedChannels)}");
        }
    }

    public async Task<AlertDeliveryResult> PublishWithResultAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        var errors = new Dictionary<string, Exception>();
        var successful = new HashSet<string>();

        foreach (var publisher in this.publishers)
        {
            var task = this.PublishWithResultTracking(publisher, webhook, severity, successful, errors, cancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var result = new AlertDeliveryResult
        {
            SuccessfulChannels = successful.ToList(),
            FailedChannels = errors.Keys.ToList(),
        };

        if (errors.Count > 0)
        {
            this.logger.LogWarning(
                "Published to {SuccessCount}/{TotalCount} providers, {ErrorCount} failed. Successful: [{Successful}], Failed: [{Failed}]",
                successful.Count,
                this.publishers.Count(),
                errors.Count,
                string.Join(", ", successful),
                string.Join(", ", errors.Keys));
        }
        else
        {
            this.logger.LogInformation(
                "Successfully published alert to {Count} providers: [{Channels}]",
                this.publishers.Count(),
                string.Join(", ", successful));
        }

        return result;
    }

    private async Task PublishWithResultTracking(
        IAlertPublisher publisher,
        AlertManagerWebhook webhook,
        string severity,
        HashSet<string> successful,
        Dictionary<string, Exception> errors,
        CancellationToken cancellationToken)
    {
        var semaphoreAcquired = false;
        var publisherName = publisher.GetType().Name.Replace("AlertPublisher", string.Empty);

        try
        {
            await this.concurrencyThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            semaphoreAcquired = true;

            await publisher.PublishAsync(webhook, severity, cancellationToken).ConfigureAwait(false);

            lock (successful)
            {
                successful.Add(publisherName);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Publisher {Publisher} failed to publish alert", publisherName);
            lock (errors)
            {
                errors[publisherName] = ex;
            }
        }
        finally
        {
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
