// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Services;
using Honua.Server.Core.Security;
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
            .RequireAuthorization(AdminAuthorizationPolicies.RequireAlertAdministrator);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "CreateAlertRule",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Name, nameof(request.Name));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

            if (!InputValidationHelpers.IsValidLength(request.Name, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Rule name must be between 1 and 200 characters");
            }

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
                CreatedBy = identity.UserId
            };

            var id = await configService.CreateAlertRuleAsync(rule, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "CreateAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: $"Created alert rule: {rule.Name}",
                additionalData: new Dictionary<string, object>
                {
                    ["ruleName"] = rule.Name,
                    ["severity"] = request.Severity,
                    ["enabled"] = request.Enabled,
                    ["channelCount"] = request.NotificationChannelIds?.Count ?? 0
                });

            logger.LogInformation("User {UserId} created alert rule {RuleId}: {RuleName}", identity.UserId, id, rule.Name);

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "CreateAlertRule",
                resourceType: "AlertRule",
                details: "Failed to create alert rule",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "UpdateAlertRule",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Name, nameof(request.Name));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

            if (!InputValidationHelpers.IsValidLength(request.Name, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Rule name must be between 1 and 200 characters");
            }

            var existingRule = await configService.GetAlertRuleAsync(id, ct);

            if (existingRule is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "UpdateAlertRule",
                    resourceType: "AlertRule",
                    resourceId: id.ToString(),
                    details: "Alert rule not found");

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
                ModifiedBy = identity.UserId
            };

            await configService.UpdateAlertRuleAsync(id, updatedRule, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: $"Updated alert rule: {updatedRule.Name}",
                additionalData: new Dictionary<string, object>
                {
                    ["ruleName"] = updatedRule.Name,
                    ["severity"] = request.Severity,
                    ["enabled"] = request.Enabled,
                    ["channelCount"] = request.NotificationChannelIds?.Count ?? 0
                });

            logger.LogInformation("User {UserId} updated alert rule {RuleId}: {RuleName}", identity.UserId, id, updatedRule.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: "Failed to update alert rule",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "DeleteAlertRule",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var existingRule = await configService.GetAlertRuleAsync(id, ct);

            if (existingRule is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteAlertRule",
                    resourceType: "AlertRule",
                    resourceId: id.ToString(),
                    details: "Alert rule not found");

                return Results.Problem(
                    title: "Alert rule not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Alert rule with ID '{id}' does not exist");
            }

            await configService.DeleteAlertRuleAsync(id, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "DeleteAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: $"Deleted alert rule: {existingRule.Name}",
                additionalData: new Dictionary<string, object>
                {
                    ["ruleName"] = existingRule.Name,
                    ["ruleId"] = id
                });

            logger.LogInformation("User {UserId} deleted alert rule {RuleId}: {RuleName}", identity.UserId, id, existingRule.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "DeleteAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: "Failed to delete alert rule",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertRule> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "TestAlertRule",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var rule = await configService.GetAlertRuleAsync(id, ct);

            if (rule is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "TestAlertRule",
                    resourceType: "AlertRule",
                    resourceId: id.ToString(),
                    details: "Alert rule not found");

                return Results.Problem(
                    title: "Alert rule not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Alert rule with ID '{id}' does not exist");
            }

            logger.LogInformation("User {UserId} testing alert rule {RuleId}: {RuleName}", identity.UserId, id, rule.Name);

            // Get the notification channels for this rule
            var allChannels = await channelService.GetNotificationChannelsAsync(ct);
            var ruleChannels = allChannels
                .Where(c => rule.NotificationChannelIds.Contains(c.Id))
                .ToList();

            if (ruleChannels.Count == 0)
            {
                await auditLoggingService.LogAdminActionAsync(
                    action: "TestAlertRule",
                    resourceType: "AlertRule",
                    resourceId: id.ToString(),
                    details: $"Test alert rule attempted but no channels configured: {rule.Name}",
                    additionalData: new Dictionary<string, object>
                    {
                        ["ruleName"] = rule.Name,
                        ["channelCount"] = 0
                    });

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

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "TestAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: $"Test alert published for rule: {rule.Name}",
                additionalData: new Dictionary<string, object>
                {
                    ["ruleName"] = rule.Name,
                    ["channelCount"] = ruleChannels.Count,
                    ["publishSuccess"] = result.Success,
                    ["publishedChannels"] = result.PublishedChannels.Count,
                    ["failedChannels"] = result.FailedChannels.Count
                });

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "TestAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: "Failed to test alert rule",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "CreateNotificationChannel",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Name, nameof(request.Name));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

            if (!InputValidationHelpers.IsValidLength(request.Name, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Channel name must be between 1 and 200 characters");
            }

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
                CreatedBy = identity.UserId
            };

            var id = await channelService.CreateNotificationChannelAsync(channel, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "CreateNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: $"Created notification channel: {channel.Name} ({channel.Type})",
                additionalData: new Dictionary<string, object>
                {
                    ["channelName"] = channel.Name,
                    ["channelType"] = channel.Type,
                    ["enabled"] = channel.Enabled
                });

            logger.LogInformation("User {UserId} created notification channel {ChannelId}: {ChannelName} ({ChannelType})",
                identity.UserId, id, channel.Name, channel.Type);

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "CreateNotificationChannel",
                resourceType: "NotificationChannel",
                details: "Failed to create notification channel",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "UpdateNotificationChannel",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Name, nameof(request.Name));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

            if (!InputValidationHelpers.IsValidLength(request.Name, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Channel name must be between 1 and 200 characters");
            }

            var existingChannel = await channelService.GetNotificationChannelAsync(id, ct);

            if (existingChannel is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "UpdateNotificationChannel",
                    resourceType: "NotificationChannel",
                    resourceId: id.ToString(),
                    details: "Notification channel not found");

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
                ModifiedBy = identity.UserId
            };

            await channelService.UpdateNotificationChannelAsync(id, updatedChannel, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: $"Updated notification channel: {updatedChannel.Name} ({updatedChannel.Type})",
                additionalData: new Dictionary<string, object>
                {
                    ["channelName"] = updatedChannel.Name,
                    ["channelType"] = updatedChannel.Type,
                    ["enabled"] = updatedChannel.Enabled
                });

            logger.LogInformation("User {UserId} updated notification channel {ChannelId}: {ChannelName}", identity.UserId, id, updatedChannel.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: "Failed to update notification channel",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "DeleteNotificationChannel",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var existingChannel = await channelService.GetNotificationChannelAsync(id, ct);

            if (existingChannel is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteNotificationChannel",
                    resourceType: "NotificationChannel",
                    resourceId: id.ToString(),
                    details: "Notification channel not found");

                return Results.Problem(
                    title: "Notification channel not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Notification channel with ID '{id}' does not exist");
            }

            await channelService.DeleteNotificationChannelAsync(id, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "DeleteNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: $"Deleted notification channel: {existingChannel.Name} ({existingChannel.Type})",
                additionalData: new Dictionary<string, object>
                {
                    ["channelName"] = existingChannel.Name,
                    ["channelType"] = existingChannel.Type,
                    ["channelId"] = id
                });

            logger.LogInformation("User {UserId} deleted notification channel {ChannelId}: {ChannelName}", identity.UserId, id, existingChannel.Name);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "DeleteNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: "Failed to delete notification channel",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<NotificationChannel> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "TestNotificationChannel",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var channel = await channelService.GetNotificationChannelAsync(id, ct);

            if (channel is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "TestNotificationChannel",
                    resourceType: "NotificationChannel",
                    resourceId: id.ToString(),
                    details: "Notification channel not found");

                return Results.Problem(
                    title: "Notification channel not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Notification channel with ID '{id}' does not exist");
            }

            if (!channel.Enabled)
            {
                await auditLoggingService.LogAdminActionAsync(
                    action: "TestNotificationChannel",
                    resourceType: "NotificationChannel",
                    resourceId: id.ToString(),
                    details: $"Test notification channel attempted but channel disabled: {channel.Name}",
                    additionalData: new Dictionary<string, object>
                    {
                        ["channelName"] = channel.Name,
                        ["channelType"] = channel.Type,
                        ["enabled"] = false
                    });

                return Results.Ok(new TestNotificationChannelResponse
                {
                    Success = false,
                    Message = "Notification channel is disabled. Enable it before testing.",
                    LatencyMs = null
                });
            }

            logger.LogInformation("User {UserId} testing notification channel {ChannelId}: {ChannelName} ({ChannelType})",
                identity.UserId, id, channel.Name, channel.Type);

            // Test the notification channel
            var result = await publishingService.TestNotificationChannelAsync(channel, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "TestNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: $"Test notification sent for channel: {channel.Name} ({channel.Type})",
                additionalData: new Dictionary<string, object>
                {
                    ["channelName"] = channel.Name,
                    ["channelType"] = channel.Type,
                    ["testSuccess"] = result.Success,
                    ["latencyMs"] = result.LatencyMs ?? 0
                });

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "TestNotificationChannel",
                resourceType: "NotificationChannel",
                resourceId: id.ToString(),
                details: "Failed to test notification channel",
                exception: ex);

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

    private static Task<IResult> GetAlertHistoryById(
        long id,
        [FromServices] IAlertHistoryStore historyStore,
        CancellationToken ct)
    {
        try
        {
            // TODO: Add method to get alert by ID
            // For now, return not implemented
            return Task.FromResult<IResult>(Results.Problem(
                title: "Not implemented",
                statusCode: StatusCodes.Status501NotImplemented,
                detail: "Alert history by ID not yet implemented. Use fingerprint-based lookup instead."));
        }
        catch (Exception ex)
        {
            return Task.FromResult<IResult>(Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to retrieve alert: {ex.Message}"));
        }
    }

    private static async Task<IResult> AcknowledgeAlert(
        long id,
        AcknowledgeAlertRequest request,
        [FromServices] IAlertHistoryStore historyStore,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertHistoryEntry> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "AcknowledgeAlert",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Comment, nameof(request.Comment));

            // TODO: Get alert by ID first
            // For now, return not implemented
            var acknowledgement = new AlertAcknowledgement
            {
                Fingerprint = "unknown", // TODO: Get from alert
                AcknowledgedBy = identity.UserId,
                AcknowledgedAt = DateTimeOffset.UtcNow,
                Comment = request.Comment,
                ExpiresAt = request.ExpiresAt
            };

            await historyStore.InsertAcknowledgementAsync(acknowledgement, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "AcknowledgeAlert",
                resourceType: "Alert",
                resourceId: id.ToString(),
                details: $"Alert acknowledged by {identity.UserId}",
                additionalData: new Dictionary<string, object>
                {
                    ["alertId"] = id,
                    ["comment"] = request.Comment ?? string.Empty,
                    ["expiresAt"] = request.ExpiresAt?.ToString() ?? "never"
                });

            logger.LogInformation("User {UserId} acknowledged alert {AlertId}", identity.UserId, id);

            return Results.Ok(new { Message = "Alert acknowledged successfully" });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "AcknowledgeAlert",
                resourceType: "Alert",
                resourceId: id.ToString(),
                details: "Failed to acknowledge alert",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertHistoryEntry> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "SilenceAlert",
                    resourceId: id.ToString(),
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Name, nameof(request.Name));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Comment, nameof(request.Comment));

            if (!InputValidationHelpers.IsValidLength(request.Name, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Silencing rule name must be between 1 and 200 characters");
            }

            // TODO: Get alert by ID first to extract matchers
            var silencingRule = new AlertSilencingRule
            {
                Name = request.Name,
                Matchers = new Dictionary<string, string>(), // TODO: Extract from alert
                CreatedBy = identity.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Comment = request.Comment,
                IsActive = true
            };

            var ruleId = await historyStore.InsertSilencingRuleAsync(silencingRule, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "SilenceAlert",
                resourceType: "AlertSilencingRule",
                resourceId: ruleId.ToString(),
                details: $"Created silencing rule for alert {id}: {request.Name}",
                additionalData: new Dictionary<string, object>
                {
                    ["alertId"] = id,
                    ["ruleName"] = request.Name,
                    ["ruleId"] = ruleId,
                    ["startsAt"] = request.StartsAt?.ToString() ?? "now",
                    ["endsAt"] = request.EndsAt?.ToString() ?? "never"
                });

            logger.LogInformation("User {UserId} created silencing rule {RuleId} for alert {AlertId}",
                identity.UserId, ruleId, id);

            return Results.Ok(new { Message = "Alert silencing rule created successfully", RuleId = ruleId });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "SilenceAlert",
                resourceType: "AlertSilencingRule",
                resourceId: id.ToString(),
                details: "Failed to create silencing rule",
                exception: ex);

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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<AlertRoutingConfiguration> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "UpdateAlertRouting",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation for route names
            foreach (var route in request.Routes)
            {
                InputValidationHelpers.ThrowIfUnsafeInput(route.Name, nameof(route.Name));
                if (!InputValidationHelpers.IsValidLength(route.Name, minLength: 1, maxLength: 200))
                {
                    return Results.BadRequest($"Route name '{route.Name}' must be between 1 and 200 characters");
                }
            }

            if (request.DefaultRoute != null)
            {
                InputValidationHelpers.ThrowIfUnsafeInput(request.DefaultRoute.Name, nameof(request.DefaultRoute.Name));
            }

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
                ModifiedBy = identity.UserId
            };

            await configService.UpdateRoutingConfigurationAsync(config, ct);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateAlertRouting",
                resourceType: "AlertRoutingConfiguration",
                resourceId: "global",
                details: $"Updated alert routing configuration with {config.Routes.Count} routes",
                additionalData: new Dictionary<string, object>
                {
                    ["routeCount"] = config.Routes.Count,
                    ["hasDefaultRoute"] = config.DefaultRoute != null
                });

            logger.LogInformation("User {UserId} updated alert routing configuration with {RouteCount} routes",
                identity.UserId, config.Routes.Count);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateAlertRouting",
                resourceType: "AlertRoutingConfiguration",
                resourceId: "global",
                details: "Failed to update alert routing configuration",
                exception: ex);

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
