// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MetadataState = Honua.Cli.AI.Services.Processes.State.MetadataState;
using System.Text.Json;
using System.Globalization;

namespace Honua.Cli.AI.Services.Processes.Steps.Metadata;

/// <summary>
/// Generates a STAC (SpatioTemporal Asset Catalog) Item from extracted metadata.
/// </summary>
public class GenerateStacItemStep : KernelProcessStep<MetadataState>
{
    private readonly ILogger<GenerateStacItemStep> _logger;
    private MetadataState _state = new();

    public GenerateStacItemStep(ILogger<GenerateStacItemStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<MetadataState> state)
    {
        _state = state.State ?? new MetadataState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("GenerateStacItem")]
    public async Task GenerateStacItemAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Generating STAC Item for {DatasetName}", _state.DatasetName);

        _state.Status = "GeneratingStacItem";

        try
        {
            // Generate STAC Item JSON
            var stacItem = GenerateStacJson();

            _state.StacItemJson = stacItem;
            _state.StacGenerated = true;

            _logger.LogInformation("STAC Item generated for {DatasetName}", _state.DatasetName);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "StacGenerated",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate STAC Item for {DatasetName}", _state.DatasetName);
            _state.Status = "StacGenerationFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "StacGenerationFailed",
                Data = new { _state.DatasetName, Error = ex.Message }
            });
        }
    }

    private string GenerateStacJson()
    {
        // Parse bounding box from state
        var bboxParts = _state.BoundingBox.Split(',');
        if (bboxParts.Length != 4)
        {
            throw new InvalidOperationException($"Invalid bounding box format: {_state.BoundingBox}");
        }

        var minX = double.Parse(bboxParts[0], CultureInfo.InvariantCulture);
        var minY = double.Parse(bboxParts[1], CultureInfo.InvariantCulture);
        var maxX = double.Parse(bboxParts[2], CultureInfo.InvariantCulture);
        var maxY = double.Parse(bboxParts[3], CultureInfo.InvariantCulture);

        // Create GeoJSON coordinates for bounding box polygon
        var coordinates = $"[[{minX},{minY}],[{maxX},{minY}],[{maxX},{maxY}],[{minX},{maxY}],[{minX},{minY}]]";

        // Extract EPSG code
        var epsgCode = _state.CRS?.Replace("EPSG:", "").Replace("UNKNOWN", "4326") ?? "4326";

        // Determine media type from file extension
        var extension = Path.GetExtension(_state.DatasetPath).ToLowerInvariant();
        var mediaType = extension switch
        {
            ".tif" or ".tiff" => "image/tiff; application=geotiff",
            ".nc" or ".nc4" or ".netcdf" => "application/x-netcdf",
            ".h5" or ".hdf5" or ".hdf" => "application/x-hdf5",
            ".grib" or ".grib2" => "application/wmo-GRIB2",
            ".zarr" => "application/vnd+zarr",
            _ => "application/octet-stream"
        };

        // Parse temporal extent for datetime property
        string datetime;
        if (!string.IsNullOrWhiteSpace(_state.TemporalExtent))
        {
            // If it's a range, use start time
            var temporalParts = _state.TemporalExtent.Split('/');
            datetime = temporalParts[0];

            // Try to ensure ISO 8601 format
            if (!datetime.Contains('T'))
            {
                datetime += "T00:00:00Z";
            }
        }
        else
        {
            datetime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        // Build bands/eo:bands extension if bands are present
        var eoBands = string.Empty;
        if (_state.Bands.Count > 0)
        {
            var bandsList = new List<string>();
            for (int i = 0; i < _state.Bands.Count; i++)
            {
                var bandName = _state.Bands[i];
                bandsList.Add($"{{\"name\":\"{bandName}\",\"common_name\":\"{bandName.ToLowerInvariant()}\"}}");
            }
            eoBands = $",\n    \"eo:bands\": [{string.Join(",", bandsList)}]";
        }

        // Add raster extension properties if available
        var rasterProps = string.Empty;
        if (_state.ExtractedMetadata.ContainsKey("Width") && _state.ExtractedMetadata.ContainsKey("Height"))
        {
            rasterProps = $",\n    \"raster:width\": {_state.ExtractedMetadata["Width"]},\n    \"raster:height\": {_state.ExtractedMetadata["Height"]}";
        }

        // Format GSD property - ensure numeric values are unquoted, strings are quoted
        var gsdValue = _state.Resolution;
        var gsdJson = string.IsNullOrEmpty(gsdValue) || gsdValue.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? "\"unknown\""
            : (double.TryParse(gsdValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ? gsdValue : $"\"{gsdValue}\"");

        // Generate STAC Item compliant with STAC spec 1.0.0
        var stacJson = $$"""
        {
          "stac_version": "1.0.0",
          "stac_extensions": [
            "https://stac-extensions.github.io/projection/v1.1.0/schema.json"
          ],
          "type": "Feature",
          "id": "{{_state.MetadataId}}",
          "bbox": [{{minX}},{{minY}},{{maxX}},{{maxY}}],
          "geometry": {
            "type": "Polygon",
            "coordinates": [{{coordinates}}]
          },
          "properties": {
            "datetime": "{{datetime}}",
            "title": "{{_state.DatasetName}}",
            "proj:epsg": {{epsgCode}},
            "proj:bbox": [{{minX}},{{minY}},{{maxX}},{{maxY}}],
            "gsd": {{gsdJson}}{{eoBands}}{{rasterProps}}
          },
          "assets": {
            "data": {
              "href": "{{_state.DatasetPath}}",
              "type": "{{mediaType}}",
              "roles": ["data"],
              "title": "{{_state.DatasetName}}"
            }
          },
          "links": [
            {
              "rel": "self",
              "href": "./{{_state.DatasetName}}.json"
            },
            {
              "rel": "parent",
              "href": "../collection.json"
            },
            {
              "rel": "collection",
              "href": "../collection.json"
            }
          ]
        }
        """;

        // Validate JSON format
        try
        {
            using var doc = JsonDocument.Parse(stacJson);
            _logger.LogDebug("Generated valid STAC JSON for {DatasetName}", _state.DatasetName);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Generated invalid STAC JSON for {DatasetName}", _state.DatasetName);
            throw;
        }

        return stacJson;
    }
}
