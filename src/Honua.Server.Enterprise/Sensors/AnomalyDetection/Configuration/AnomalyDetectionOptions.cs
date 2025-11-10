// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;

/// <summary>
/// Configuration options for the sensor anomaly detection service
/// </summary>
public sealed class AnomalyDetectionOptions
{
    /// <summary>
    /// Whether anomaly detection is enabled globally
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Interval between anomaly detection checks
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Stale sensor detection configuration
    /// </summary>
    public StaleSensorDetectionOptions StaleSensorDetection { get; set; } = new();

    /// <summary>
    /// Unusual reading detection configuration
    /// </summary>
    public UnusualReadingDetectionOptions UnusualReadingDetection { get; set; } = new();

    /// <summary>
    /// Alert delivery configuration
    /// </summary>
    public AlertDeliveryOptions AlertDelivery { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration to prevent alert flooding
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();
}

/// <summary>
/// Configuration for stale sensor detection
/// </summary>
public sealed class StaleSensorDetectionOptions
{
    /// <summary>
    /// Whether stale sensor detection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Threshold for considering a sensor stale (no recent data)
    /// Default: 1 hour
    /// </summary>
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Override thresholds per datastream type
    /// Key: ObservedProperty name (e.g., "temperature", "traffic_count")
    /// Value: Threshold duration
    /// </summary>
    public Dictionary<string, TimeSpan> ThresholdOverrides { get; set; } = new();
}

/// <summary>
/// Configuration for unusual reading detection using statistical methods
/// </summary>
public sealed class UnusualReadingDetectionOptions
{
    /// <summary>
    /// Whether unusual reading detection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of standard deviations from mean to consider an outlier
    /// Default: 3 (99.7% of normal distribution)
    /// </summary>
    public double StandardDeviationThreshold { get; set; } = 3.0;

    /// <summary>
    /// Minimum number of observations needed to calculate statistics
    /// Default: 10
    /// </summary>
    public int MinimumObservationCount { get; set; } = 10;

    /// <summary>
    /// Time window for calculating statistics (rolling window)
    /// Default: 24 hours
    /// </summary>
    public TimeSpan StatisticalWindow { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Override thresholds per datastream type
    /// Key: ObservedProperty name (e.g., "temperature", "traffic_count")
    /// Value: Number of standard deviations
    /// </summary>
    public Dictionary<string, double> ThresholdOverrides { get; set; } = new();

    /// <summary>
    /// Override minimum observation counts per datastream type
    /// </summary>
    public Dictionary<string, int> MinimumCountOverrides { get; set; } = new();
}

/// <summary>
/// Configuration for alert delivery
/// </summary>
public sealed class AlertDeliveryOptions
{
    /// <summary>
    /// Webhook URL for delivering alerts via GeoEvent API
    /// If not specified, alerts are logged but not delivered
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Additional webhook URLs for redundant alert delivery
    /// </summary>
    public List<string> AdditionalWebhooks { get; set; } = new();

    /// <summary>
    /// Timeout for webhook calls
    /// </summary>
    public TimeSpan WebhookTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to retry failed webhook deliveries
    /// </summary>
    public bool EnableRetries { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Configuration for rate limiting to prevent alert flooding
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Whether rate limiting is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of alerts per datastream within the time window
    /// Default: 5 alerts
    /// </summary>
    public int MaxAlertsPerDatastream { get; set; } = 5;

    /// <summary>
    /// Time window for rate limiting
    /// Default: 1 hour
    /// </summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum total alerts across all sensors within the time window
    /// Helps prevent overwhelming the alert system
    /// Default: 100 alerts
    /// </summary>
    public int MaxTotalAlerts { get; set; } = 100;
}
