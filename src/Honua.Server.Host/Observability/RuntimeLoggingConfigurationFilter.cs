// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Logging filter that checks RuntimeLoggingConfigurationService for dynamic log level overrides.
/// </summary>
public sealed class RuntimeLoggingConfigurationFilter : ILoggerProvider
{
    private readonly RuntimeLoggingConfigurationService _configService;

    public RuntimeLoggingConfigurationFilter(RuntimeLoggingConfigurationService configService)
    {
        _configService = Guard.NotNull(configService);
    }

    public ILogger CreateLogger(string categoryName)
    {
        // This is a filter provider, not a real logger provider
        // The actual filtering happens through IsEnabled checks
        throw new NotSupportedException("This is a filter provider only");
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    /// <summary>
    /// Checks if logging is enabled for the given category and level.
    /// Returns null to defer to other providers if no override is configured.
    /// </summary>
    public bool? IsEnabled(string categoryName, LogLevel logLevel)
    {
        // Check if there's a runtime override for this exact category
        return _configService.IsEnabled(categoryName, logLevel);
    }
}
