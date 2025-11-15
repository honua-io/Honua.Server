// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Geoprocessing.Webhooks;

/// <summary>
/// Service for managing webhook deliveries with retry logic
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>
    /// Enqueues a webhook for delivery with retry support
    /// </summary>
    /// <param name="delivery">Webhook delivery request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created delivery ID</returns>
    Task<Guid> EnqueueAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next webhook ready for delivery
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Webhook to deliver, or null if queue is empty</returns>
    Task<WebhookDelivery?> DequeueNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records successful webhook delivery
    /// </summary>
    /// <param name="deliveryId">Delivery ID</param>
    /// <param name="responseStatus">HTTP response status code</param>
    /// <param name="responseBody">HTTP response body (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordSuccessAsync(Guid deliveryId, int responseStatus, string? responseBody = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records failed webhook delivery attempt
    /// </summary>
    /// <param name="deliveryId">Delivery ID</param>
    /// <param name="responseStatus">HTTP response status code (if available)</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordFailureAsync(Guid deliveryId, int? responseStatus, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets webhook delivery by ID
    /// </summary>
    /// <param name="deliveryId">Delivery ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Webhook delivery or null if not found</returns>
    Task<WebhookDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all webhook deliveries for a job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of webhook deliveries</returns>
    Task<WebhookDelivery[]> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default);
}
