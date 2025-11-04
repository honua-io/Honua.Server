// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Configuration options for circuit breaker policies.
/// Circuit breakers prevent cascading failures by temporarily blocking calls to failing services.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "Resilience:CircuitBreaker";

    /// <summary>
    /// Database circuit breaker settings.
    /// </summary>
    public DatabaseCircuitBreakerOptions Database { get; set; } = new();

    /// <summary>
    /// External API circuit breaker settings.
    /// </summary>
    public ExternalApiCircuitBreakerOptions ExternalApi { get; set; } = new();

    /// <summary>
    /// Storage (S3, Azure Blob, GCS) circuit breaker settings.
    /// </summary>
    public StorageCircuitBreakerOptions Storage { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration for database operations.
/// </summary>
public class DatabaseCircuitBreakerOptions
{
    /// <summary>
    /// Whether the circuit breaker is enabled for database operations.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Failure ratio threshold (0.0 to 1.0) that will open the circuit.
    /// For example, 0.5 means circuit opens when 50% of requests fail.
    /// </summary>
    [Range(0.0, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of operations before circuit can open.
    /// Prevents circuit from opening due to low sample size.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Time window for measuring failure ratio.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the circuit stays open before transitioning to half-open.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (FailureRatio < 0.0 || FailureRatio > 1.0)
        {
            throw new InvalidOperationException("FailureRatio must be between 0.0 and 1.0");
        }

        if (MinimumThroughput < 1)
        {
            throw new InvalidOperationException("MinimumThroughput must be at least 1");
        }

        if (SamplingDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("SamplingDuration must be greater than zero");
        }

        if (BreakDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BreakDuration must be greater than zero");
        }
    }
}

/// <summary>
/// Circuit breaker configuration for external API calls.
/// </summary>
public class ExternalApiCircuitBreakerOptions
{
    /// <summary>
    /// Whether the circuit breaker is enabled for external API calls.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Failure ratio threshold (0.0 to 1.0) that will open the circuit.
    /// </summary>
    [Range(0.0, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of operations before circuit can open.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Time window for measuring failure ratio.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the circuit stays open before transitioning to half-open.
    /// External APIs may need longer recovery time.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (FailureRatio < 0.0 || FailureRatio > 1.0)
        {
            throw new InvalidOperationException("FailureRatio must be between 0.0 and 1.0");
        }

        if (MinimumThroughput < 1)
        {
            throw new InvalidOperationException("MinimumThroughput must be at least 1");
        }

        if (SamplingDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("SamplingDuration must be greater than zero");
        }

        if (BreakDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BreakDuration must be greater than zero");
        }
    }
}

/// <summary>
/// Circuit breaker configuration for cloud storage operations (S3, Azure Blob, GCS).
/// </summary>
public class StorageCircuitBreakerOptions
{
    /// <summary>
    /// Whether the circuit breaker is enabled for storage operations.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Failure ratio threshold (0.0 to 1.0) that will open the circuit.
    /// </summary>
    [Range(0.0, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of operations before circuit can open.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Time window for measuring failure ratio.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the circuit stays open before transitioning to half-open.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (FailureRatio < 0.0 || FailureRatio > 1.0)
        {
            throw new InvalidOperationException("FailureRatio must be between 0.0 and 1.0");
        }

        if (MinimumThroughput < 1)
        {
            throw new InvalidOperationException("MinimumThroughput must be at least 1");
        }

        if (SamplingDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("SamplingDuration must be greater than zero");
        }

        if (BreakDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("BreakDuration must be greater than zero");
        }
    }
}
