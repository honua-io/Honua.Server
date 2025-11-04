// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Validation;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Raster;

public sealed class RasterTilePreseedRequest : IValidatableObject
{
    public const int MaxTileBudget = 100_000;

    public RasterTilePreseedRequest(IEnumerable<string> datasetIds)
    {
        Guard.NotNull(datasetIds);

        var normalized = datasetIds
            .Select(id => id?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one dataset identifier must be provided.", nameof(datasetIds));
        }

        DatasetIds = new ReadOnlyCollection<string>(normalized);
    }

    [Required(ErrorMessage = "At least one dataset ID is required.")]
    [MinLength(1, ErrorMessage = "At least one dataset ID is required.")]
    public IReadOnlyList<string> DatasetIds { get; }

    [Required(ErrorMessage = "TileMatrixSetId is required.")]
    [StringLength(100, ErrorMessage = "TileMatrixSetId cannot exceed 100 characters.")]
    public string TileMatrixSetId { get; init; } = OgcTileMatrixHelper.WorldWebMercatorQuadId;

    [Range(0, 30, ErrorMessage = "MinZoom must be between 0 and 30.")]
    public int? MinZoom { get; init; }

    [Range(0, 30, ErrorMessage = "MaxZoom must be between 0 and 30.")]
    public int? MaxZoom { get; init; }

    [StringLength(100, ErrorMessage = "StyleId cannot exceed 100 characters.")]
    public string? StyleId { get; init; }

    public bool Transparent { get; init; } = true;

    [Required(ErrorMessage = "Format is required.")]
    [AllowedMimeTypes("image/png", "image/jpeg", "image/webp", "image/avif")]
    public string Format { get; init; } = "image/png";

    public bool Overwrite { get; init; }

    [TileSize]
    public int TileSize { get; init; } = 256;

    public void EnsureValid()
    {
        if (!OgcTileMatrixHelper.IsSupportedMatrixSet(TileMatrixSetId))
        {
            throw new InvalidOperationException($"Tile matrix set '{TileMatrixSetId}' is not supported.");
        }

        if (TileSize <= 0 || TileSize > 4096)
        {
            throw new InvalidOperationException("TileSize must be between 1 and 4096 pixels.");
        }

        if (MinZoom is < 0)
        {
            throw new InvalidOperationException("MinZoom must be non-negative.");
        }

        if (MaxZoom is < 0)
        {
            throw new InvalidOperationException("MaxZoom must be non-negative.");
        }

        if (MinZoom.HasValue && MaxZoom.HasValue && MinZoom.Value > MaxZoom.Value)
        {
            throw new InvalidOperationException("MinZoom cannot exceed MaxZoom.");
        }

        if (string.IsNullOrWhiteSpace(Format))
        {
            throw new InvalidOperationException("Format must be provided.");
        }

        // CRITICAL: Validate total tile count
        if (MinZoom.HasValue && MaxZoom.HasValue)
        {
            long totalTiles = 0;
            for (var zoom = MinZoom.Value; zoom <= MaxZoom.Value; zoom++)
            {
                var tilesAtZoom = 1L << zoom;
                totalTiles += tilesAtZoom * tilesAtZoom;
                if (totalTiles > MaxTileBudget)
                {
                    throw new InvalidOperationException(
                        $"Requested tile range would generate more than 100,000 tiles. " +
                        $"Reduce zoom range or provide a smaller bounding box.");
                }
            }
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!OgcTileMatrixHelper.IsSupportedMatrixSet(TileMatrixSetId))
        {
            yield return new ValidationResult(
                $"Tile matrix set '{TileMatrixSetId}' is not supported.",
                new[] { nameof(TileMatrixSetId) });
        }

        if (MinZoom.HasValue && MaxZoom.HasValue && MinZoom.Value > MaxZoom.Value)
        {
            yield return new ValidationResult(
                "MinZoom cannot exceed MaxZoom.",
                new[] { nameof(MinZoom), nameof(MaxZoom) });
        }
    }
}
