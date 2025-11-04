// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using Honua.Server.Core.Extensions;

#nullable enable

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Shared helper for normalizing GlobalId values in GeoServices REST operations.
/// Handles various GlobalId formats including quoted strings, GUID formats with/without braces.
/// </summary>
public static class GeoservicesGlobalIdHelper
{
    /// <summary>
    /// Normalizes a GlobalId value by trimming whitespace, removing quotes and braces,
    /// and formatting valid GUIDs consistently.
    /// </summary>
    /// <param name="value">The GlobalId value to normalize.</param>
    /// <returns>
    /// Normalized GlobalId string, or null if the input is null/whitespace.
    /// Valid GUIDs are returned in standard "D" format (e.g., "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx").
    /// </returns>
    public static string? NormalizeGlobalId(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return null;
        }

        var trimmed = value.Trim();

        // Remove surrounding double quotes if present
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        // Try to parse as GUID - this will also handle {guid} format by stripping braces
        if (Guid.TryParse(trimmed, out var guid))
        {
            return guid.ToString("D", CultureInfo.InvariantCulture);
        }

        // If not a valid GUID, return the trimmed value as-is
        return trimmed;
    }
}
