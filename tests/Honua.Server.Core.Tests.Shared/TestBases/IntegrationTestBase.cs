// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

namespace Honua.Server.Core.Tests.Shared.TestBases;

/// <summary>
/// Base class for integration tests using TestContainers and WebApplicationFactory.
/// Provides common setup, HTTP client access, and assertion helpers.
/// </summary>
[Collection("DatabaseCollection")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected DatabaseFixture DatabaseFixture { get; }
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTestBase(DatabaseFixture databaseFixture)
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

    protected virtual WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactoryFixture<Program>(DatabaseFixture);
    }

    // Common assertion helpers
    protected async Task<T> AssertSuccessAndDeserialize<T>(HttpResponseMessage response)
    {
        response.Should().BeSuccessful();
        var content = await response.Content.ReadFromJsonAsync<T>();
        content.Should().NotBeNull();
        return content!;
    }

    protected async Task AssertNotFound(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    protected async Task AssertBadRequest(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
