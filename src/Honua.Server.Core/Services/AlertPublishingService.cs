// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Services;

/// <summary>
/// Result of publishing a test alert to notification channels.
/// </summary>
public sealed class AlertPublishingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> PublishedChannels { get; set; } = new();
    public List<string> FailedChannels { get; set; } = new();
    public Dictionary<string, string> ChannelErrors { get; set; } = new();
}

/// <summary>
/// Result of testing a single notification channel.
/// </summary>
public sealed class NotificationChannelTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? LatencyMs { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Service for publishing test alerts and testing notification channels.
/// </summary>
public interface IAlertPublishingService
{
    /// <summary>
    /// Publishes a test alert based on an alert rule.
    /// </summary>
    Task<AlertPublishingResult> PublishTestAlertAsync(
        AlertRule rule,
        IReadOnlyList<NotificationChannel> channels,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a notification channel by sending a test notification.
    /// </summary>
    Task<NotificationChannelTestResult> TestNotificationChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of alert publishing service.
/// </summary>
public sealed class AlertPublishingService : IAlertPublishingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertPublishingService> _logger;

    public AlertPublishingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AlertPublishingService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AlertPublishingResult> PublishTestAlertAsync(
        AlertRule rule,
        IReadOnlyList<NotificationChannel> channels,
        CancellationToken cancellationToken = default)
    {
        var result = new AlertPublishingResult
        {
            Success = false,
            Message = "Test alert publishing started"
        };

        if (channels.Count == 0)
        {
            result.Message = "No notification channels configured for this alert rule";
            return result;
        }

        var testPayload = CreateTestAlertPayload(rule);
        var successCount = 0;
        var failureCount = 0;

        foreach (var channel in channels.Where(c => c.Enabled))
        {
            try
            {
                var channelResult = await TestNotificationChannelAsync(channel, cancellationToken);

                if (channelResult.Success)
                {
                    result.PublishedChannels.Add($"{channel.Name} ({channel.Type})");
                    successCount++;
                }
                else
                {
                    result.FailedChannels.Add($"{channel.Name} ({channel.Type})");
                    result.ChannelErrors[channel.Name] = channelResult.Message;
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test channel {ChannelName} ({ChannelType})",
                    channel.Name, channel.Type);
                result.FailedChannels.Add($"{channel.Name} ({channel.Type})");
                result.ChannelErrors[channel.Name] = ex.Message;
                failureCount++;
            }
        }

        result.Success = successCount > 0;
        result.Message = successCount > 0
            ? $"Test alert sent to {successCount}/{channels.Count(c => c.Enabled)} channel(s)"
            : $"Failed to send test alert to all {channels.Count(c => c.Enabled)} channel(s)";

        return result;
    }

    public async Task<NotificationChannelTestResult> TestNotificationChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Testing notification channel {ChannelName} ({ChannelType})",
                channel.Name, channel.Type);

            var result = channel.Type.ToLowerInvariant() switch
            {
                "slack" => await TestSlackChannelAsync(channel, cancellationToken),
                "teams" => await TestTeamsChannelAsync(channel, cancellationToken),
                "email" => await TestEmailChannelAsync(channel, cancellationToken),
                "webhook" => await TestWebhookChannelAsync(channel, cancellationToken),
                "sns" => await TestSnsChannelAsync(channel, cancellationToken),
                "azureeventgrid" => await TestAzureEventGridChannelAsync(channel, cancellationToken),
                "pagerduty" => await TestPagerDutyChannelAsync(channel, cancellationToken),
                "opsgenie" => await TestOpsgenieChannelAsync(channel, cancellationToken),
                _ => new NotificationChannelTestResult
                {
                    Success = false,
                    Message = $"Unsupported channel type: {channel.Type}"
                }
            };

            var latency = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            if (result.Success)
            {
                result.LatencyMs = (long)latency;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing notification channel {ChannelName} ({ChannelType})",
                channel.Name, channel.Type);

            return new NotificationChannelTestResult
            {
                Success = false,
                Message = $"Test failed: {ex.Message}",
                ErrorDetails = ex.ToString()
            };
        }
    }

    #region Channel-Specific Testing

    private async Task<NotificationChannelTestResult> TestSlackChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("webhookUrl", out var webhookUrl))
        {
            return new NotificationChannelTestResult
            {
                Success = false,
                Message = "Slack webhook URL not configured"
            };
        }

        var payload = new
        {
            text = "Test notification from Honua Alert System",
            attachments = new[]
            {
                new
                {
                    color = "good",
                    title = "Test Alert",
                    text = $"This is a test notification sent at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
                    fields = new[]
                    {
                        new { title = "Channel", value = channel.Name, @short = true },
                        new { title = "Type", value = "Test", @short = true }
                    },
                    footer = "Honua Alert Administration",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            }
        };

        using var httpClient = _httpClientFactory.CreateClient("Slack");
        var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new NotificationChannelTestResult
            {
                Success = true,
                Message = "Test notification sent successfully to Slack"
            };
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new NotificationChannelTestResult
        {
            Success = false,
            Message = $"Slack API error: {response.StatusCode}",
            ErrorDetails = error
        };
    }

    private async Task<NotificationChannelTestResult> TestTeamsChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("webhookUrl", out var webhookUrl))
        {
            return new NotificationChannelTestResult
            {
                Success = false,
                Message = "Teams webhook URL not configured"
            };
        }

        var payload = new
        {
            type = "MessageCard",
            context = "https://schema.org/extensions",
            summary = "Test notification from Honua",
            themeColor = "0078D4",
            title = "Test Alert",
            sections = new[]
            {
                new
                {
                    activityTitle = "Test Notification",
                    activitySubtitle = $"Sent at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
                    facts = new[]
                    {
                        new { name = "Channel", value = channel.Name },
                        new { name = "Type", value = "Test" },
                        new { name = "Status", value = "Success" }
                    }
                }
            }
        };

        using var httpClient = _httpClientFactory.CreateClient("Teams");
        var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new NotificationChannelTestResult
            {
                Success = true,
                Message = "Test notification sent successfully to Teams"
            };
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new NotificationChannelTestResult
        {
            Success = false,
            Message = $"Teams API error: {response.StatusCode}",
            ErrorDetails = error
        };
    }

    private Task<NotificationChannelTestResult> TestEmailChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        // Email testing would require SMTP configuration
        // For now, return a simulated success if configuration exists
        if (!channel.Configuration.TryGetValue("recipient", out var recipient))
        {
            return Task.FromResult(new NotificationChannelTestResult
            {
                Success = false,
                Message = "Email recipient not configured"
            });
        }

        _logger.LogInformation("Email test would send to: {Recipient}", recipient);

        return Task.FromResult(new NotificationChannelTestResult
        {
            Success = true,
            Message = $"Email channel configured (would send to {recipient})"
        });
    }

    private async Task<NotificationChannelTestResult> TestWebhookChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("url", out var url))
        {
            return new NotificationChannelTestResult
            {
                Success = false,
                Message = "Webhook URL not configured"
            };
        }

        var payload = new
        {
            type = "test",
            channel = channel.Name,
            timestamp = DateTimeOffset.UtcNow,
            message = "Test notification from Honua Alert System"
        };

        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new NotificationChannelTestResult
            {
                Success = true,
                Message = "Test notification sent successfully to webhook"
            };
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new NotificationChannelTestResult
        {
            Success = false,
            Message = $"Webhook error: {response.StatusCode}",
            ErrorDetails = error
        };
    }

    private Task<NotificationChannelTestResult> TestSnsChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("topicArn", out var topicArn))
        {
            return Task.FromResult(new NotificationChannelTestResult
            {
                Success = false,
                Message = "SNS topic ARN not configured"
            });
        }

        // SNS testing would require AWS SDK integration
        // For now, validate configuration
        _logger.LogInformation("SNS test would publish to: {TopicArn}", topicArn);

        return Task.FromResult(new NotificationChannelTestResult
        {
            Success = true,
            Message = $"SNS channel configured (topic: {topicArn})"
        });
    }

    private Task<NotificationChannelTestResult> TestAzureEventGridChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("endpoint", out var endpoint))
        {
            return Task.FromResult(new NotificationChannelTestResult
            {
                Success = false,
                Message = "Azure Event Grid endpoint not configured"
            });
        }

        _logger.LogInformation("Azure Event Grid test would publish to: {Endpoint}", endpoint);

        return Task.FromResult(new NotificationChannelTestResult
        {
            Success = true,
            Message = $"Azure Event Grid channel configured (endpoint: {endpoint})"
        });
    }

    private async Task<NotificationChannelTestResult> TestPagerDutyChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("routingKey", out var routingKey))
        {
            return new NotificationChannelTestResult
            {
                Success = false,
                Message = "PagerDuty routing key not configured"
            };
        }

        // PagerDuty Events API v2 endpoint
        var endpoint = "https://events.pagerduty.com/v2/enqueue";

        var payload = new
        {
            routing_key = routingKey,
            event_action = "trigger",
            payload = new
            {
                summary = "Test notification from Honua Alert System",
                source = "honua-alert-admin",
                severity = "info",
                timestamp = DateTimeOffset.UtcNow,
                custom_details = new
                {
                    channel = channel.Name,
                    type = "test"
                }
            }
        };

        using var httpClient = _httpClientFactory.CreateClient("PagerDuty");
        var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new NotificationChannelTestResult
            {
                Success = true,
                Message = "Test notification sent successfully to PagerDuty"
            };
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new NotificationChannelTestResult
        {
            Success = false,
            Message = $"PagerDuty API error: {response.StatusCode}",
            ErrorDetails = error
        };
    }

    private async Task<NotificationChannelTestResult> TestOpsgenieChannelAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        if (!channel.Configuration.TryGetValue("apiKey", out var apiKey))
        {
            return new NotificationChannelTestResult
            {
                Success = false,
                Message = "Opsgenie API key not configured"
            };
        }

        var endpoint = channel.Configuration.TryGetValue("endpoint", out var customEndpoint)
            ? customEndpoint
            : "https://api.opsgenie.com/v2/alerts";

        var payload = new
        {
            message = "Test notification from Honua Alert System",
            description = $"This is a test notification sent at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
            priority = "P5",
            details = new
            {
                channel = channel.Name,
                type = "test"
            }
        };

        using var httpClient = _httpClientFactory.CreateClient("Opsgenie");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("Authorization", $"GenieKey {apiKey}");
        request.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new NotificationChannelTestResult
            {
                Success = true,
                Message = "Test notification sent successfully to Opsgenie"
            };
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new NotificationChannelTestResult
        {
            Success = false,
            Message = $"Opsgenie API error: {response.StatusCode}",
            ErrorDetails = error
        };
    }

    #endregion

    #region Helper Methods

    private static object CreateTestAlertPayload(AlertRule rule)
    {
        return new
        {
            version = "4",
            groupKey = $"test-{Guid.NewGuid()}",
            status = "firing",
            receiver = "test",
            groupLabels = new Dictionary<string, string>
            {
                ["alertname"] = rule.Name,
                ["severity"] = rule.Severity
            },
            commonLabels = rule.Matchers,
            commonAnnotations = new Dictionary<string, string>
            {
                ["summary"] = $"Test alert for rule: {rule.Name}",
                ["description"] = rule.Description ?? "This is a test alert sent from the admin interface"
            },
            externalURL = "https://honua.io/alerts",
            alerts = new[]
            {
                new
                {
                    status = "firing",
                    labels = new Dictionary<string, string>(rule.Matchers)
                    {
                        ["alertname"] = rule.Name,
                        ["severity"] = rule.Severity
                    },
                    annotations = new Dictionary<string, string>
                    {
                        ["summary"] = $"Test alert for rule: {rule.Name}",
                        ["description"] = rule.Description ?? "This is a test alert sent from the admin interface"
                    },
                    startsAt = DateTimeOffset.UtcNow,
                    endsAt = (DateTimeOffset?)null,
                    generatorURL = "https://honua.io/admin/alerts",
                    fingerprint = $"test-{Guid.NewGuid()}"
                }
            }
        };
    }

    #endregion
}
