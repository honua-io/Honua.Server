// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Interface for alert deduplication with async database operations.
/// </summary>
public interface IAlertDeduplicator
{
    /// <summary>
    /// Asynchronously determines if an alert should be sent based on deduplication rules.
    /// </summary>
    /// <param name="fingerprint">The alert fingerprint for deduplication.</param>
    /// <param name="severity">The alert severity level.</param>
    /// <param name="cancellationToken">Token to cancel the async operation.</param>
    /// <returns>A tuple containing whether to send the alert and the reservation ID if sending.</returns>
    Task<(bool shouldSend, string reservationId)> ShouldSendAlertAsync(
        string fingerprint,
        string severity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously records that an alert was successfully sent.
    /// </summary>
    /// <param name="fingerprint">The alert fingerprint.</param>
    /// <param name="severity">The alert severity level.</param>
    /// <param name="reservationId">The reservation ID from ShouldSendAlertAsync.</param>
    /// <param name="cancellationToken">Token to cancel the async operation.</param>
    Task RecordAlertAsync(
        string fingerprint,
        string severity,
        string reservationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously releases a reservation if the alert publishing failed.
    /// </summary>
    /// <param name="reservationId">The reservation ID to release.</param>
    /// <param name="cancellationToken">Token to cancel the async operation.</param>
    Task ReleaseReservationAsync(
        string reservationId,
        CancellationToken cancellationToken = default);
}
