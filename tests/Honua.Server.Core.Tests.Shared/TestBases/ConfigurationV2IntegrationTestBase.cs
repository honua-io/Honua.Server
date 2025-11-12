// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Integration.Tests.Fixtures;
using Xunit;

namespace Honua.Server.Core.Tests.Shared.TestBases;

/// <summary>
/// Base class for integration tests that use Configuration V2 (HCL-based config).
/// </summary>
[Collection("DatabaseCollection")]
public abstract class ConfigurationV2IntegrationTestBase : IAsyncLifetime
{
    protected DatabaseFixture DatabaseFixture { get; }
    protected ConfigurationV2TestFixture<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    protected ConfigurationV2IntegrationTestBase(DatabaseFixture databaseFixture)
    {
        DatabaseFixture = databaseFixture;
    }

    public virtual Task InitializeAsync()
    {
        Factory = CreateFactory();
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory != null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected abstract ConfigurationV2TestFixture<Program> CreateFactory();

    protected string CreateHclConfig(string dataSourceId, string serviceName, bool enabled = true)
    {
        return $$"""
        honua {
            version     = "2.0"
            environment = "test"
        }

        data_source "{{dataSourceId}}" {
            provider   = "postgresql"
            connection = env("DATABASE_URL")
        }

        service "{{serviceName}}" {
            enabled = {{(enabled ? "true" : "false")}}
        }
        """;
    }
}
