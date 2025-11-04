// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Notifications;

/// <summary>
/// Configuration options for the notification system.
/// </summary>
public class NotificationOptions
{
    /// <summary>
    /// Whether notifications are enabled globally
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Slack notification configuration
    /// </summary>
    public SlackOptions Slack { get; set; } = new();

    /// <summary>
    /// Email notification configuration
    /// </summary>
    public EmailOptions Email { get; set; } = new();

    /// <summary>
    /// Path to the notification templates directory
    /// If not specified, uses embedded templates
    /// </summary>
    public string? TemplatesDirectory { get; set; }

    /// <summary>
    /// Whether to enable specific notification types
    /// </summary>
    public NotificationTypeSettings Types { get; set; } = new();

    /// <summary>
    /// Retry configuration for failed notifications
    /// </summary>
    public RetryOptions Retry { get; set; } = new();
}

/// <summary>
/// Configuration for Slack notifications
/// </summary>
public class SlackOptions
{
    /// <summary>
    /// Whether Slack notifications are enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Slack webhook URL for sending messages
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Default channel for notifications (optional, webhook URL determines channel)
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>
    /// Bot name to display in Slack (optional)
    /// </summary>
    public string? BotName { get; set; } = "Honua GitOps";

    /// <summary>
    /// Bot icon emoji (optional)
    /// </summary>
    public string? BotIcon { get; set; } = ":rocket:";

    /// <summary>
    /// HTTP timeout for Slack webhook calls in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Configuration for Email notifications
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Whether Email notifications are enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// SMTP server hostname
    /// </summary>
    public string? SmtpServer { get; set; }

    /// <summary>
    /// SMTP server port (typically 587 for TLS, 465 for SSL, 25 for unencrypted)
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS for SMTP connection
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// SMTP username (if authentication required)
    /// </summary>
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// SMTP password (if authentication required)
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>
    /// From address for notification emails
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>
    /// From display name (optional)
    /// </summary>
    public string? FromName { get; set; } = "Honua GitOps";

    /// <summary>
    /// Default recipient email addresses
    /// </summary>
    public List<string> ToAddresses { get; set; } = new();

    /// <summary>
    /// Environment-specific recipient email addresses
    /// Key is environment name (production, staging, etc.), value is list of email addresses
    /// </summary>
    public Dictionary<string, List<string>> PerEnvironment { get; set; } = new();

    /// <summary>
    /// HTTP timeout for email sending in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Configuration for which notification types are enabled
/// </summary>
public class NotificationTypeSettings
{
    /// <summary>
    /// Notify when deployment is created
    /// </summary>
    public bool DeploymentCreated { get; set; } = true;

    /// <summary>
    /// Notify when approval is required
    /// </summary>
    public bool ApprovalRequired { get; set; } = true;

    /// <summary>
    /// Notify when deployment is approved
    /// </summary>
    public bool Approved { get; set; } = true;

    /// <summary>
    /// Notify when deployment is rejected
    /// </summary>
    public bool Rejected { get; set; } = true;

    /// <summary>
    /// Notify when deployment starts
    /// </summary>
    public bool DeploymentStarted { get; set; } = true;

    /// <summary>
    /// Notify when deployment completes successfully
    /// </summary>
    public bool DeploymentCompleted { get; set; } = true;

    /// <summary>
    /// Notify when deployment fails
    /// </summary>
    public bool DeploymentFailed { get; set; } = true;

    /// <summary>
    /// Notify when rollback occurs
    /// </summary>
    public bool Rollback { get; set; } = true;
}

/// <summary>
/// Retry configuration for failed notifications
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before first retry
    /// </summary>
    public int InitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds between retries
    /// </summary>
    public int MaxDelayMs { get; set; } = 10000;

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
}
