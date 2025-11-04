using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Stac;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Backwards-compatible shim for legacy tests that were written against the older STAC extent models.
/// The production code now exposes <see cref="StacExtent"/> along with simple spatial/temporal collections,
/// so these lightweight helpers project the legacy object initialiser syntax into the new structures.
/// </summary>
internal sealed class StacSpatialExtent : List<double[]>
{
    public IEnumerable<IEnumerable<double>> Bbox
    {
        init
        {
            Clear();
            foreach (var coordinates in value)
            {
                Add(coordinates.ToArray());
            }
        }
    }
}

internal sealed class StacTemporalExtent : List<StacTemporalInterval>
{
    public IEnumerable Interval
    {
        init
        {
            Clear();
            foreach (var entry in value)
            {
                if (entry is not IEnumerable inner)
                {
                    continue;
                }

                var values = inner.Cast<object?>().ToArray();
                var interval = new StacTemporalInterval
                {
                    Start = Parse(values.ElementAtOrDefault(0)),
                    End = Parse(values.ElementAtOrDefault(1))
                };

                Add(interval);
            }
        }
    }

    private static DateTimeOffset? Parse(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            string s when !string.IsNullOrWhiteSpace(s)
                => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => null
        };
    }
}
