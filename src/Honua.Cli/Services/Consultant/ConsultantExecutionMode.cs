// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Controls how the consultant workflow should execute a request.
/// </summary>
public enum ConsultantExecutionMode
{
    /// <summary>
    /// Let the workflow decide: attempt multi-agent orchestration when available,
    /// falling back to the traditional plan workflow if needed.
    /// </summary>
    Auto,

    /// <summary>
    /// Always use the traditional plan workflow (Semantic Kernel plan + executor).
    /// </summary>
    Plan,

    /// <summary>
    /// Require the multi-agent coordinator to handle the request; do not fall back automatically.
    /// </summary>
    MultiAgent
}

/// <summary>
/// Type converter that maps common CLI strings to <see cref="ConsultantExecutionMode"/> values.
/// </summary>
public sealed class ConsultantExecutionModeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            var normalized = text.Trim().ToLowerInvariant();
            return normalized switch
            {
                "auto" or "automatic" => ConsultantExecutionMode.Auto,
                "plan" or "plan-only" or "planmode" => ConsultantExecutionMode.Plan,
                "multi" or "multi-agent" or "multiagent" => ConsultantExecutionMode.MultiAgent,
                _ => throw new FormatException($"Unrecognized consultant execution mode: '{text}'. Expected 'auto', 'plan', or 'multi'.")
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}
