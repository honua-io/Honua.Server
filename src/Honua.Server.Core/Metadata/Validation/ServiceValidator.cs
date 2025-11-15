// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates service metadata definitions including folder and data source references, and stored queries.
/// </summary>
internal static class ServiceValidator
{
    /// <summary>
    /// Validates service definitions and returns a set of service IDs.
    /// </summary>
    /// <param name="services">The services to validate.</param>
    /// <param name="folderIds">The set of valid folder IDs.</param>
    /// <param name="dataSourceIds">The set of valid data source IDs.</param>
    /// <returns>A set of valid service IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when service validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(
        IReadOnlyList<ServiceDefinition> services,
        HashSet<string> folderIds,
        HashSet<string> dataSourceIds)
    {
        var serviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            if (service is null)
            {
                continue;
            }

            if (service.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Services must include an id.");
            }

            if (!serviceIds.Add(service.Id))
            {
                throw new InvalidDataException($"Duplicate service id '{service.Id}'.");
            }

            if (service.FolderId.IsNullOrWhiteSpace() || !folderIds.Contains(service.FolderId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown folder '{service.FolderId}'.");
            }

            if (service.DataSourceId.IsNullOrWhiteSpace() || !dataSourceIds.Contains(service.DataSourceId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown data source '{service.DataSourceId}'.");
            }

            ValidateStoredQueries(service);
        }

        return serviceIds;
    }

    /// <summary>
    /// Validates stored queries for a service.
    /// </summary>
    /// <param name="service">The service to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when stored query validation fails.</exception>
    private static void ValidateStoredQueries(ServiceDefinition service)
    {
        if (service.Ogc.StoredQueries.Count == 0)
        {
            return;
        }

        var queryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var storedQuery in service.Ogc.StoredQueries)
        {
            if (storedQuery is null)
            {
                throw new InvalidDataException($"Service '{service.Id}' contains a null stored query.");
            }

            if (storedQuery.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' contains a stored query without an id.");
            }

            if (!queryIds.Add(storedQuery.Id))
            {
                throw new InvalidDataException($"Service '{service.Id}' has duplicate stored query id '{storedQuery.Id}'.");
            }

            if (storedQuery.Title.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must have a title.");
            }

            if (storedQuery.LayerId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must specify a layerId.");
            }

            if (storedQuery.FilterCql.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must specify a filterCql expression.");
            }

            var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in storedQuery.Parameters)
            {
                if (parameter is null)
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' contains a null parameter.");
                }

                if (parameter.Name.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' contains a parameter without a name.");
                }

                if (!parameterNames.Add(parameter.Name))
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' has duplicate parameter '{parameter.Name}'.");
                }

                if (parameter.Type.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' parameter '{parameter.Name}' must have a type.");
                }

                if (parameter.Title.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' parameter '{parameter.Name}' must have a title.");
                }
            }
        }
    }
}
