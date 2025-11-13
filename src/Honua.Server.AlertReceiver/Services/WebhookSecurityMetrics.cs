// <copyright file="WebhookSecurityMetrics.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics.Metrics;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Metrics for webhook security validation and authentication.
/// Tracks signature validation failures, method rejections, and security events.
/// </summary>
public interface IWebhookSecurityMetrics
{
    void RecordValidationAttempt(string method, bool success);

    void RecordMethodRejection(string method, string reason);

    void RecordTimestampValidationFailure(string reason);

    void RecordHttpsViolation();

    void RecordSecretRotation(int activeSecrets);
}

public sealed class WebhookSecurityMetrics : IWebhookSecurityMetrics, IDisposable
{
    private readonly Meter meter;
    private readonly Counter<long> validationAttempts;
    private readonly Counter<long> validationFailures;
    private readonly Counter<long> methodRejections;
    private readonly Counter<long> timestampFailures;
    private readonly Counter<long> httpsViolations;
    private readonly Histogram<int> activeSecrets;

    public WebhookSecurityMetrics()
    {
        this.meter = new Meter("Honua.AlertReceiver.Security", "1.0.0");

        this.validationAttempts = this.meter.CreateCounter<long>(
            "honua.webhook.validation_attempts",
            unit: "{attempt}",
            description: "Number of webhook signature validation attempts by HTTP method");

        this.validationFailures = this.meter.CreateCounter<long>(
            "honua.webhook.validation_failures",
            unit: "{failure}",
            description: "Number of failed webhook signature validations by HTTP method and reason");

        this.methodRejections = this.meter.CreateCounter<long>(
            "honua.webhook.method_rejections",
            unit: "{rejection}",
            description: "Number of rejected HTTP methods for webhook endpoints");

        this.timestampFailures = this.meter.CreateCounter<long>(
            "honua.webhook.timestamp_failures",
            unit: "{failure}",
            description: "Number of timestamp validation failures (replay attack protection)");

        this.httpsViolations = this.meter.CreateCounter<long>(
            "honua.webhook.https_violations",
            unit: "{violation}",
            description: "Number of attempts to use HTTP instead of HTTPS");

        this.activeSecrets = this.meter.CreateHistogram<int>(
            "honua.webhook.active_secrets",
            unit: "{secret}",
            description: "Number of active secrets configured for webhook validation");
    }

    public void RecordValidationAttempt(string method, bool success)
    {
        this.validationAttempts.Add(
            1,
            new("method", method),
            new("success", success.ToString().ToLowerInvariant()));

        if (!success)
        {
            this.validationFailures.Add(
                1,
                new("method", method),
                new("reason", "invalid_signature"));
        }
    }

    public void RecordMethodRejection(string method, string reason)
    {
        this.methodRejections.Add(
            1,
            new("method", method),
            new("reason", reason));
    }

    public void RecordTimestampValidationFailure(string reason)
    {
        this.timestampFailures.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordHttpsViolation()
    {
        this.httpsViolations.Add(1);
    }

    public void RecordSecretRotation(int activeSecrets)
    {
        this.activeSecrets.Record(activeSecrets);
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
