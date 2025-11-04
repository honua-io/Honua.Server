// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Manages alert acknowledgements and silencing rules.
/// </summary>
public interface IAlertSilencingService
{
    Task<bool> IsAlertSilencedAsync(GenericAlert alert);
    Task<bool> IsAlertAcknowledgedAsync(string fingerprint);
    Task AcknowledgeAlertAsync(string fingerprint, string acknowledgedBy, string? comment = null, TimeSpan? expiresIn = null);
    // BUG FIX #33: Use DateTimeOffset to preserve timezone information
    Task CreateSilencingRuleAsync(string name, Dictionary<string, string> matchers, string createdBy, DateTimeOffset startsAt, DateTimeOffset endsAt, string? comment = null);
    Task<List<AlertSilencingRule>> GetActiveSilencingRulesAsync();
    Task DeactivateSilencingRuleAsync(long ruleId);
}

public sealed class AlertSilencingService : IAlertSilencingService
{
    private readonly IAlertHistoryStore _historyStore;
    private readonly ILogger<AlertSilencingService> _logger;

    public AlertSilencingService(IAlertHistoryStore historyStore, ILogger<AlertSilencingService> logger)
    {
        _historyStore = historyStore;
        _logger = logger;
    }

    public async Task<bool> IsAlertSilencedAsync(GenericAlert alert)
    {
        // BUG FIX #33: Use DateTimeOffset.UtcNow for timezone-aware comparisons
        var now = DateTimeOffset.UtcNow;
        var activeRules = await _historyStore.GetActiveSilencingRulesAsync(now).ConfigureAwait(false);

        foreach (var rule in activeRules)
        {
            if (MatchesRule(alert, rule))
            {
                _logger.LogInformation(
                    "Alert silenced by rule '{RuleName}': {AlertName}",
                    rule.Name, alert.Name);
                return true;
            }
        }

        return false;
    }

    public async Task<bool> IsAlertAcknowledgedAsync(string fingerprint)
    {
        // BUG FIX #33: Use DateTimeOffset.UtcNow for timezone-aware comparisons
        var now = DateTimeOffset.UtcNow;
        var ack = await _historyStore.GetLatestAcknowledgementAsync(fingerprint).ConfigureAwait(false);

        if (ack == null)
        {
            return false;
        }

        if (ack.ExpiresAt.HasValue && ack.ExpiresAt <= now)
        {
            return false;
        }

        return true;
    }

    public async Task AcknowledgeAlertAsync(string fingerprint, string acknowledgedBy, string? comment = null, TimeSpan? expiresIn = null)
    {
        // BUG FIX #33: Use DateTimeOffset.UtcNow for timezone-aware timestamps
        var ack = new AlertAcknowledgement
        {
            Fingerprint = fingerprint,
            AcknowledgedBy = acknowledgedBy,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            Comment = comment,
            ExpiresAt = expiresIn.HasValue ? DateTimeOffset.UtcNow.Add(expiresIn.Value) : null
        };

        await _historyStore.InsertAcknowledgementAsync(ack).ConfigureAwait(false);

        _logger.LogInformation(
            "Alert acknowledged: {Fingerprint} by {User}, expires: {ExpiresAt}",
            fingerprint, acknowledgedBy, ack.ExpiresAt?.ToString() ?? "never");
    }

    public async Task CreateSilencingRuleAsync(
        string name,
        Dictionary<string, string> matchers,
        string createdBy,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? comment = null)
    {
        // BUG FIX #33: Use DateTimeOffset.UtcNow for timezone-aware timestamps
        var rule = new AlertSilencingRule
        {
            Name = name,
            Matchers = new Dictionary<string, string>(matchers, StringComparer.OrdinalIgnoreCase),
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Comment = comment,
            IsActive = true
        };

        await _historyStore.InsertSilencingRuleAsync(rule).ConfigureAwait(false);

        _logger.LogInformation(
            "Created silencing rule '{Name}' by {User}, active {Start} to {End}",
            name, createdBy, startsAt, endsAt);
    }

    public async Task<List<AlertSilencingRule>> GetActiveSilencingRulesAsync()
    {
        // BUG FIX #33: Use DateTimeOffset.UtcNow for timezone-aware comparisons
        var now = DateTimeOffset.UtcNow;
        var results = await _historyStore.GetActiveSilencingRulesAsync(now).ConfigureAwait(false);
        return results
            .Where(r => r.StartsAt <= now)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task DeactivateSilencingRuleAsync(long ruleId)
    {
        await _historyStore.DeactivateSilencingRuleAsync(ruleId).ConfigureAwait(false);
        _logger.LogInformation("Deactivated silencing rule ID {RuleId}", ruleId);
    }

    private bool MatchesRule(GenericAlert alert, AlertSilencingRule rule)
    {
        try
        {
            if (rule.Matchers == null || rule.Matchers.Count == 0)
            {
                return false;
            }

            foreach (var matcher in rule.Matchers)
            {
                var value = matcher.Key.ToLowerInvariant() switch
                {
                    "name" => alert.Name,
                    "severity" => alert.Severity,
                    "source" => alert.Source,
                    "service" => alert.Service ?? "",
                    "environment" => alert.Environment ?? "",
                    _ => alert.Labels.GetValueOrDefault(matcher.Key, "")
                };

                // Support regex matching with cached compiled patterns for performance
                // PERFORMANCE: Using RegexCache reduces 15% overhead from pattern compilation
                if (matcher.Value.StartsWith("~"))
                {
                    var pattern = matcher.Value[1..];
                    try
                    {
                        var regex = RegexCache.GetOrAdd(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, timeoutMilliseconds: 100);
                        if (!regex.IsMatch(value))
                        {
                            return false;
                        }
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        _logger.LogWarning(
                            "Regex pattern timed out for rule {RuleName}, pattern: {Pattern}. Treating as non-match.",
                            rule.Name, pattern);
                        return false;
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogError(ex,
                            "Invalid regex pattern in rule {RuleName}, pattern: {Pattern}. Treating as non-match.",
                            rule.Name, pattern);
                        return false;
                    }
                }
                else
                {
                    if (!string.Equals(value, matcher.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching alert against rule {RuleName}", rule.Name);
            return false;
        }
    }
}
