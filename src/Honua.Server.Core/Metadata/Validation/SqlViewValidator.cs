// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates SQL view definitions including SQL security, parameters, and required fields.
/// </summary>
internal static class SqlViewValidator
{
    /// <summary>
    /// Validates a SQL view definition for a layer.
    /// </summary>
    /// <param name="layer">The layer containing the SQL view.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <exception cref="InvalidDataException">Thrown when SQL view validation fails.</exception>
    public static void Validate(LayerDefinition layer, ILogger? logger)
    {
        var sqlView = layer.SqlView;
        if (sqlView is null)
        {
            return;
        }

        // Validate SQL security (SQL injection prevention)
        SqlSecurityValidator.ValidateSql(layer.Id, sqlView.Sql);

        // Validate parameters
        ValidateParameters(layer, sqlView);

        // Warn if timeout is very high
        if (sqlView.TimeoutSeconds is > 300)
        {
            logger?.LogWarning("Layer {LayerId} SQL view has a very high timeout of {TimeoutSeconds} seconds", layer.Id, sqlView.TimeoutSeconds);
        }

        // Check that required fields are included in the query
        ValidateRequiredFields(layer, sqlView.Sql);
    }

    /// <summary>
    /// Validates SQL view parameters.
    /// </summary>
    /// <param name="layer">The layer containing the SQL view.</param>
    /// <param name="sqlView">The SQL view to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when parameter validation fails.</exception>
    private static void ValidateParameters(LayerDefinition layer, SqlViewDefinition sqlView)
    {
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in sqlView.Parameters)
        {
            // Validate parameter structure and type
            ParameterValidator.ValidateParameter(layer.Id, parameter);

            // Check for duplicate parameters
            if (!parameterNames.Add(parameter.Name))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view has duplicate parameter '{parameter.Name}'.");
            }

            // Check that parameter is actually used in the SQL
            var paramRef = $":{parameter.Name}";
            if (!sqlView.Sql.Contains(paramRef, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view defines parameter '{parameter.Name}' but it is not used in the SQL query.");
            }
        }
    }

    /// <summary>
    /// Validates that required fields are included in the SQL query.
    /// </summary>
    /// <param name="layer">The layer containing the SQL view.</param>
    /// <param name="sql">The SQL query to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when required fields are missing.</exception>
    private static void ValidateRequiredFields(LayerDefinition layer, string sql)
    {
        // Check that required fields are included in the query
        // This is a simple check - we look for the field names in the SQL
        var requiredFields = new[] { layer.IdField, layer.GeometryField };

        foreach (var field in requiredFields)
        {
            if (!sql.Contains(field, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' SQL view must include the '{field}' field in the SELECT clause.");
            }
        }
    }
}
