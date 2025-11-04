// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for Metadata Process workflow.
/// Tracks geospatial dataset metadata extraction and STAC publishing.
/// </summary>
public class MetadataState
{
    public string ProcessId { get; set; } = string.Empty;
    public string MetadataId { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public string DatasetPath { get; set; } = string.Empty;
    public List<string> DatasetFiles { get; set; } = new();
    public Dictionary<string, object> ExtractedMetadata { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public string BoundingBox { get; set; } = string.Empty;
    public string CRS { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string TemporalExtent { get; set; } = string.Empty;
    public List<string> Bands { get; set; } = new();
    public string? StacItemJson { get; set; }
    public string? STACItemUrl { get; set; }
    public string? PublishedUrl { get; set; }
    public bool MetadataExtracted { get; set; }
    public bool StacGenerated { get; set; }
    public DateTime StartTime { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// API key for authenticating STAC catalog publishing requests.
    /// Retrieved from configuration or environment variables.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Bearer token for authenticating STAC catalog publishing requests.
    /// Alternative to ApiKey for token-based authentication.
    /// </summary>
    public string? BearerToken { get; set; }
}
