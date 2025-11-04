// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Honua.Server.Core.Serialization;

public static class KmzArchiveBuilder
{
    private const string DefaultEntryName = "doc.kml";

    public static byte[] CreateArchive(string kmlContent, string? entryName = null, IReadOnlyDictionary<string, byte[]>? assets = null)
    {
        if (string.IsNullOrWhiteSpace(kmlContent))
        {
            throw new ArgumentException("KML content is required.", nameof(kmlContent));
        }

        var entry = string.IsNullOrWhiteSpace(entryName) ? DefaultEntryName : entryName!;

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteKmlEntry(archive, entry, kmlContent);
            WriteAssets(archive, assets);
        }

        return buffer.ToArray();
    }

    private static void WriteKmlEntry(ZipArchive archive, string entryName, string content)
    {
        var documentEntry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(documentEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void WriteAssets(ZipArchive archive, IReadOnlyDictionary<string, byte[]>? assets)
    {
        if (assets is null)
        {
            return;
        }

        foreach (var pair in assets)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Length == 0)
            {
                continue;
            }

            // Validate asset name to prevent path traversal in KMZ archives
            ValidateAssetName(pair.Key);

            var entry = archive.CreateEntry(pair.Key, CompressionLevel.Fastest);
            using var stream = entry.Open();
            stream.Write(pair.Value, 0, pair.Value.Length);
        }
    }

    /// <summary>
    /// Validates that an asset name does not contain path traversal sequences.
    /// KMZ files are ZIP archives, and malicious asset names could write files outside the archive.
    /// </summary>
    private static void ValidateAssetName(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new ArgumentException("Asset name cannot be null or whitespace.", nameof(assetName));
        }

        // Reject absolute paths
        if (Path.IsPathRooted(assetName))
        {
            throw new ArgumentException($"Asset name cannot be an absolute path: {assetName}", nameof(assetName));
        }

        // Reject path traversal sequences
        if (assetName.Contains("..") || assetName.Contains("./") || assetName.Contains(".\\"))
        {
            throw new ArgumentException($"Asset name cannot contain path traversal sequences: {assetName}", nameof(assetName));
        }

        // Reject paths with directory separators that could escape the archive root
        var normalizedPath = assetName.Replace('\\', '/');
        if (normalizedPath.StartsWith("/") || normalizedPath.Contains("../") || normalizedPath.Contains("/.."))
        {
            throw new ArgumentException($"Asset name contains invalid path segments: {assetName}", nameof(assetName));
        }

        // Ensure no dangerous characters (null bytes, control characters)
        // Note: ZIP archives allow more characters than filesystems (like < > * ? |)
        // But we should reject null bytes and control characters
        foreach (var ch in assetName)
        {
            if (ch == '\0')
            {
                throw new ArgumentException($"Asset name contains null byte character: {assetName}", nameof(assetName));
            }
            if (char.IsControl(ch))
            {
                throw new ArgumentException($"Asset name contains control characters: {assetName}", nameof(assetName));
            }
        }
    }
}
