// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates folder metadata definitions.
/// </summary>
internal static class FolderValidator
{
    /// <summary>
    /// Validates folder definitions and returns a set of folder IDs.
    /// </summary>
    /// <param name="folders">The folders to validate.</param>
    /// <returns>A set of valid folder IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when folder validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(IReadOnlyList<FolderDefinition> folders)
    {
        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            if (folder is null)
            {
                continue;
            }

            if (folder.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Folders must include an id.");
            }

            if (!folderIds.Add(folder.Id))
            {
                throw new InvalidDataException($"Duplicate folder id '{folder.Id}'.");
            }
        }

        return folderIds;
    }
}
