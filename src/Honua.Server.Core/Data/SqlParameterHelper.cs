// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides common SQL parameter handling utilities for relational data store providers.
/// Consolidates parameter creation, normalization, and type conversion logic.
/// </summary>
public static class SqlParameterHelper
{
    /// <summary>
    /// Adds parameters from a dictionary to a database command.
    /// If a parameter already exists, updates its value; otherwise creates a new parameter.
    /// </summary>
    /// <typeparam name="TCommand">The type of database command</typeparam>
    /// <param name="command">The command to add parameters to</param>
    /// <param name="parameters">Dictionary of parameter names to values</param>
    /// <exception cref="ArgumentNullException">Thrown if command or parameters is null</exception>
    public static void AddParameters<TCommand>(TCommand command, IReadOnlyDictionary<string, object?> parameters)
        where TCommand : class, IDbCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var pair in parameters)
        {
            if (command.Parameters.Contains(pair.Key))
            {
                ((IDbDataParameter)command.Parameters[pair.Key]!).Value = pair.Value ?? DBNull.Value;
            }
            else
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = pair.Key;
                parameter.Value = pair.Value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }
    }

    /// <summary>
    /// Adds parameters from a collection to a database command.
    /// </summary>
    /// <typeparam name="TCommand">The type of database command</typeparam>
    /// <param name="command">The command to add parameters to</param>
    /// <param name="parameters">Collection of parameter name-value pairs</param>
    /// <exception cref="ArgumentNullException">Thrown if command or parameters is null</exception>
    public static void AddParameters<TCommand>(TCommand command, IEnumerable<KeyValuePair<string, object?>> parameters)
        where TCommand : class, IDbCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var pair in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = pair.Key;
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    /// <summary>
    /// Normalizes a feature ID parameter value based on the layer's ID field data type.
    /// Attempts to parse the string feature ID into the appropriate type (int, long, decimal, guid, etc).
    /// </summary>
    /// <param name="layer">The layer definition containing field metadata</param>
    /// <param name="featureId">The feature ID as a string</param>
    /// <returns>The feature ID converted to the appropriate type, or the original string if type conversion fails</returns>
    /// <exception cref="ArgumentNullException">Thrown if layer or featureId is null</exception>
    public static object NormalizeKeyParameter(LayerDefinition layer, string featureId)
    {
        // Delegate to LayerMetadataHelper to eliminate duplication
        return LayerMetadataHelper.NormalizeKeyValue(featureId, layer);
    }

    /// <summary>
    /// Attempts to resolve a feature's primary key value from its attribute dictionary.
    /// </summary>
    /// <param name="attributes">The feature's attribute dictionary</param>
    /// <param name="keyColumn">The name of the primary key column</param>
    /// <returns>The key value as a string, or null if not found</returns>
    /// <exception cref="ArgumentNullException">Thrown if attributes or keyColumn is null</exception>
    public static string? TryResolveKey(IReadOnlyDictionary<string, object?> attributes, string keyColumn)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(keyColumn);

        if (attributes.TryGetValue(keyColumn, out var value) && value is not null)
        {
            return value switch
            {
                System.Text.Json.Nodes.JsonNode node => node.ToJsonString(),
                System.Text.Json.JsonElement element => element.ValueKind == System.Text.Json.JsonValueKind.Null ? null : element.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        return null;
    }

    /// <summary>
    /// Normalizes a parameter name by removing special characters and ensuring it starts with '@'.
    /// Used for creating safe, consistent parameter names from column names.
    /// </summary>
    /// <param name="columnName">The column name to normalize</param>
    /// <param name="ordinal">The ordinal number to use if the column name is empty or invalid</param>
    /// <returns>A normalized parameter name starting with '@'</returns>
    public static string NormalizeParameterName(string columnName, int ordinal)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return $"@p{ordinal}";
        }

        var builder = new System.Text.StringBuilder();
        foreach (var ch in columnName)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        if (builder.Length == 0)
        {
            return $"@p{ordinal}";
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, 'p');
        }

        return "@" + builder.ToString();
    }

    /// <summary>
    /// Creates a unique parameter name with a counter suffix if duplicates exist.
    /// </summary>
    /// <param name="columnName">The column name to create a parameter for</param>
    /// <param name="counters">Dictionary tracking parameter name usage counts</param>
    /// <param name="ordinal">The ordinal number to use as a fallback</param>
    /// <returns>A unique parameter name</returns>
    public static string CreateUniqueParameterName(string columnName, IDictionary<string, int> counters, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(counters);

        var builder = new System.Text.StringBuilder();
        foreach (var ch in columnName)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        if (builder.Length == 0)
        {
            builder.Append($"p{ordinal}");
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, 'p');
        }

        var core = builder.ToString();
        if (counters.TryGetValue(core, out var count))
        {
            counters[core] = count + 1;
            core = $"{core}_{count + 1}";
        }
        else
        {
            counters[core] = 0;
        }

        return "@" + core;
    }

    /// <summary>
    /// Formats a parameter value for AWS-style Data APIs (like Redshift Data API).
    /// Converts values to string representations expected by cloud data APIs.
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <returns>String representation of the value for cloud data APIs</returns>
    public static string FormatParameterValue(object? value)
    {
        // AWS Redshift Data API expects parameter values as strings
        return value switch
        {
            null => "NULL",
            string s => s,
            bool b => b ? "TRUE" : "FALSE",
            int or long or short or byte or float or double or decimal => value.ToString()!,
            _ => value.ToString() ?? ""
        };
    }
}
