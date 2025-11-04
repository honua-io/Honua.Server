// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

internal static class ZarrMetadataConsolidator
{
    private static readonly string[] MetadataFiles = { ".zgroup", ".zarray", ".zattrs" };

    public static async Task ConsolidateAsync(string zarrPath, CancellationToken cancellationToken)
    {
        Guard.NotNullOrWhiteSpace(zarrPath);

        if (!Directory.Exists(zarrPath))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var metadataEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        await CollectMetadataEntriesAsync(zarrPath, string.Empty, metadataEntries, cancellationToken).ConfigureAwait(false);

        if (metadataEntries.Count == 0)
        {
            return;
        }

        await WriteZMetadataAsync(zarrPath, metadataEntries, cancellationToken).ConfigureAwait(false);
        await WriteKerchunkReferenceAsync(zarrPath, metadataEntries, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CollectMetadataEntriesAsync(
        string directory,
        string relativePath,
        IDictionary<string, string> metadataEntries,
        CancellationToken cancellationToken)
    {
        foreach (var metadataFile in MetadataFiles)
        {
            var fullPath = Path.Combine(directory, metadataFile);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var key = relativePath.IsNullOrEmpty() ? metadataFile : $"{relativePath}/{metadataFile}";
            metadataEntries[key] = content;
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDirectory);
            if (name.IsNullOrEmpty() || name.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            var childRelative = relativePath.IsNullOrEmpty() ? name : $"{relativePath}/{name}";
            await CollectMetadataEntriesAsync(subDirectory, childRelative, metadataEntries, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteZMetadataAsync(
        string zarrPath,
        IReadOnlyDictionary<string, string> metadataEntries,
        CancellationToken cancellationToken)
    {
        var metadataNode = new JsonObject();

        foreach (var entry in metadataEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Value.IsNullOrWhiteSpace())
            {
                metadataNode[entry.Key] = JsonValue.Create((string?)null);
                continue;
            }

            var parsed = JsonNode.Parse(entry.Value);
            metadataNode[entry.Key] = parsed;
        }

        var consolidated = new JsonObject
        {
            ["zarr_consolidated_format"] = 1,
            ["metadata"] = metadataNode
        };

        var outputPath = Path.Combine(zarrPath, ".zmetadata");
        await File.WriteAllTextAsync(outputPath, consolidated.ToJsonString(JsonSerializerOptionsRegistry.Web), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteKerchunkReferenceAsync(
        string zarrPath,
        IReadOnlyDictionary<string, string> metadataEntries,
        CancellationToken cancellationToken)
    {
        var refsNode = new JsonObject();

        foreach (var entry in metadataEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Value.IsNullOrWhiteSpace())
            {
                refsNode[entry.Key] = JsonValue.Create((string?)null);
                continue;
            }

            refsNode[entry.Key] = JsonNode.Parse(entry.Value);
        }

        foreach (var file in Directory.EnumerateFiles(zarrPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(file);
            if (name.IsNullOrEmpty())
            {
                continue;
            }

            if (name.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            if (name.Equals("kerchunk-reference.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Array.Exists(MetadataFiles, meta => string.Equals(meta, name, StringComparison.Ordinal)))
            {
                continue;
            }

            var relative = Path.GetRelativePath(zarrPath, file).Replace('\\', '/');
            var length = new FileInfo(file).Length;

            var array = new JsonArray
            {
                JsonValue.Create($"{{{{root}}}}/{relative}"),
                JsonValue.Create(0),
                JsonValue.Create(length)
            };

            refsNode[relative] = array;
        }

        var rootObject = new JsonObject
        {
            ["version"] = 1,
            ["templates"] = new JsonObject
            {
                ["root"] = zarrPath.Replace('\\', '/')
            },
            ["refs"] = refsNode
        };

        var outputPath = Path.Combine(zarrPath, "kerchunk-reference.json");
        await File.WriteAllTextAsync(outputPath, rootObject.ToJsonString(JsonSerializerOptionsRegistry.Web), cancellationToken).ConfigureAwait(false);
    }
}
