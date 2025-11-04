// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Utilities;

internal static class FileNameHelper
{
    private static readonly HashSet<char> InvalidCharacters = CreateInvalidCharacterSet();

    public static string BuildDownloadFileName(string collectionId, string? featureId, string extension)
    {
        if (extension.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Extension must be provided.", nameof(extension));
        }

        var baseSegment = SanitizeSegment(collectionId);
        if (featureId.HasValue())
        {
            baseSegment = string.Concat(baseSegment, "-", SanitizeSegment(featureId));
        }

        return string.Concat(baseSegment, ".", extension);
    }

    public static string BuildArchiveEntryName(string collectionId, string? featureId)
    {
        return BuildDownloadFileName(collectionId, featureId, "kml");
    }

    public static string SanitizeSegment(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "export";
        }

        var chars = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            chars[i] = InvalidCharacters.Contains(ch) ? '_' : ch;
        }

        var sanitized = new string(chars).Trim('_');
        return sanitized.IsNullOrEmpty() ? "export" : sanitized;
    }

    private static HashSet<char> CreateInvalidCharacterSet()
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var additional = new[] { ':', '/', '\\', '?', '*', '"', '<', '>', '|' };
        foreach (var ch in additional)
        {
            invalid.Add(ch);
        }

        return invalid;
    }
}
