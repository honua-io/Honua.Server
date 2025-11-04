// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Features;

/// <summary>
/// Service for managing feature flags and graceful degradation.
/// Monitors health checks and automatically degrades features when unhealthy.
/// </summary>
public sealed class FeatureManagementService : IFeatureManagementService
{
    private readonly IOptionsMonitor<FeatureFlagsOptions> _optionsMonitor;
    private readonly HealthCheckService? _healthCheckService;
    private readonly ILogger<FeatureManagementService> _logger;
    private readonly ConcurrentDictionary<string, FeatureState> _featureStates = new();
    private readonly ConcurrentDictionary<string, bool> _manualOverrides = new();
    private readonly IDisposable? _optionsChangeToken;

    private sealed class FeatureState
    {
        public FeatureStatus Status { get; set; } = null!;
        public FeatureOptions Options { get; set; } = null!;
        public DateTimeOffset LastHealthCheck { get; set; }
    }

    public FeatureManagementService(
        IOptionsMonitor<FeatureFlagsOptions> optionsMonitor,
        ILogger<FeatureManagementService> logger,
        HealthCheckService? healthCheckService = null)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthCheckService = healthCheckService;

        InitializeFeatures();

        // Register change callback for hot reload support
        _optionsChangeToken = optionsMonitor.OnChange(OnConfigurationChanged);

        _logger.LogInformation(
            "Feature management service initialized with hot reload support. {Count} features registered",
            _featureStates.Count);
    }

    /// <summary>
    /// Handles configuration changes during hot reload.
    /// </summary>
    /// <param name="options">New configuration options.</param>
    private void OnConfigurationChanged(FeatureFlagsOptions options)
    {
        _logger.LogInformation("Feature flags configuration reloaded. Updating feature states...");

        // Update feature states based on new configuration
        UpdateFeatureState("AIConsultant", options.AIConsultant);
        UpdateFeatureState("AdvancedCaching", options.AdvancedCaching);
        UpdateFeatureState("Search", options.Search);
        UpdateFeatureState("RealTimeMetrics", options.RealTimeMetrics);
        UpdateFeatureState("StacCatalog", options.StacCatalog);
        UpdateFeatureState("AdvancedRasterProcessing", options.AdvancedRasterProcessing);
        UpdateFeatureState("VectorTiles", options.VectorTiles);
        UpdateFeatureState("Analytics", options.Analytics);
        UpdateFeatureState("ExternalStorage", options.ExternalStorage);
        UpdateFeatureState("OidcAuthentication", options.OidcAuthentication);

        _logger.LogInformation("Feature flags configuration reload complete");
    }

    /// <summary>
    /// Updates a feature state based on new configuration options.
    /// </summary>
    /// <param name="featureName">Name of the feature.</param>
    /// <param name="newOptions">New feature options.</param>
    private void UpdateFeatureState(string featureName, FeatureOptions newOptions)
    {
        if (!_featureStates.TryGetValue(featureName, out var state))
        {
            _logger.LogWarning("Attempted to update unknown feature: {Feature}", featureName);
            return;
        }

        var wasEnabled = state.Options.Enabled;
        var nowEnabled = newOptions.Enabled;

        // Update the options
        state.Options = newOptions;

        // Handle enable/disable state changes
        if (wasEnabled && !nowEnabled)
        {
            // Feature was disabled in configuration
            if (!_manualOverrides.ContainsKey(featureName))
            {
                state.Status = FeatureStatus.Disabled(featureName, "Disabled via configuration reload");
                _logger.LogWarning("Feature {Feature} disabled via configuration reload", featureName);
            }
        }
        else if (!wasEnabled && nowEnabled)
        {
            // Feature was enabled in configuration
            if (!_manualOverrides.ContainsKey(featureName))
            {
                state.Status = FeatureStatus.Healthy(featureName);
                _logger.LogInformation("Feature {Feature} enabled via configuration reload", featureName);
            }
        }

        // Update health check parameters
        if (state.Options.MinHealthScore != newOptions.MinHealthScore ||
            state.Options.RecoveryCheckInterval != newOptions.RecoveryCheckInterval)
        {
            _logger.LogInformation(
                "Feature {Feature} health parameters updated: MinHealthScore={MinScore}, RecoveryInterval={Interval}s",
                featureName,
                newOptions.MinHealthScore,
                newOptions.RecoveryCheckInterval);
        }
    }

    private void InitializeFeatures()
    {
        var options = _optionsMonitor.CurrentValue;
        RegisterFeature("AIConsultant", options.AIConsultant);
        RegisterFeature("AdvancedCaching", options.AdvancedCaching);
        RegisterFeature("Search", options.Search);
        RegisterFeature("RealTimeMetrics", options.RealTimeMetrics);
        RegisterFeature("StacCatalog", options.StacCatalog);
        RegisterFeature("AdvancedRasterProcessing", options.AdvancedRasterProcessing);
        RegisterFeature("VectorTiles", options.VectorTiles);
        RegisterFeature("Analytics", options.Analytics);
        RegisterFeature("ExternalStorage", options.ExternalStorage);
        RegisterFeature("OidcAuthentication", options.OidcAuthentication);

        _logger.LogInformation(
            "Feature management initialized with {Count} features",
            _featureStates.Count);
    }

    private void RegisterFeature(string name, FeatureOptions options)
    {
        var status = options.Enabled
            ? FeatureStatus.Healthy(name)
            : FeatureStatus.Disabled(name, "Disabled in configuration");

        _featureStates[name] = new FeatureState
        {
            Status = status,
            Options = options,
            LastHealthCheck = DateTimeOffset.UtcNow
        };

        _logger.LogDebug(
            "Registered feature {Feature}: Enabled={Enabled}, Required={Required}",
            name,
            options.Enabled,
            options.Required);
    }

    public async Task<bool> IsFeatureAvailableAsync(
        string featureName,
        CancellationToken cancellationToken = default)
    {
        // Check manual override first
        if (_manualOverrides.TryGetValue(featureName, out var overrideValue))
        {
            return overrideValue;
        }

        if (!_featureStates.TryGetValue(featureName, out var state))
        {
            _logger.LogWarning("Unknown feature requested: {Feature}", featureName);
            return false;
        }

        // If not enabled in config, it's not available
        if (!state.Options.Enabled)
        {
            return false;
        }

        // Check if we need to perform health check
        var timeSinceLastCheck = DateTimeOffset.UtcNow - state.LastHealthCheck;
        if (timeSinceLastCheck.TotalSeconds >= state.Options.RecoveryCheckInterval)
        {
            await CheckFeatureHealthAsync(featureName, cancellationToken);
        }

        return state.Status.IsAvailable;
    }

    public Task<FeatureStatus> GetFeatureStatusAsync(
        string featureName,
        CancellationToken cancellationToken = default)
    {
        if (!_featureStates.TryGetValue(featureName, out var state))
        {
            return Task.FromResult(FeatureStatus.Unavailable(
                featureName,
                "Feature not registered"));
        }

        return Task.FromResult(state.Status);
    }

    public Task<Dictionary<string, FeatureStatus>> GetAllFeatureStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var statuses = _featureStates.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Status);

        return Task.FromResult(statuses);
    }

    public Task DisableFeatureAsync(
        string featureName,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_featureStates.TryGetValue(featureName, out var state))
        {
            _logger.LogWarning("Attempted to disable unknown feature: {Feature}", featureName);
            return Task.CompletedTask;
        }

        if (state.Options.Required)
        {
            _logger.LogWarning(
                "Cannot manually disable required feature: {Feature}",
                featureName);
            return Task.CompletedTask;
        }

        _manualOverrides[featureName] = false;
        state.Status = FeatureStatus.Disabled(featureName, reason);
        state.LastHealthCheck = DateTimeOffset.UtcNow;

        _logger.LogWarning(
            "Feature {Feature} manually disabled: {Reason}",
            featureName,
            reason);

        return Task.CompletedTask;
    }

    public Task EnableFeatureAsync(
        string featureName,
        CancellationToken cancellationToken = default)
    {
        if (!_featureStates.TryGetValue(featureName, out var state))
        {
            _logger.LogWarning("Attempted to enable unknown feature: {Feature}", featureName);
            return Task.CompletedTask;
        }

        _manualOverrides.TryRemove(featureName, out _);

        if (!state.Options.Enabled)
        {
            _logger.LogWarning(
                "Cannot enable feature {Feature} - disabled in configuration",
                featureName);
            return Task.CompletedTask;
        }

        state.Status = FeatureStatus.Healthy(featureName);
        state.LastHealthCheck = DateTimeOffset.UtcNow;

        _logger.LogInformation("Feature {Feature} manually enabled", featureName);

        return Task.CompletedTask;
    }

    public async Task<FeatureStatus> CheckFeatureHealthAsync(
        string featureName,
        CancellationToken cancellationToken = default)
    {
        if (!_featureStates.TryGetValue(featureName, out var state))
        {
            return FeatureStatus.Unavailable(featureName, "Feature not registered");
        }

        state.LastHealthCheck = DateTimeOffset.UtcNow;

        // If manually overridden, don't check health
        if (_manualOverrides.ContainsKey(featureName))
        {
            return state.Status;
        }

        // If health check service not available, assume healthy
        if (_healthCheckService == null)
        {
            return state.Status;
        }

        try
        {
            // Map feature name to health check tag
            var healthCheckTag = GetHealthCheckTag(featureName);
            if (healthCheckTag == null)
            {
                // No specific health check for this feature
                return state.Status;
            }

            // Run health checks with the feature's tag
            var healthReport = await _healthCheckService.CheckHealthAsync(
                check => check.Tags.Contains(healthCheckTag),
                cancellationToken);

            var healthScore = CalculateHealthScore(healthReport);

            // Determine if degradation is needed
            if (healthScore < state.Options.MinHealthScore)
            {
                state.Status = ApplyDegradation(featureName, state.Options, healthScore, healthReport);

                if (!state.Options.Required)
                {
                    _logger.LogWarning(
                        "Feature {Feature} degraded: Health score {Score} below threshold {Threshold}. Status: {Status}",
                        featureName,
                        healthScore,
                        state.Options.MinHealthScore,
                        healthReport.Status);
                }
                else
                {
                    _logger.LogError(
                        "Required feature {Feature} is unhealthy: Health score {Score} below threshold {Threshold}. Status: {Status}",
                        featureName,
                        healthScore,
                        state.Options.MinHealthScore,
                        healthReport.Status);
                }
            }
            else if (state.Status.IsDegraded)
            {
                // Feature has recovered
                state.Status = FeatureStatus.Healthy(featureName, healthScore);

                _logger.LogInformation(
                    "Feature {Feature} recovered: Health score {Score}",
                    featureName,
                    healthScore);
            }
            else
            {
                // Update health score but keep healthy status
                state.Status = FeatureStatus.Healthy(featureName, healthScore);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking health for feature {Feature}",
                featureName);

            // On error, degrade if not required
            if (!state.Options.Required)
            {
                state.Status = FeatureStatus.Unavailable(
                    featureName,
                    $"Health check error: {ex.Message}");
            }
        }

        return state.Status;
    }

    private static string? GetHealthCheckTag(string featureName)
    {
        return featureName switch
        {
            "AdvancedCaching" => "distributed",
            "StacCatalog" => "stac",
            "OidcAuthentication" => "oidc",
            "RealTimeMetrics" => "metrics",
            _ => null
        };
    }

    private static int CalculateHealthScore(HealthReport report)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            return 100;
        }

        if (report.Status == HealthStatus.Degraded)
        {
            return 50;
        }

        // Unhealthy
        return 0;
    }

    private FeatureStatus ApplyDegradation(
        string featureName,
        FeatureOptions options,
        int healthScore,
        HealthReport healthReport)
    {
        var strategy = options.Strategy ?? new DegradationStrategy
        {
            Type = DegradationType.Disable
        };

        var reason = $"Health check failed: {healthReport.Status}";
        var nextCheck = DateTimeOffset.UtcNow.AddSeconds(options.RecoveryCheckInterval);

        return strategy.Type switch
        {
            DegradationType.Disable => FeatureStatus.Unavailable(featureName, reason),

            DegradationType.ReduceQuality or
            DegradationType.ReduceFunctionality or
            DegradationType.ReducePerformance or
            DegradationType.Fallback => FeatureStatus.Degraded(
                featureName,
                healthScore,
                strategy.Type,
                reason,
                nextCheck),

            _ => FeatureStatus.Unavailable(featureName, reason)
        };
    }
}
