// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Honua.Server.Host.Validation;

namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Request to preseed vector tiles for a layer
/// </summary>
public sealed class VectorTilePreseedRequest : IValidatableObject
{
    [JsonPropertyName("serviceId")]
    [Required(ErrorMessage = "ServiceId is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "ServiceId must be between 1 and 255 characters.")]
    [CollectionName]
    public required string ServiceId { get; init; }

    [JsonPropertyName("layerId")]
    [Required(ErrorMessage = "LayerId is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "LayerId must be between 1 and 255 characters.")]
    [CollectionName]
    public required string LayerId { get; init; }

    [JsonPropertyName("minZoom")]
    [Range(0, 22, ErrorMessage = "MinZoom must be between 0 and 22.")]
    public int MinZoom { get; init; }

    [JsonPropertyName("maxZoom")]
    [Range(0, 22, ErrorMessage = "MaxZoom must be between 0 and 22.")]
    public int MaxZoom { get; init; }

    [JsonPropertyName("datetime")]
    [Iso8601DateTime]
    public string? Datetime { get; init; }

    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; init; }

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(ServiceId))
        {
            throw new InvalidOperationException("ServiceId must be provided.");
        }

        if (string.IsNullOrWhiteSpace(LayerId))
        {
            throw new InvalidOperationException("LayerId must be provided.");
        }

        if (MinZoom < 0 || MinZoom > 22)
        {
            throw new InvalidOperationException("MinZoom must be between 0 and 22.");
        }

        if (MaxZoom < 0 || MaxZoom > 22)
        {
            throw new InvalidOperationException("MaxZoom must be between 0 and 22.");
        }

        if (MinZoom > MaxZoom)
        {
            throw new InvalidOperationException("MinZoom cannot exceed MaxZoom.");
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinZoom > MaxZoom)
        {
            yield return new ValidationResult(
                "MinZoom cannot exceed MaxZoom.",
                new[] { nameof(MinZoom), nameof(MaxZoom) });
        }
    }
}
