// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Elevation;

/// <summary>
/// Composite elevation service that delegates to multiple elevation providers.
/// Tries each provider in order until one can handle the request.
/// </summary>
public sealed class CompositeElevationService : IElevationService
{
    private readonly IReadOnlyList<IElevationService> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeElevationService"/> class.
    /// </summary>
    /// <param name="providers">Elevation providers in priority order</param>
    public CompositeElevationService(IEnumerable<IElevationService> providers)
    {
        Guard.NotNull(providers);
        _providers = providers.ToList();
    }

    /// <inheritdoc />
    public async Task<double?> GetElevationAsync(
        double longitude,
        double latitude,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (provider.CanProvideElevation(context))
            {
                return await provider.GetElevationAsync(longitude, latitude, context, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // No provider could handle this request
        return null;
    }

    /// <inheritdoc />
    public async Task<double?[]> GetElevationsAsync(
        IReadOnlyList<(double Longitude, double Latitude)> coordinates,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (provider.CanProvideElevation(context))
            {
                return await provider.GetElevationsAsync(coordinates, context, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // No provider could handle this request - return all nulls
        return new double?[coordinates.Count];
    }

    /// <inheritdoc />
    public bool CanProvideElevation(ElevationContext context)
    {
        return _providers.Any(p => p.CanProvideElevation(context));
    }
}
