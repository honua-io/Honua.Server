// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.Resilience;

/// <summary>
/// Configuration options for hedging strategy.
/// Hedging sends parallel requests and uses the first successful response to reduce tail latency.
/// </summary>
public class HedgingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Resilience:Hedging";

    /// <summary>
    /// Whether hedging is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of hedged attempts (including the primary request).
    /// Example: If set to 2, one primary request and 1 hedged request will be sent.
    /// Default: 2.
    /// </summary>
    public int MaxHedgedAttempts { get; set; } = 2;

    /// <summary>
    /// Delay before sending a hedged request.
    /// This allows the primary request to complete first if it's fast enough.
    /// Default: 50ms.
    /// </summary>
    public int DelayMilliseconds { get; set; } = 50;

    /// <summary>
    /// Overall timeout for the entire hedging operation (all attempts).
    /// Default: 5 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (MaxHedgedAttempts < 1)
        {
            throw new ArgumentException("MaxHedgedAttempts must be at least 1.", nameof(MaxHedgedAttempts));
        }

        if (MaxHedgedAttempts > 5)
        {
            throw new ArgumentException("MaxHedgedAttempts cannot exceed 5 to prevent excessive load.", nameof(MaxHedgedAttempts));
        }

        if (DelayMilliseconds < 0)
        {
            throw new ArgumentException("DelayMilliseconds cannot be negative.", nameof(DelayMilliseconds));
        }

        if (DelayMilliseconds > 5000)
        {
            throw new ArgumentException("DelayMilliseconds cannot exceed 5000ms (5 seconds).", nameof(DelayMilliseconds));
        }

        if (TimeoutSeconds < 1)
        {
            throw new ArgumentException("TimeoutSeconds must be at least 1 second.", nameof(TimeoutSeconds));
        }

        if (TimeoutSeconds > 60)
        {
            throw new ArgumentException("TimeoutSeconds cannot exceed 60 seconds.", nameof(TimeoutSeconds));
        }
    }
}
