// <copyright file="SqlAlertDeduplicator.Helpers.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Helper methods partial class for SqlAlertDeduplicator.
/// Contains utility functions for key generation, serialization, configuration, and cryptographic operations.
/// </summary>
public sealed partial class SqlAlertDeduplicator
{
    /// <summary>
    /// Builds a unique state identifier from fingerprint and severity.
    /// Format: "fingerprint:severity"
    /// </summary>
    private static string BuildKey(string fingerprint, string severity) => $"{fingerprint}:{severity}";

    /// <summary>
    /// Gets the deduplication time window for a given severity level.
    /// Configurable via appsettings.json under Alerts:Deduplication section.
    /// </summary>
    private TimeSpan GetDeduplicationWindow(string severity)
    {
        var minutes = severity.ToLowerInvariant() switch
        {
            "critical" => this.configuration.GetValue("Alerts:Deduplication:CriticalWindowMinutes", 5),
            "high" => this.configuration.GetValue("Alerts:Deduplication:HighWindowMinutes", 10),
            "medium" or "warning" => this.configuration.GetValue("Alerts:Deduplication:WarningWindowMinutes", 15),
            _ => this.configuration.GetValue("Alerts:Deduplication:DefaultWindowMinutes", 30),
        };

        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Gets the rate limit (alerts per hour) for a given severity level.
    /// Configurable via appsettings.json under Alerts:RateLimit section.
    /// </summary>
    private int GetRateLimit(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => this.configuration.GetValue("Alerts:RateLimit:CriticalPerHour", 20),
            "high" => this.configuration.GetValue("Alerts:RateLimit:HighPerHour", 10),
            "medium" or "warning" => this.configuration.GetValue("Alerts:RateLimit:WarningPerHour", 5),
            _ => this.configuration.GetValue("Alerts:RateLimit:DefaultPerHour", 3),
        };
    }

    /// <summary>
    /// Deserializes sent timestamps from JSON string.
    /// Returns empty list if JSON is invalid or empty.
    /// </summary>
    private static List<DateTimeOffset> DeserializeTimestamps(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new();
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<DateTimeOffset>>(json);
            return values ?? new List<DateTimeOffset>();
        }
        catch
        {
            return new List<DateTimeOffset>();
        }
    }

    /// <summary>
    /// Serializes sent timestamps to JSON string.
    /// </summary>
    private static string SerializeTimestamps(List<DateTimeOffset> timestamps)
        => JsonSerializer.Serialize(timestamps);

    /// <summary>
    /// RACE CONDITION FIX: Generate unique reservation ID.
    /// Format: "rsv_" + GUID without hyphens
    /// </summary>
    private static string GenerateReservationId()
    {
        return $"rsv_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// RACE CONDITION FIX: Compute 64-bit lock key for PostgreSQL advisory lock.
    /// Uses SHA256 hash of the state ID to generate a deterministic lock key.
    /// Same state ID will always produce the same lock key, ensuring serialization.
    /// </summary>
    private static long ComputeLockKey(string stateId)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(stateId));
        return BitConverter.ToInt64(hash, 0);
    }
}
