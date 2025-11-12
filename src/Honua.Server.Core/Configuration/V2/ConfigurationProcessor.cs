// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Processes configuration to resolve environment variables, variables, and references.
/// </summary>
public sealed class ConfigurationProcessor
{
    // Regex patterns for interpolation
    private static readonly Regex EnvVarPattern = new(@"\$\{env:([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex EnvFunctionPattern = new(@"env\([""']([^""']+)[""']\)", RegexOptions.Compiled);
    private static readonly Regex VarReferencePattern = new(@"var\.(\w+)", RegexOptions.Compiled);

    private Dictionary<string, object?> _variables = new();

    /// <summary>
    /// Process the configuration to resolve all interpolations and references.
    /// </summary>
    public HonuaConfig Process(HonuaConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        // Store variables for later resolution
        _variables = new Dictionary<string, object?>(config.Variables);

        // Process each section
        var processedDataSources = ProcessDataSources(config.DataSources);
        var processedServices = ProcessServices(config.Services);
        var processedLayers = ProcessLayers(config.Layers);
        var processedCaches = ProcessCaches(config.Caches);
        var processedRateLimit = ProcessRateLimit(config.RateLimit);

        return new HonuaConfig
        {
            Honua = config.Honua,
            DataSources = processedDataSources,
            Services = processedServices,
            Layers = processedLayers,
            Caches = processedCaches,
            RateLimit = processedRateLimit,
            Variables = config.Variables
        };
    }

    private Dictionary<string, DataSourceBlock> ProcessDataSources(Dictionary<string, DataSourceBlock> dataSources)
    {
        var result = new Dictionary<string, DataSourceBlock>();

        foreach (var (key, ds) in dataSources)
        {
            var processedConnection = InterpolateString(ds.Connection);

            result[key] = new DataSourceBlock
            {
                Id = ds.Id,
                Provider = ds.Provider,
                Connection = processedConnection,
                HealthCheck = ds.HealthCheck != null ? InterpolateString(ds.HealthCheck) : null,
                Pool = ds.Pool
            };
        }

        return result;
    }

    private Dictionary<string, ServiceBlock> ProcessServices(Dictionary<string, ServiceBlock> services)
    {
        var result = new Dictionary<string, ServiceBlock>();

        foreach (var (key, service) in services)
        {
            var processedSettings = new Dictionary<string, object?>();
            foreach (var (settingKey, settingValue) in service.Settings)
            {
                processedSettings[settingKey] = settingValue is string str
                    ? InterpolateString(str)
                    : settingValue;
            }

            result[key] = new ServiceBlock
            {
                Id = service.Id,
                Type = service.Type,
                Enabled = service.Enabled,
                Settings = processedSettings
            };
        }

        return result;
    }

    private Dictionary<string, LayerBlock> ProcessLayers(Dictionary<string, LayerBlock> layers)
    {
        var result = new Dictionary<string, LayerBlock>();

        foreach (var (key, layer) in layers)
        {
            result[key] = new LayerBlock
            {
                Id = layer.Id,
                Title = layer.Title,
                DataSource = layer.DataSource, // Reference resolution happens later
                Table = layer.Table,
                Description = layer.Description,
                Geometry = layer.Geometry,
                IdField = layer.IdField,
                DisplayField = layer.DisplayField,
                IntrospectFields = layer.IntrospectFields,
                Fields = layer.Fields,
                Services = layer.Services
            };
        }

        return result;
    }

    private Dictionary<string, CacheBlock> ProcessCaches(Dictionary<string, CacheBlock> caches)
    {
        var result = new Dictionary<string, CacheBlock>();

        foreach (var (key, cache) in caches)
        {
            var processedConnection = cache.Connection != null
                ? InterpolateString(cache.Connection)
                : null;

            result[key] = new CacheBlock
            {
                Id = cache.Id,
                Type = cache.Type,
                Enabled = cache.Enabled,
                Connection = processedConnection,
                RequiredIn = cache.RequiredIn
            };
        }

        return result;
    }

    private RateLimitBlock? ProcessRateLimit(RateLimitBlock? rateLimit)
    {
        if (rateLimit == null)
        {
            return null;
        }

        return new RateLimitBlock
        {
            Enabled = rateLimit.Enabled,
            Store = InterpolateString(rateLimit.Store),
            Rules = rateLimit.Rules
        };
    }

    /// <summary>
    /// Interpolate environment variables and variable references in a string.
    /// Supports:
    /// - ${env:VAR_NAME} syntax
    /// - env("VAR_NAME") function syntax
    /// - var.variable_name references
    /// </summary>
    private string InterpolateString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = input;

        // Process ${env:VAR_NAME} syntax
        result = EnvVarPattern.Replace(result, match =>
        {
            var varName = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(varName);
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"Environment variable '{varName}' not found. " +
                    $"Set the environment variable or update the configuration.");
            }
            return value;
        });

        // Process env("VAR_NAME") function syntax
        result = EnvFunctionPattern.Replace(result, match =>
        {
            var varName = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(varName);
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"Environment variable '{varName}' not found. " +
                    $"Set the environment variable or update the configuration.");
            }
            return value;
        });

        // Process var.variable_name references
        result = VarReferencePattern.Replace(result, match =>
        {
            var varName = match.Groups[1].Value;
            if (!_variables.TryGetValue(varName, out var value))
            {
                throw new InvalidOperationException(
                    $"Variable '{varName}' not found in configuration. " +
                    $"Define the variable or update the reference.");
            }
            return value?.ToString() ?? string.Empty;
        });

        return result;
    }
}
