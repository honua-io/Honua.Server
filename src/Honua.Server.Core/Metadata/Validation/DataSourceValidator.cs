// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates data source metadata definitions.
/// </summary>
internal static class DataSourceValidator
{
    /// <summary>
    /// Validates data source definitions and returns a set of data source IDs.
    /// </summary>
    /// <param name="dataSources">The data sources to validate.</param>
    /// <returns>A set of valid data source IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when data source validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(IReadOnlyList<DataSourceDefinition> dataSources)
    {
        var dataSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataSource in dataSources)
        {
            if (dataSource is null)
            {
                continue;
            }

            if (dataSource.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Data sources must include an id.");
            }

            if (!dataSourceIds.Add(dataSource.Id))
            {
                throw new InvalidDataException($"Duplicate data source id '{dataSource.Id}'.");
            }
        }

        return dataSourceIds;
    }
}
