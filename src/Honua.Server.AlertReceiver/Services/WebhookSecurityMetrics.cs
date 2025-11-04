// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
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
    private readonly Meter _meter;
    private readonly Counter<long> _validationAttempts;
    private readonly Counter<long> _validationFailures;
    private readonly Counter<long> _methodRejections;
    private readonly Counter<long> _timestampFailures;
    private readonly Counter<long> _httpsViolations;
    private readonly Histogram<int> _activeSecrets;

    public WebhookSecurityMetrics()
    {
        _meter = new Meter("Honua.AlertReceiver.Security", "1.0.0");

        _validationAttempts = _meter.CreateCounter<long>(
            "honua.webhook.validation_attempts",
            unit: "{attempt}",
            description: "Number of webhook signature validation attempts by HTTP method");

        _validationFailures = _meter.CreateCounter<long>(
            "honua.webhook.validation_failures",
            unit: "{failure}",
            description: "Number of failed webhook signature validations by HTTP method and reason");

        _methodRejections = _meter.CreateCounter<long>(
            "honua.webhook.method_rejections",
            unit: "{rejection}",
            description: "Number of rejected HTTP methods for webhook endpoints");

        _timestampFailures = _meter.CreateCounter<long>(
            "honua.webhook.timestamp_failures",
            unit: "{failure}",
            description: "Number of timestamp validation failures (replay attack protection)");

        _httpsViolations = _meter.CreateCounter<long>(
            "honua.webhook.https_violations",
            unit: "{violation}",
            description: "Number of attempts to use HTTP instead of HTTPS");

        _activeSecrets = _meter.CreateHistogram<int>(
            "honua.webhook.active_secrets",
            unit: "{secret}",
            description: "Number of active secrets configured for webhook validation");
    }

    public void RecordValidationAttempt(string method, bool success)
    {
        _validationAttempts.Add(1,
            new("method", method),
            new("success", success.ToString().ToLowerInvariant()));

        if (!success)
        {
            _validationFailures.Add(1,
                new("method", method),
                new("reason", "invalid_signature"));
        }
    }

    public void RecordMethodRejection(string method, string reason)
    {
        _methodRejections.Add(1,
            new("method", method),
            new("reason", reason));
    }

    public void RecordTimestampValidationFailure(string reason)
    {
        _timestampFailures.Add(1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordHttpsViolation()
    {
        _httpsViolations.Add(1);
    }

    public void RecordSecretRotation(int activeSecrets)
    {
        _activeSecrets.Record(activeSecrets);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
