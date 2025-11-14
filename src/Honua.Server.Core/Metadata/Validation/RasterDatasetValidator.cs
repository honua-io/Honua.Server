// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates raster dataset metadata definitions.
/// </summary>
internal static class RasterDatasetValidator
{
    /// <summary>
    /// Validates raster dataset definitions and returns a set of raster IDs.
    /// </summary>
    /// <param name="rasterDatasets">The raster datasets to validate.</param>
    /// <param name="serviceIds">The set of valid service IDs.</param>
    /// <param name="layerIds">The set of valid layer IDs.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <returns>A set of valid raster dataset IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when raster dataset validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets,
        HashSet<string> serviceIds,
        HashSet<string> layerIds,
        HashSet<string> styleIds)
    {
        var rasterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raster in rasterDatasets)
        {
            if (raster is null)
            {
                continue;
            }

            if (raster.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Raster datasets must include an id.");
            }

            if (!rasterIds.Add(raster.Id))
            {
                throw new InvalidDataException($"Duplicate raster dataset id '{raster.Id}'.");
            }

            if (raster.ServiceId.HasValue() && !serviceIds.Contains(raster.ServiceId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown service '{raster.ServiceId}'.");
            }

            if (raster.LayerId.HasValue() && !layerIds.Contains(raster.LayerId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown layer '{raster.LayerId}'.");
            }

            if (raster.Source is null)
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' must include a source definition.");
            }

            if (raster.Source.Type.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' is missing a source type.");
            }

            if (raster.Source.Uri.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' is missing a source uri.");
            }

            if (raster.Styles.DefaultStyleId.HasValue() && !styleIds.Contains(raster.Styles.DefaultStyleId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' default style '{raster.Styles.DefaultStyleId}' is not defined.");
            }

            foreach (var styleId in raster.Styles.StyleIds)
            {
                if (!styleIds.Contains(styleId))
                {
                    throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown style '{styleId}'.");
                }
            }
        }

        return rasterIds;
    }
}
