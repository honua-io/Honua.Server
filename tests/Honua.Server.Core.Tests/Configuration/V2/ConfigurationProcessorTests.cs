// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Honua.Server.Core.Configuration.V2;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2;

public sealed class ConfigurationProcessorTests
{
    [Fact]
    public void Process_InterpolatesEnvironmentVariables_DollarBraceSyntax()
    {
        // Arrange
        var testVarName = "TEST_CONNECTION_STRING";
        var testVarValue = "Server=localhost;Database=test";
        Environment.SetEnvironmentVariable(testVarName, testVarValue);

        try
        {
            var config = new HonuaConfig
            {
                DataSources = new Dictionary<string, DataSourceBlock>
                {
                    ["test"] = new DataSourceBlock
                    {
                        Id = "test",
                        Provider = "postgresql",
                        Connection = "${env:TEST_CONNECTION_STRING}"
                    }
                }
            };

            var processor = new ConfigurationProcessor();

            // Act
            var processed = processor.Process(config);

            // Assert
            Assert.Equal(testVarValue, processed.DataSources["test"].Connection);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [Fact]
    public void Process_InterpolatesEnvironmentVariables_FunctionSyntax()
    {
        // Arrange
        var testVarName = "TEST_REDIS_CONNECTION";
        var testVarValue = "localhost:6379";
        Environment.SetEnvironmentVariable(testVarName, testVarValue);

        try
        {
            var config = new HonuaConfig
            {
                Caches = new Dictionary<string, CacheBlock>
                {
                    ["redis"] = new CacheBlock
                    {
                        Id = "redis",
                        Type = "redis",
                        Connection = "env(\"TEST_REDIS_CONNECTION\")"
                    }
                }
            };

            var processor = new ConfigurationProcessor();

            // Act
            var processed = processor.Process(config);

            // Assert
            Assert.Equal(testVarValue, processed.Caches["redis"].Connection);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [Fact]
    public void Process_MissingEnvironmentVariable_ThrowsException()
    {
        // Arrange
        var config = new HonuaConfig
        {
            DataSources = new Dictionary<string, DataSourceBlock>
            {
                ["test"] = new DataSourceBlock
                {
                    Id = "test",
                    Provider = "sqlite",
                    Connection = "${env:NONEXISTENT_VAR}"
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => processor.Process(config));
        Assert.Contains("NONEXISTENT_VAR", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void Process_InterpolatesVariableReferences()
    {
        // Arrange
        var config = new HonuaConfig
        {
            Variables = new Dictionary<string, object?>
            {
                ["database_name"] = "honua_dev"
            },
            DataSources = new Dictionary<string, DataSourceBlock>
            {
                ["test"] = new DataSourceBlock
                {
                    Id = "test",
                    Provider = "postgresql",
                    Connection = "Server=localhost;Database=var.database_name"
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act
        var processed = processor.Process(config);

        // Assert
        Assert.Equal("Server=localhost;Database=honua_dev", processed.DataSources["test"].Connection);
    }

    [Fact]
    public void Process_MissingVariable_ThrowsException()
    {
        // Arrange
        var config = new HonuaConfig
        {
            Variables = new Dictionary<string, object?>(),
            DataSources = new Dictionary<string, DataSourceBlock>
            {
                ["test"] = new DataSourceBlock
                {
                    Id = "test",
                    Provider = "sqlite",
                    Connection = "var.nonexistent_variable"
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => processor.Process(config));
        Assert.Contains("nonexistent_variable", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void Process_CombinedInterpolation_Success()
    {
        // Arrange
        var testVarName = "DB_HOST";
        var testVarValue = "prod-db.example.com";
        Environment.SetEnvironmentVariable(testVarName, testVarValue);

        try
        {
            var config = new HonuaConfig
            {
                Variables = new Dictionary<string, object?>
                {
                    ["db_name"] = "honua_production"
                },
                DataSources = new Dictionary<string, DataSourceBlock>
                {
                    ["prod"] = new DataSourceBlock
                    {
                        Id = "prod",
                        Provider = "postgresql",
                        Connection = "Server=${env:DB_HOST};Database=var.db_name;User=postgres"
                    }
                }
            };

            var processor = new ConfigurationProcessor();

            // Act
            var processed = processor.Process(config);

            // Assert
            Assert.Equal(
                "Server=prod-db.example.com;Database=honua_production;User=postgres",
                processed.DataSources["prod"].Connection);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [Fact]
    public void Process_ServiceSettings_InterpolatesStringValues()
    {
        // Arrange
        var config = new HonuaConfig
        {
            Variables = new Dictionary<string, object?>
            {
                ["page_size"] = "500"
            },
            Services = new Dictionary<string, ServiceBlock>
            {
                ["odata"] = new ServiceBlock
                {
                    Id = "odata",
                    Type = "odata",
                    Settings = new Dictionary<string, object?>
                    {
                        ["max_page_size"] = "var.page_size",
                        ["enabled"] = true
                    }
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act
        var processed = processor.Process(config);

        // Assert
        Assert.Equal("500", processed.Services["odata"].Settings["max_page_size"]);
        Assert.True((bool)processed.Services["odata"].Settings["enabled"]!);
    }

    [Fact]
    public void Process_NullConnectionString_DoesNotThrow()
    {
        // Arrange
        var config = new HonuaConfig
        {
            Caches = new Dictionary<string, CacheBlock>
            {
                ["memory"] = new CacheBlock
                {
                    Id = "memory",
                    Type = "memory",
                    Connection = null
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act
        var processed = processor.Process(config);

        // Assert
        Assert.Null(processed.Caches["memory"].Connection);
    }

    [Fact]
    public void Process_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        var config = new HonuaConfig
        {
            DataSources = new Dictionary<string, DataSourceBlock>
            {
                ["test"] = new DataSourceBlock
                {
                    Id = "test",
                    Provider = "sqlite",
                    Connection = ""
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act
        var processed = processor.Process(config);

        // Assert
        Assert.Equal("", processed.DataSources["test"].Connection);
    }

    [Fact]
    public void Process_StringWithoutInterpolation_ReturnsUnchanged()
    {
        // Arrange
        var originalConnection = "Data Source=./test.db";
        var config = new HonuaConfig
        {
            DataSources = new Dictionary<string, DataSourceBlock>
            {
                ["test"] = new DataSourceBlock
                {
                    Id = "test",
                    Provider = "sqlite",
                    Connection = originalConnection
                }
            }
        };

        var processor = new ConfigurationProcessor();

        // Act
        var processed = processor.Process(config);

        // Assert
        Assert.Equal(originalConnection, processed.DataSources["test"].Connection);
    }

    [Fact]
    public void Process_MultipleDataSources_ProcessesAllCorrectly()
    {
        // Arrange
        var testVar1 = "TEST_DB1";
        var testVar2 = "TEST_DB2";
        Environment.SetEnvironmentVariable(testVar1, "connection1");
        Environment.SetEnvironmentVariable(testVar2, "connection2");

        try
        {
            var config = new HonuaConfig
            {
                DataSources = new Dictionary<string, DataSourceBlock>
                {
                    ["db1"] = new DataSourceBlock
                    {
                        Id = "db1",
                        Provider = "sqlite",
                        Connection = "${env:TEST_DB1}"
                    },
                    ["db2"] = new DataSourceBlock
                    {
                        Id = "db2",
                        Provider = "postgresql",
                        Connection = "${env:TEST_DB2}"
                    }
                }
            };

            var processor = new ConfigurationProcessor();

            // Act
            var processed = processor.Process(config);

            // Assert
            Assert.Equal("connection1", processed.DataSources["db1"].Connection);
            Assert.Equal("connection2", processed.DataSources["db2"].Connection);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVar1, null);
            Environment.SetEnvironmentVariable(testVar2, null);
        }
    }

    [Fact]
    public void Process_HealthCheck_InterpolatesCorrectly()
    {
        // Arrange
        var testVarName = "HEALTH_CHECK_QUERY";
        var testVarValue = "SELECT 1 FROM health";
        Environment.SetEnvironmentVariable(testVarName, testVarValue);

        try
        {
            var config = new HonuaConfig
            {
                DataSources = new Dictionary<string, DataSourceBlock>
                {
                    ["test"] = new DataSourceBlock
                    {
                        Id = "test",
                        Provider = "postgresql",
                        Connection = "Server=localhost",
                        HealthCheck = "${env:HEALTH_CHECK_QUERY}"
                    }
                }
            };

            var processor = new ConfigurationProcessor();

            // Act
            var processed = processor.Process(config);

            // Assert
            Assert.Equal(testVarValue, processed.DataSources["test"].HealthCheck);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [Fact]
    public void Process_RateLimitStore_InterpolatesCorrectly()
    {
        // Arrange
        var config = new HonuaConfig
        {
            Variables = new Dictionary<string, object?>
            {
                ["rate_limit_store"] = "redis"
            },
            RateLimit = new RateLimitBlock
            {
                Enabled = true,
                Store = "var.rate_limit_store",
                Rules = new Dictionary<string, RateLimitRule>()
            }
        };

        var processor = new ConfigurationProcessor();

        // Act
        var processed = processor.Process(config);

        // Assert
        Assert.NotNull(processed.RateLimit);
        Assert.Equal("redis", processed.RateLimit.Store);
    }
}
