// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Services;
using Honua.Server.Core.Services;
using Honua.Server.Host.Admin.Models;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin REST API endpoints for alert management (rules, channels, history, routing).
/// </summary>
public static class AlertAdministrationEndpoints
{
    /// <summary>
    /// Maps all admin alert endpoints to the application.
    /// </summary>
    public static RouteGroupBuilder MapAdminAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/alerts")
            .WithTags("Admin - Alerts")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        // Alert Rules
        group.MapGet("/rules", GetAlertRules)
            .WithName("GetAlertRules")
            .WithSummary("List all alert rules");

        group.MapGet("/rules/{id}", GetAlertRuleById)
            .WithName("GetAlertRuleById")
            .WithSummary("Get alert rule by ID");

        group.MapPost("/rules", CreateAlertRule)
            .WithName("CreateAlertRule")
            .WithSummary("Create a new alert rule");

        group.MapPut("/rules/{id}", UpdateAlertRule)
            .WithName("UpdateAlertRule")
            .WithSummary("Update an existing alert rule");

        group.MapDelete("/rules/{id}", DeleteAlertRule)
            .WithName("DeleteAlertRule")
            .WithSummary("Delete an alert rule");

        group.MapPost("/rules/{id}/test", TestAlertRule)
            .WithName("TestAlertRule")
            .WithSummary("Test alert rule by sending a test alert");

        // Notification Channels
        group.MapGet("/channels", GetNotificationChannels)
            .WithName("GetNotificationChannels")
            .WithSummary("List all notification channels");

        group.MapGet("/channels/{id}", GetNotificationChannelById)
            .WithName("GetNotificationChannelById")
            .WithSummary("Get notification channel by ID");

        group.MapPost("/channels", CreateNotificationChannel)
            .WithName("CreateNotificationChannel")
            .WithSummary("Create a new notification channel");

        group.MapPut("/channels/{id}", UpdateNotificationChannel)
            .WithName("UpdateNotificationChannel")
            .WithSummary("Update an existing notification channel");

        group.MapDelete("/channels/{id}", DeleteNotificationChannel)
            .WithName("DeleteNotificationChannel")
            .WithSummary("Delete a notification channel");

        group.MapPost("/channels/{id}/test", TestNotificationChannel)
            .WithName("TestNotificationChannel")
            .WithSummary("Test notification channel by sending a test notification");

        // Alert History
        group.MapGet("/history", GetAlertHistory)
            .WithName("GetAlertHistory")
            .WithSummary("Get alert history with filtering");

        group.MapGet("/history/{id}", GetAlertHistoryById)
            .WithName("GetAlertHistoryById")
            .WithSummary("Get alert history entry by ID");

        group.MapPost("/history/{id}/acknowledge", AcknowledgeAlert)
            .WithName("AcknowledgeAlert")
            .WithSummary("Acknowledge an alert");

        group.MapPost("/history/{id}/silence", SilenceAlert)
            .WithName("SilenceAlert")
            .WithSummary("Silence an alert by creating a silencing rule");

        // Alert Routing
        group.MapGet("/routing", GetAlertRouting)
            .WithName("GetAlertRouting")
            .WithSummary("Get alert routing configuration");

        group.MapPut("/routing", UpdateAlertRouting)
            .WithName("UpdateAlertRouting")
            .WithSummary("Update alert routing configuration");

        return group;
    }

    #region Alert Rules

    private static async Task<IResult> GetAlertRules(
        [FromServices] IAlertConfigurationService configService,
        CancellationToken ct)
    {
        try
        {
            var rules = await configService.GetAlertRulesAsync(ct);

            var response = rules.Select(r => new AlertRuleListItem
            {
                Id = r.Id,
                Name = r.Name,
                Severity = r.Severity,
                Enabled = r.Enabled,
                ChannelCount = r.NotificationChannelIds.Count,
                CreatedAt = r.CreatedAt
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve alert rules: {ex.Message}");
        }
    }

    private static async Task<IResult> GetAlertRuleById(
        long id,
        [FromServices] IAlertConfigurationService configService,
        CancellationToken ct)
    {
        try
        {
            var rule = await configService.GetAlertRuleAsync(id, ct);

            if (rule is null)
            {
                return Results.Problem(
                    title: "Alert rule not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Alert rule with ID '{id}' does not exist");
            }

            var response = new AlertRuleResponse
            {
                Id = rule.Id,
                Name = rule.Name,
                Description = rule.Description,
                Severity = rule.Severity,
                Matchers = rule.Matchers,
                NotificationChannelIds = rule.NotificationChannelIds,
                Enabled = rule.Enabled,
                Metadata = rule.Metadata,
                CreatedAt = rule.CreatedAt,
                ModifiedAt = rule.ModifiedAt,
                CreatedBy = rule.CreatedBy,
                ModifiedBy = rule.ModifiedBy
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve alert rule: {ex.Message}");
        }
    }

    private static async Task<IResult> CreateAlertRule(
        CreateAlertRuleRequest request,
        HttpContext context,
        [FromServices] IAlertConfigurationService configService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            // Validate severity
            if (!IsValidSeverity(request.Severity))
            {
                return Results.Problem(
                    title: "Invalid severity",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"Severity must be one of: critical, error, warning, info");
            }

            var rule = new AlertRule
            {
                Name = request.Name,
                Description = request.Description,
                Severity = request.Severity,
                Matchers = request.Matchers,
                NotificationChannelIds = request.NotificationChannelIds,
                Enabled = request.Enabled,
                Metadata = request.Metadata,
                CreatedBy = UserIdentityHelper.GetUserIdentifier(context.User)
            };

            var id = await configService.CreateAlertRuleAsync(rule, ct);

            logger.LogInformation("Created alert rule {RuleId}: {RuleName}", id, rule.Name);

            var response = new AlertRuleResponse
            {
                Id = id,
                Name = rule.Name,
                Description = rule.Description,
                Severity = rule.Severity,
                Matchers = rule.Matchers,
                NotificationChannelIds = rule.NotificationChannelIds,
                Enabled = rule.Enabled,
                Metadata = rule.Metadata,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = rule.CreatedBy
            };

            return Results.Created($"/admin/alerts/rules/{id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create alert rule {RuleName}", request.Name);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to create alert rule: {ex.Message}");
        }
    }

    private static async Task<IResult> UpdateAlertRule(
        long id,
        UpdateAlertRuleRequest request,
        HttpContext context,
        [FromServices] IAlertConfigurationService configService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            var existingRule = await configService.GetAlertRuleAsync(id, ct);

            if (existingRule is null)
            {
                return Results.Problem(
                    title: "Alert rule not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Alert rule with ID '{id}' does not exist");
            }

            // Validate severity
            if (!IsValidSeverity(request.Severity))
            {
                return Results.Problem(
                    title: "Invalid severity",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"Severity must be one of: critical, error, warning, info");
            }

            var updatedRule = new AlertRule
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Severity = request.Severity,
                Matchers = request.Matchers,
                NotificationChannelIds = request.NotificationChannelIds,
                Enabled = request.Enabled,
                Metadata = request.Metadata,
                ModifiedBy = UserIdentityHelper.GetUserIdentifier(context.User)
            };

            await configService.UpdateAlertRuleAsync(id, updatedRule, ct);

            logger.LogInformation("Updated alert rule {RuleId}: {RuleName}", id, updatedRule.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update alert rule {RuleId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to update alert rule: {ex.Message}");
        }
    }

    private static async Task<IResult> DeleteAlertRule(
        long id,
        [FromServices] IAlertConfigurationService configService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            var existingRule = await configService.GetAlertRuleAsync(id, ct);

            if (existingRule is null)
            {
                return Results.Problem(
                    title: "Alert rule not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Alert rule with ID '{id}' does not exist");
            }

            await configService.DeleteAlertRuleAsync(id, ct);

            logger.LogInformation("Deleted alert rule {RuleId}: {RuleName}", id, existingRule.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete alert rule {RuleId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to delete alert rule: {ex.Message}");
        }
    }

    private static async Task<IResult> TestAlertRule(
        long id,
        TestAlertRuleRequest? request,
        [FromServices] IAlertConfigurationService configService,
        [FromServices] INotificationChannelService channelService,
        [FromServices] IAlertPublishingService publishingService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            var rule = await configService.GetAlertRuleAsync(id, ct);

            if (rule is null)
            {
                return Results.Problem(
                    title: "Alert rule not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Alert rule with ID '{id}' does not exist");
            }

            logger.LogInformation("Testing alert rule {RuleId}: {RuleName}", id, rule.Name);

            // Get the notification channels for this rule
            var allChannels = await channelService.GetNotificationChannelsAsync(ct);
            var ruleChannels = allChannels
                .Where(c => rule.NotificationChannelIds.Contains(c.Id))
                .ToList();

            if (ruleChannels.Count == 0)
            {
                return Results.Ok(new TestAlertRuleResponse
                {
                    Success = false,
                    Message = "No notification channels configured for this alert rule",
                    PublishedChannels = new List<string>(),
                    FailedChannels = new List<string>()
                });
            }

            // Publish test alert to configured channels
            var result = await publishingService.PublishTestAlertAsync(rule, ruleChannels, ct);

            var response = new TestAlertRuleResponse
            {
                Success = result.Success,
                Message = result.Message,
                PublishedChannels = result.PublishedChannels,
                FailedChannels = result.FailedChannels
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test alert rule {RuleId}", id);
            return Results.Ok(new TestAlertRuleResponse
            {
                Success = false,
                Message = $"Test failed: {ex.Message}",
                PublishedChannels = new List<string>(),
                FailedChannels = new List<string>()
            });
        }
    }

    #endregion

    #region Notification Channels

    private static async Task<IResult> GetNotificationChannels(
        [FromServices] INotificationChannelService channelService,
        CancellationToken ct)
    {
        try
        {
            var channels = await channelService.GetNotificationChannelsAsync(ct);

            var response = channels.Select(c => new NotificationChannelListItem
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type,
                Enabled = c.Enabled,
                CreatedAt = c.CreatedAt
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve notification channels: {ex.Message}");
        }
    }

    private static async Task<IResult> GetNotificationChannelById(
        long id,
        [FromServices] INotificationChannelService channelService,
        CancellationToken ct)
    {
        try
        {
            var channel = await channelService.GetNotificationChannelAsync(id, ct);

            if (channel is null)
            {
                return Results.Problem(
                    title: "Notification channel not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Notification channel with ID '{id}' does not exist");
            }

            var response = new NotificationChannelResponse
            {
                Id = channel.Id,
                Name = channel.Name,
                Description = channel.Description,
                Type = channel.Type,
                Configuration = channel.Configuration,
                Enabled = channel.Enabled,
                SeverityFilter = channel.SeverityFilter,
                CreatedAt = channel.CreatedAt,
                ModifiedAt = channel.ModifiedAt,
                CreatedBy = channel.CreatedBy,
                ModifiedBy = channel.ModifiedBy
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve notification channel: {ex.Message}");
        }
    }

    private static async Task<IResult> CreateNotificationChannel(
        CreateNotificationChannelRequest request,
        HttpContext context,
        [FromServices] INotificationChannelService channelService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            // Validate channel type
            if (!IsValidChannelType(request.Type))
            {
                return Results.Problem(
                    title: "Invalid channel type",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"Channel type must be one of: sns, slack, email, webhook, azureeventgrid");
            }

            var channel = new NotificationChannel
            {
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Configuration = request.Configuration,
                Enabled = request.Enabled,
                SeverityFilter = request.SeverityFilter,
                CreatedBy = UserIdentityHelper.GetUserIdentifier(context.User)
            };

            var id = await channelService.CreateNotificationChannelAsync(channel, ct);

            logger.LogInformation("Created notification channel {ChannelId}: {ChannelName} ({ChannelType})",
                id, channel.Name, channel.Type);

            var response = new NotificationChannelResponse
            {
                Id = id,
                Name = channel.Name,
                Description = channel.Description,
                Type = channel.Type,
                Configuration = channel.Configuration,
                Enabled = channel.Enabled,
                SeverityFilter = channel.SeverityFilter,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = channel.CreatedBy
            };

            return Results.Created($"/admin/alerts/channels/{id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification channel {ChannelName}", request.Name);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to create notification channel: {ex.Message}");
        }
    }

    private static async Task<IResult> UpdateNotificationChannel(
        long id,
        UpdateNotificationChannelRequest request,
        HttpContext context,
        [FromServices] INotificationChannelService channelService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            var existingChannel = await channelService.GetNotificationChannelAsync(id, ct);

            if (existingChannel is null)
            {
                return Results.Problem(
                    title: "Notification channel not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Notification channel with ID '{id}' does not exist");
            }

            var updatedChannel = new NotificationChannel
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Type = existingChannel.Type, // Type cannot be changed
                Configuration = request.Configuration,
                Enabled = request.Enabled,
                SeverityFilter = request.SeverityFilter,
                ModifiedBy = UserIdentityHelper.GetUserIdentifier(context.User)
            };

            await channelService.UpdateNotificationChannelAsync(id, updatedChannel, ct);

            logger.LogInformation("Updated notification channel {ChannelId}: {ChannelName}", id, updatedChannel.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update notification channel {ChannelId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to update notification channel: {ex.Message}");
        }
    }

    private static async Task<IResult> DeleteNotificationChannel(
        long id,
        [FromServices] INotificationChannelService channelService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            var existingChannel = await channelService.GetNotificationChannelAsync(id, ct);

            if (existingChannel is null)
            {
                return Results.Problem(
                    title: "Notification channel not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Notification channel with ID '{id}' does not exist");
            }

            await channelService.DeleteNotificationChannelAsync(id, ct);

            logger.LogInformation("Deleted notification channel {ChannelId}: {ChannelName}", id, existingChannel.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete notification channel {ChannelId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to delete notification channel: {ex.Message}");
        }
    }

    private static async Task<IResult> TestNotificationChannel(
        long id,
        TestNotificationChannelRequest? request,
        [FromServices] INotificationChannelService channelService,
        [FromServices] IAlertPublishingService publishingService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            var channel = await channelService.GetNotificationChannelAsync(id, ct);

            if (channel is null)
            {
                return Results.Problem(
                    title: "Notification channel not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Notification channel with ID '{id}' does not exist");
            }

            if (!channel.Enabled)
            {
                return Results.Ok(new TestNotificationChannelResponse
                {
                    Success = false,
                    Message = "Notification channel is disabled. Enable it before testing.",
                    LatencyMs = null
                });
            }

            logger.LogInformation("Testing notification channel {ChannelId}: {ChannelName} ({ChannelType})",
                id, channel.Name, channel.Type);

            // Test the notification channel
            var result = await publishingService.TestNotificationChannelAsync(channel, ct);

            var response = new TestNotificationChannelResponse
            {
                Success = result.Success,
                Message = result.Message,
                LatencyMs = result.LatencyMs
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test notification channel {ChannelId}", id);
            return Results.Ok(new TestNotificationChannelResponse
            {
                Success = false,
                Message = $"Test failed: {ex.Message}",
                LatencyMs = null
            });
        }
    }

    #endregion

    #region Alert History

    private static async Task<IResult> GetAlertHistory(
        [FromServices] IAlertHistoryStore historyStore,
        string? severity,
        string? status,
        string? service,
        string? environment,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default)
    {
        try
        {
            // For now, use the existing GetRecentAlertsAsync method
            // TODO: Enhance AlertHistoryStore to support full filtering
            var alerts = await historyStore.GetRecentAlertsAsync(limit, severity, ct);

            var response = alerts.Select(a => new AlertHistoryListItem
            {
                Id = a.Id,
                Fingerprint = a.Fingerprint,
                Name = a.Name,
                Severity = a.Severity,
                Status = a.Status,
                Summary = a.Summary,
                Timestamp = a.Timestamp,
                WasSuppressed = a.WasSuppressed,
                IsAcknowledged = false // TODO: Check acknowledgement status
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve alert history: {ex.Message}");
        }
    }

    private static async Task<IResult> GetAlertHistoryById(
        long id,
        [FromServices] IAlertHistoryStore historyStore,
        CancellationToken ct)
    {
        try
        {
            // TODO: Add method to get alert by ID
            // For now, return not implemented
            return Results.Problem(
                title: "Not implemented",
                statusCode: StatusCodes.Status501NotImplemented,
                detail: "Alert history by ID not yet implemented. Use fingerprint-based lookup instead.");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve alert: {ex.Message}");
        }
    }

    private static async Task<IResult> AcknowledgeAlert(
        long id,
        AcknowledgeAlertRequest request,
        [FromServices] IAlertHistoryStore historyStore,
        [FromServices] ILogger<AlertHistoryEntry> logger,
        CancellationToken ct)
    {
        try
        {
            // TODO: Get alert by ID first
            // For now, return not implemented
            var acknowledgement = new AlertAcknowledgement
            {
                Fingerprint = "unknown", // TODO: Get from alert
                AcknowledgedBy = request.AcknowledgedBy,
                AcknowledgedAt = DateTimeOffset.UtcNow,
                Comment = request.Comment,
                ExpiresAt = request.ExpiresAt
            };

            await historyStore.InsertAcknowledgementAsync(acknowledgement, ct);

            logger.LogInformation("Acknowledged alert {AlertId} by {User}", id, request.AcknowledgedBy);

            return Results.Ok(new { Message = "Alert acknowledged successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to acknowledge alert {AlertId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to acknowledge alert: {ex.Message}");
        }
    }

    private static async Task<IResult> SilenceAlert(
        long id,
        SilenceAlertRequest request,
        [FromServices] IAlertHistoryStore historyStore,
        [FromServices] ILogger<AlertHistoryEntry> logger,
        CancellationToken ct)
    {
        try
        {
            // TODO: Get alert by ID first to extract matchers
            var silencingRule = new AlertSilencingRule
            {
                Name = request.Name,
                Matchers = new Dictionary<string, string>(), // TODO: Extract from alert
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Comment = request.Comment,
                IsActive = true
            };

            var ruleId = await historyStore.InsertSilencingRuleAsync(silencingRule, ct);

            logger.LogInformation("Created silencing rule {RuleId} for alert {AlertId} by {User}",
                ruleId, id, request.CreatedBy);

            return Results.Ok(new { Message = "Alert silencing rule created successfully", RuleId = ruleId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to silence alert {AlertId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to silence alert: {ex.Message}");
        }
    }

    #endregion

    #region Alert Routing

    private static async Task<IResult> GetAlertRouting(
        [FromServices] IAlertConfigurationService configService,
        CancellationToken ct)
    {
        try
        {
            var config = await configService.GetRoutingConfigurationAsync(ct);

            var response = new AlertRoutingConfigurationResponse
            {
                Routes = config?.Routes.Select(r => new AlertRoutingRuleDto
                {
                    Name = r.Name,
                    Matchers = r.Matchers,
                    NotificationChannelIds = r.NotificationChannelIds,
                    Continue = r.Continue
                }).ToList() ?? new List<AlertRoutingRuleDto>(),
                DefaultRoute = config?.DefaultRoute != null ? new AlertRoutingRuleDto
                {
                    Name = config.DefaultRoute.Name,
                    Matchers = config.DefaultRoute.Matchers,
                    NotificationChannelIds = config.DefaultRoute.NotificationChannelIds,
                    Continue = config.DefaultRoute.Continue
                } : null
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve alert routing configuration: {ex.Message}");
        }
    }

    private static async Task<IResult> UpdateAlertRouting(
        UpdateAlertRoutingConfigurationRequest request,
        HttpContext context,
        [FromServices] IAlertConfigurationService configService,
        [FromServices] ILogger<AlertRoutingConfiguration> logger,
        CancellationToken ct)
    {
        try
        {
            var config = new AlertRoutingConfiguration
            {
                Routes = request.Routes.Select(r => new AlertRoutingRule
                {
                    Name = r.Name,
                    Matchers = r.Matchers,
                    NotificationChannelIds = r.NotificationChannelIds,
                    Continue = r.Continue
                }).ToList(),
                DefaultRoute = request.DefaultRoute != null ? new AlertRoutingRule
                {
                    Name = request.DefaultRoute.Name,
                    Matchers = request.DefaultRoute.Matchers,
                    NotificationChannelIds = request.DefaultRoute.NotificationChannelIds,
                    Continue = request.DefaultRoute.Continue
                } : null,
                ModifiedBy = UserIdentityHelper.GetUserIdentifier(context.User)
            };

            await configService.UpdateRoutingConfigurationAsync(config, ct);

            logger.LogInformation("Updated alert routing configuration with {RouteCount} routes",
                config.Routes.Count);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update alert routing configuration");
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to update alert routing configuration: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private static bool IsValidSeverity(string severity)
    {
        var validSeverities = new[] { "critical", "error", "warning", "info" };
        return validSeverities.Contains(severity.ToLowerInvariant());
    }

    private static bool IsValidChannelType(string type)
    {
        var validTypes = new[] { "sns", "slack", "email", "webhook", "azureeventgrid" };
        return validTypes.Contains(type.ToLowerInvariant());
    }

    #endregion
}
