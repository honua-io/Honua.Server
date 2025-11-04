// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Handles JSON metadata file I/O and deserialization operations.
/// </summary>
internal sealed class JsonMetadataLoader
{
    /// <summary>
    /// Loads and deserializes metadata from a file path.
    /// </summary>
    /// <param name="metadataPath">Path to the metadata JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized metadata document.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the metadata file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file contains invalid JSON or is empty.</exception>
    public async Task<MetadataDocument> LoadFromFileAsync(string metadataPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await JsonHelper.LoadFromFileAsync<MetadataDocument>(metadataPath, JsonSerializerOptionsRegistry.DevTooling, cancellationToken);
            return result ?? throw new InvalidDataException("Metadata file is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Metadata file contains invalid JSON.", ex);
        }
    }

    /// <summary>
    /// Parses metadata from a JSON string.
    /// </summary>
    /// <param name="json">JSON string containing metadata.</param>
    /// <returns>Deserialized metadata document.</returns>
    /// <exception cref="InvalidDataException">Thrown when the JSON is invalid or empty.</exception>
    public MetadataDocument ParseFromString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Metadata payload is empty.");
        }

        try
        {
            return JsonHelper.Deserialize<MetadataDocument>(json, JsonSerializerOptionsRegistry.DevTooling)
                   ?? throw new InvalidDataException("Metadata payload is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Metadata payload contains invalid JSON.", ex);
        }
    }
}
