// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.AuditLog;

/// <summary>
/// Extension methods for registering audit logging services
/// </summary>
public static class AuditLogServiceCollectionExtensions
{
    /// <summary>
    /// Adds audit logging services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Check if audit logging is enabled
        var enabled = configuration.GetValue<bool>("AuditLog:Enabled", true);
        if (!enabled)
        {
            // Register a no-op implementation
            services.AddSingleton<IAuditLogService, NoOpAuditLogService>();
            return services;
        }

        // Get database connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is required for audit logging.");
        }

        // Register audit log service
        services.AddSingleton<IAuditLogService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresAuditLogService>>();
            return new PostgresAuditLogService(connectionString, logger);
        });

        // Register background service for periodic archival
        var enableAutoArchival = configuration.GetValue<bool>("AuditLog:EnableAutoArchival", false);
        if (enableAutoArchival)
        {
            services.AddHostedService<AuditLogArchivalService>();
        }

        return services;
    }

    /// <summary>
    /// Adds audit log middleware to the application pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for method chaining</returns>
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<AuditLogMiddleware>();
    }
}

/// <summary>
/// No-op implementation of audit log service (when disabled)
/// </summary>
internal class NoOpAuditLogService : IAuditLogService
{
    public Task RecordAsync(AuditEvent @event, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RecordBatchAsync(IEnumerable<AuditEvent> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AuditLogResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new AuditLogResult());

    public Task<AuditEvent?> GetByIdAsync(Guid eventId, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<AuditEvent?>(null);

    public Task<AuditLogStatistics> GetStatisticsAsync(
        Guid? tenantId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AuditLogStatistics());

    public Task<string> ExportToCsvAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);

    public Task<string> ExportToJsonAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);

    public Task<long> ArchiveEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
        => Task.FromResult(0L);

    public Task<long> PurgeArchivedEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
        => Task.FromResult(0L);
}

/// <summary>
/// Background service for periodic audit log archival
/// </summary>
internal class AuditLogArchivalService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogArchivalService> _logger;
    private readonly TimeSpan _archivalInterval = TimeSpan.FromDays(1);
    private readonly int _archiveAfterDays = 90;

    public AuditLogArchivalService(
        IAuditLogService auditLogService,
        ILogger<AuditLogArchivalService> logger)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit log archival service started (archives events older than {Days} days)", _archiveAfterDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoffDate = DateTimeOffset.UtcNow.AddDays(-_archiveAfterDays);
                var archived = await _auditLogService.ArchiveEventsAsync(cutoffDate, stoppingToken);

                if (archived > 0)
                {
                    _logger.LogInformation("Archived {Count} audit events older than {Date}", archived, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving audit events");
            }

            await Task.Delay(_archivalInterval, stoppingToken);
        }

        _logger.LogInformation("Audit log archival service stopped");
    }
}
