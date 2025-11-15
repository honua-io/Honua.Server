// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Coordination;

/// <summary>
/// Background service that maintains leadership through automatic renewal.
/// Ensures this instance remains the leader for the configured resource.
/// </summary>
/// <remarks>
/// This service is responsible for:
///
/// 1. Initial Leadership Acquisition:
///    - Attempts to acquire leadership on startup
///    - Retries until successful or service is stopped
///    - Logs when leadership is acquired
///
/// 2. Automatic Renewal:
///    - Renews leadership lease every RenewalInterval
///    - Prevents leadership expiry during normal operation
///    - Detects leadership loss and stops processing
///
/// 3. Graceful Release:
///    - Releases leadership on shutdown
///    - Enables immediate failover to other instances
///    - Ensures clean cluster state
///
/// Usage Pattern:
/// - Other services should check IsLeader before processing
/// - Listen to leadership change events (future enhancement)
/// - Implement health checks based on leadership status
///
/// HA Deployment Considerations:
/// - Only the leader should process singleton tasks
/// - Non-leaders should wait for leadership acquisition
/// - Failed leaders automatically hand off after lease expiry
/// - Configure lease duration based on failover requirements
/// </remarks>
public sealed class LeaderElectionService : BackgroundService, IDisposable
{
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<LeaderElectionService> _logger;
    private readonly LeaderElectionOptions _options;
    private bool _isLeader;
    private readonly object _leadershipLock = new();

    public LeaderElectionService(
        ILeaderElection leaderElection,
        ILogger<LeaderElectionService> logger,
        IOptions<LeaderElectionOptions> options)
    {
        _leaderElection = leaderElection ?? throw new ArgumentNullException(nameof(leaderElection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _options.Validate();
    }

    /// <summary>
    /// Gets whether this instance is currently the leader.
    /// Thread-safe property for checking leadership status.
    /// </summary>
    public bool IsLeader
    {
        get
        {
            lock (_leadershipLock)
            {
                return _isLeader;
            }
        }
        private set
        {
            lock (_leadershipLock)
            {
                _isLeader = value;
            }
        }
    }

    /// <summary>
    /// Gets the instance ID of this leader election participant.
    /// </summary>
    public string InstanceId => _leaderElection.InstanceId;

    /// <summary>
    /// Main execution loop: acquire leadership and maintain it through renewal.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "LeaderElectionService starting: Resource={Resource}, InstanceId={InstanceId}",
            _options.ResourceName, _leaderElection.InstanceId);

        try
        {
            // Attempt to acquire leadership
            await AcquireLeadershipAsync(stoppingToken).ConfigureAwait(false);

            // Main renewal loop
            await RenewalLoopAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "LeaderElectionService stopping: Resource={Resource}, InstanceId={InstanceId}",
                _options.ResourceName, _leaderElection.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LeaderElectionService failed: Resource={Resource}, InstanceId={InstanceId}",
                _options.ResourceName, _leaderElection.InstanceId);
            throw;
        }
    }

    /// <summary>
    /// Attempts to acquire leadership, retrying until successful or cancelled.
    /// </summary>
    private async Task AcquireLeadershipAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Attempting to acquire leadership: Resource={Resource}, InstanceId={InstanceId}",
            _options.ResourceName, _leaderElection.InstanceId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var acquired = await _leaderElection.TryAcquireLeadershipAsync(
                    _options.ResourceName,
                    cancellationToken).ConfigureAwait(false);

                if (acquired)
                {
                    IsLeader = true;

                    _logger.LogInformation(
                        "Leadership acquired successfully: Resource={Resource}, InstanceId={InstanceId}, Lease={Lease}s",
                        _options.ResourceName, _leaderElection.InstanceId, _options.LeaseDurationSeconds);

                    return;
                }

                // Leadership is held by another instance, wait and retry
                _logger.LogDebug(
                    "Leadership is held by another instance, will retry in {Interval}s: Resource={Resource}",
                    _options.RenewalIntervalSeconds, _options.ResourceName);

                await Task.Delay(_options.RenewalInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during leadership acquisition, will retry in {Interval}s: Resource={Resource}, InstanceId={InstanceId}",
                    _options.RenewalIntervalSeconds, _options.ResourceName, _leaderElection.InstanceId);

                await Task.Delay(_options.RenewalInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Maintains leadership through periodic renewal.
    /// </summary>
    private async Task RenewalLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for renewal interval
                await Task.Delay(_options.RenewalInterval, cancellationToken).ConfigureAwait(false);

                // Renew leadership
                var renewed = await _leaderElection.RenewLeadershipAsync(
                    _options.ResourceName,
                    cancellationToken).ConfigureAwait(false);

                if (!renewed)
                {
                    // Lost leadership - another instance may have taken over
                    IsLeader = false;

                    _logger.LogWarning(
                        "Leadership lost: Resource={Resource}, InstanceId={InstanceId}. Attempting to reacquire...",
                        _options.ResourceName, _leaderElection.InstanceId);

                    // Try to reacquire leadership
                    await AcquireLeadershipAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Leadership renewed: Resource={Resource}, InstanceId={InstanceId}, NextRenewal={NextRenewal}s",
                        _options.ResourceName, _leaderElection.InstanceId, _options.RenewalIntervalSeconds);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during leadership renewal: Resource={Resource}, InstanceId={InstanceId}",
                    _options.ResourceName, _leaderElection.InstanceId);

                // On error, assume leadership is lost
                IsLeader = false;

                // Try to reacquire
                try
                {
                    await AcquireLeadershipAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception acquireEx)
                {
                    _logger.LogError(acquireEx,
                        "Failed to reacquire leadership after renewal error: Resource={Resource}, InstanceId={InstanceId}",
                        _options.ResourceName, _leaderElection.InstanceId);
                }
            }
        }
    }

    /// <summary>
    /// Gracefully stops the service and releases leadership.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "LeaderElectionService stopping gracefully: Resource={Resource}, InstanceId={InstanceId}",
            _options.ResourceName, _leaderElection.InstanceId);

        try
        {
            // Release leadership to allow immediate failover
            if (IsLeader)
            {
                var released = await _leaderElection.ReleaseLeadershipAsync(
                    _options.ResourceName,
                    cancellationToken).ConfigureAwait(false);

                if (released)
                {
                    IsLeader = false;

                    _logger.LogInformation(
                        "Leadership released during shutdown: Resource={Resource}, InstanceId={InstanceId}",
                        _options.ResourceName, _leaderElection.InstanceId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to release leadership during shutdown (may have already expired): Resource={Resource}, InstanceId={InstanceId}",
                        _options.ResourceName, _leaderElection.InstanceId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error releasing leadership during shutdown: Resource={Resource}, InstanceId={InstanceId}",
                _options.ResourceName, _leaderElection.InstanceId);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public new void Dispose()
    {
        base.Dispose();
    }
}
