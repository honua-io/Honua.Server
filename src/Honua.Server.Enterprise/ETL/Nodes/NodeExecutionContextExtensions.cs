// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Extension methods for NodeExecutionContext to make parameter and input access easier
/// </summary>
public static class NodeExecutionContextExtensions
{
    /// <summary>
    /// Gets a parameter value with optional default
    /// </summary>
    public static T GetParameter<T>(this NodeExecutionContext context, string key, T defaultValue = default!)
    {
        if (context.Parameters.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Try to convert
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets input data from a specific input node and key
    /// </summary>
    public static T? GetInputData<T>(this NodeExecutionContext context, string key)
    {
        // Try to get from the first input node
        foreach (var input in context.Inputs.Values)
        {
            if (input.Data.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
        }

        return default;
    }

    /// <summary>
    /// Gets input data from a specific input node by node ID
    /// </summary>
    public static T? GetInputData<T>(this NodeExecutionContext context, string nodeId, string key)
    {
        if (context.Inputs.TryGetValue(nodeId, out var nodeResult))
        {
            if (nodeResult.Data.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
        }

        return default;
    }
}
