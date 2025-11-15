// <copyright file="AlertEscalationStartupInitializer.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Initializes the alert escalation database schema on application startup.
/// </summary>
public sealed class AlertEscalationStartupInitializer : IHostedService
{
    private readonly IAlertEscalationStore escalationStore;
    private readonly ILogger<AlertEscalationStartupInitializer> logger;

    public AlertEscalationStartupInitializer(
        IAlertEscalationStore escalationStore,
        ILogger<AlertEscalationStartupInitializer> logger)
    {
        this.escalationStore = escalationStore;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation("Initializing alert escalation schema...");
            await this.escalationStore.EnsureSchemaAsync(cancellationToken);
            this.logger.LogInformation("Alert escalation schema initialized successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to initialize alert escalation schema");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
