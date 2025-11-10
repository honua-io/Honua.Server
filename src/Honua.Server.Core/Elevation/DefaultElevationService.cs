// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Elevation;

/// <summary>
/// Default elevation service that returns a configured default elevation or zero.
/// This is a fallback provider that always succeeds.
/// </summary>
public sealed class DefaultElevationService : IElevationService
{
    private readonly double _defaultElevation;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultElevationService"/> class.
    /// </summary>
    /// <param name="defaultElevation">Default elevation in meters (default: 0)</param>
    public DefaultElevationService(double defaultElevation = 0)
    {
        _defaultElevation = defaultElevation;
    }

    /// <inheritdoc />
    public Task<double?> GetElevationAsync(
        double longitude,
        double latitude,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(context);

        // Check if configuration specifies a default
        var elevation = context.Configuration?.DefaultElevation ?? _defaultElevation;

        // Apply vertical offset if configured
        if (context.Configuration?.VerticalOffset != 0)
        {
            elevation += context.Configuration!.VerticalOffset;
        }

        return Task.FromResult<double?>(elevation);
    }

    /// <inheritdoc />
    public Task<double?[]> GetElevationsAsync(
        IReadOnlyList<(double Longitude, double Latitude)> coordinates,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(coordinates);
        Guard.NotNull(context);

        var elevation = context.Configuration?.DefaultElevation ?? _defaultElevation;

        // Apply vertical offset if configured
        if (context.Configuration?.VerticalOffset != 0)
        {
            elevation += context.Configuration!.VerticalOffset;
        }

        var results = new double?[coordinates.Count];
        Array.Fill(results, elevation);

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public bool CanProvideElevation(ElevationContext context)
    {
        // Always returns true as this is a fallback provider
        return true;
    }
}
