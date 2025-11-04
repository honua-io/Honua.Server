// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Features;

/// <summary>
/// Service for managing feature flags and graceful degradation.
/// </summary>
public interface IFeatureManagementService
{
    /// <summary>
    /// Checks if a feature is currently available.
    /// </summary>
    /// <param name="featureName">Name of the feature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feature is available, false otherwise.</returns>
    Task<bool> IsFeatureAvailableAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a feature.
    /// </summary>
    /// <param name="featureName">Name of the feature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current feature status.</returns>
    Task<FeatureStatus> GetFeatureStatusAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of all features.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of feature statuses.</returns>
    Task<Dictionary<string, FeatureStatus>> GetAllFeatureStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually disables a feature.
    /// </summary>
    /// <param name="featureName">Name of the feature.</param>
    /// <param name="reason">Reason for disabling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisableFeatureAsync(string featureName, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually enables a feature.
    /// </summary>
    /// <param name="featureName">Name of the feature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnableFeatureAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a health check for a specific feature.
    /// </summary>
    /// <param name="featureName">Name of the feature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated feature status.</returns>
    Task<FeatureStatus> CheckFeatureHealthAsync(string featureName, CancellationToken cancellationToken = default);
}
