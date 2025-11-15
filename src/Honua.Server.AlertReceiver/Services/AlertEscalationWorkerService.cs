// <copyright file="AlertEscalationWorkerService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Options;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Background service that periodically processes pending alert escalations.
/// </summary>
public sealed class AlertEscalationWorkerService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<AlertEscalationWorkerService> logger;
    private readonly AlertEscalationOptions options;

    public AlertEscalationWorkerService(
        IServiceProvider serviceProvider,
        IOptions<AlertEscalationOptions> options,
        ILogger<AlertEscalationWorkerService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.Enabled)
        {
            this.logger.LogInformation("Alert escalation is disabled in configuration");
            return;
        }

        this.logger.LogInformation(
            "Alert escalation worker started (check interval: {Interval}s, batch size: {BatchSize})",
            this.options.CheckIntervalSeconds,
            this.options.BatchSize);

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.ProcessEscalationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing alert escalations");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(this.options.CheckIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        this.logger.LogInformation("Alert escalation worker stopped");
    }

    private async Task ProcessEscalationsAsync(CancellationToken cancellationToken)
    {
        // Create a scope to get scoped services
        using var scope = this.serviceProvider.CreateScope();
        var escalationService = scope.ServiceProvider.GetRequiredService<IAlertEscalationService>();

        try
        {
            await escalationService.ProcessPendingEscalationsAsync(this.options.BatchSize, cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to process pending escalations");
        }
    }
}

/// <summary>
/// Configuration options for alert escalation.
/// </summary>
public sealed class AlertEscalationOptions
{
    /// <summary>
    /// Whether alert escalation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often to check for pending escalations (in seconds).
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of escalations to process in one batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Default escalation policies.
    /// These are loaded from configuration and can be used to seed the database.
    /// </summary>
    public Dictionary<string, List<ConfiguredEscalationLevel>>? Policies { get; set; }
}

/// <summary>
/// Escalation level configuration from appsettings.json.
/// </summary>
public sealed class ConfiguredEscalationLevel
{
    public string Delay { get; set; } = "00:00:00";

    public List<string> NotificationChannels { get; set; } = new();

    public string? SeverityOverride { get; set; }

    public Dictionary<string, string>? CustomProperties { get; set; }

    /// <summary>
    /// Converts the configured delay string to a TimeSpan.
    /// </summary>
    public TimeSpan GetDelay()
    {
        if (TimeSpan.TryParse(this.Delay, out var delay))
        {
            return delay;
        }

        throw new InvalidOperationException($"Invalid delay format: {this.Delay}");
    }
}
