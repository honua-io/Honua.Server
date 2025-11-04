// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;
using Honua.Server.AlertReceiver.Extensions;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Adapts generic alerts to AlertManager webhook format for publisher compatibility.
/// </summary>
public static class GenericAlertAdapter
{
    public static AlertManagerWebhook ToAlertManagerWebhook(GenericAlert alert)
    {
        var alerts = new List<GenericAlert> { alert };
        return ToAlertManagerWebhook(alerts);
    }

    public static AlertManagerWebhook ToAlertManagerWebhook(List<GenericAlert> alerts)
    {
        var webhook = new AlertManagerWebhook
        {
            Version = "4",
            Status = alerts.Any(a => a.Status == "firing") ? "firing" : "resolved",
            Receiver = "generic-alerts",
            GroupKey = alerts.FirstOrDefault()?.Name ?? "generic",
            GroupLabels = new Dictionary<string, string>
            {
                ["alertname"] = alerts.FirstOrDefault()?.Name ?? "GenericAlert"
            },
            CommonLabels = new Dictionary<string, string>
            {
                ["source"] = alerts.FirstOrDefault()?.Source ?? "unknown"
            },
            CommonAnnotations = new Dictionary<string, string>(),
            ExternalUrl = string.Empty,
            Alerts = new List<Alert>()
        };

        foreach (var genericAlert in alerts)
        {
            var labels = new Dictionary<string, string>(genericAlert.Labels)
            {
                ["alertname"] = genericAlert.Name,
                ["severity"] = MapSeverity(genericAlert.Severity),
                ["source"] = genericAlert.Source
            };

            if (genericAlert.Service.HasValue())
            {
                labels["service_id"] = genericAlert.Service;
            }

            if (genericAlert.Environment.HasValue())
            {
                labels["environment"] = genericAlert.Environment;
            }

            var annotations = new Dictionary<string, string>
            {
                ["description"] = genericAlert.Description ?? genericAlert.Summary ?? "No description"
            };

            if (genericAlert.Summary.HasValue())
            {
                annotations["summary"] = genericAlert.Summary;
            }

            // Add context as annotations
            if (genericAlert.Context != null)
            {
                foreach (var kvp in genericAlert.Context)
                {
                    annotations[$"context_{kvp.Key}"] = kvp.Value?.ToString() ?? "";
                }
            }

            webhook.Alerts.Add(new Alert
            {
                Status = genericAlert.Status,
                Labels = labels,
                Annotations = annotations,
                StartsAt = genericAlert.Timestamp,
                EndsAt = genericAlert.Status == "resolved" ? DateTime.UtcNow : null,
                GeneratorUrl = string.Empty,
                Fingerprint = genericAlert.Fingerprint ?? GenerateFingerprint(genericAlert)
            });
        }

        return webhook;
    }

    private static string MapSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" or "crit" or "fatal" => "critical",
            "high" or "error" or "err" => "high",
            "medium" or "warning" or "warn" => "warning",
            "low" or "info" or "information" => "info",
            _ => "warning"
        };
    }

    private static string GenerateFingerprint(GenericAlert alert)
    {
        var key = $"{alert.Source}:{alert.Name}:{alert.Service ?? "default"}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
