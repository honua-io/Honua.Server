// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Synchronous wrapper methods partial class for SqlAlertDeduplicator.
/// Provides backward compatibility for tests and synchronous callers.
/// WARNING: These methods block the calling thread. Use async methods for production code.
/// </summary>
public sealed partial class SqlAlertDeduplicator
{
    /// <summary>
    /// Synchronously determines if an alert should be sent based on deduplication rules.
    /// WARNING: This method blocks the calling thread. Use ShouldSendAlertAsync for production code.
    /// </summary>
    /// <param name="fingerprint">The alert fingerprint for deduplication.</param>
    /// <param name="severity">The alert severity level.</param>
    /// <param name="reservationId">The reservation ID if the alert should be sent.</param>
    /// <returns>True if the alert should be sent, false if suppressed.</returns>
    public bool ShouldSendAlert(string fingerprint, string severity, out string reservationId)
    {
        var result = ShouldSendAlertAsync(fingerprint, severity).GetAwaiter().GetResult();
        reservationId = result.reservationId;
        return result.shouldSend;
    }

    /// <summary>
    /// Synchronously records that an alert was successfully sent.
    /// WARNING: This method blocks the calling thread. Use RecordAlertAsync for production code.
    /// </summary>
    /// <param name="fingerprint">The alert fingerprint.</param>
    /// <param name="severity">The alert severity level.</param>
    /// <param name="reservationId">The reservation ID from ShouldSendAlert.</param>
    public void RecordAlert(string fingerprint, string severity, string reservationId)
    {
        RecordAlertAsync(fingerprint, severity, reservationId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronously releases a reservation if the alert publishing failed.
    /// WARNING: This method blocks the calling thread. Use ReleaseReservationAsync for production code.
    /// </summary>
    /// <param name="reservationId">The reservation ID to release.</param>
    public void ReleaseReservation(string reservationId)
    {
        ReleaseReservationAsync(reservationId).GetAwaiter().GetResult();
    }
}
