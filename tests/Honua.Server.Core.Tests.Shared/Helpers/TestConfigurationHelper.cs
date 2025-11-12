// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Tests.Shared.Helpers;

/// <summary>
/// Helper methods for creating test configurations with Configuration V2 (HCL).
/// </summary>
public static class TestConfigurationHelper
{
    /// <summary>
    /// Creates a minimal HCL configuration with just a database connection.
    /// </summary>
    /// <param name="databaseUrl">The database connection string</param>
    /// <returns>HCL configuration string</returns>
    public static string CreateMinimalHclConfig(string databaseUrl)
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = "{{databaseUrl}}"
        }
        """;
    }

    /// <summary>
    /// Creates an HCL configuration with database and a single service.
    /// </summary>
    /// <param name="databaseUrl">The database connection string</param>
    /// <param name="serviceName">The name of the service to configure</param>
    /// <param name="enabled">Whether the service should be enabled</param>
    /// <returns>HCL configuration string</returns>
    public static string CreateServiceConfig(string databaseUrl, string serviceName, bool enabled = true)
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = "{{databaseUrl}}"
        }

        service "{{serviceName}}" {
            enabled = {{(enabled ? "true" : "false")}}
        }
        """;
    }

    /// <summary>
    /// Creates an HCL configuration with database and multiple services.
    /// </summary>
    /// <param name="databaseUrl">The database connection string</param>
    /// <param name="serviceNames">The names of services to configure (all enabled)</param>
    /// <returns>HCL configuration string</returns>
    public static string CreateMultiServiceConfig(string databaseUrl, params string[] serviceNames)
    {
        var servicesConfig = string.Join("\n\n", serviceNames.Select(name =>
            $$"""
            service "{{name}}" {
                enabled = true
            }
            """));

        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = "{{databaseUrl}}"
        }

        {{servicesConfig}}
        """;
    }

    /// <summary>
    /// Creates an HCL configuration using environment variable for database connection.
    /// </summary>
    /// <param name="envVariableName">The name of the environment variable containing the connection string</param>
    /// <returns>HCL configuration string</returns>
    public static string CreateEnvBasedConfig(string envVariableName = "DATABASE_URL")
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
        }

        data_source "test_db" {
            provider   = "postgresql"
            connection = env("{{envVariableName}}")
        }
        """;
    }

    /// <summary>
    /// Creates an HCL configuration with custom data source ID.
    /// </summary>
    /// <param name="dataSourceId">Custom data source identifier</param>
    /// <param name="databaseUrl">The database connection string</param>
    /// <returns>HCL configuration string</returns>
    public static string CreateCustomDataSourceConfig(string dataSourceId, string databaseUrl)
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
        }

        data_source "{{dataSourceId}}" {
            provider   = "postgresql"
            connection = "{{databaseUrl}}"
        }
        """;
    }
}
